# ARCHITECTURE ANALYSIS

## 1. Cấu trúc Kiến trúc Thực tế
Dự án áp dụng mô hình **Monolithic MVC Architecture** kết hợp với **Service Layer Pattern** cho các tác vụ phức tạp. Không áp dụng Clean Architecture hay Repository Pattern.

### 1.1. Controller Layer (MVC)
- **Đặc điểm:** Tiếp nhận HTTP Request, xử lý Authentication/Authorization, Validation.
- **Business Logic trong Controller:** Rất nhiều logic nghiệp vụ, truy vấn và ghi cơ sở dữ liệu được viết trực tiếp tại Controller thông qua Entity Framework Core.
- *Bằng chứng:* `ClassesController.cs`, `AbsenceController.cs` (gọi trực tiếp `_context.AbsenceRequests.Add(...)`, `_context.SaveChangesAsync()`).

### 1.2. Service Layer (Nghiệp vụ phức tạp)
- **Đặc điểm:** Các logic đòi hỏi tính toán nhiều, đồng bộ dữ liệu hoặc giao tiếp ngoại vi được tách ra Service.
- *Bằng chứng:* 
  - `GradeCalculationService.cs` (tính điểm trung bình).
  - `RedisAttendanceService.cs` (xử lý logic điểm danh trên Cache, hỗ trợ `SessionMode`, tra cứu mã năm cũ, hoàn tác 5 giây và đồng bộ về DB).
  - `ClassCurriculumService.cs` (đọc nạp file kế hoạch `.xlsx/.docx/.csv/.md/.pdf`, quản lý version ngày sửa, xuất Excel & đồng bộ `ClassLessonPlans` — `FEAT-P5-07`).
  - `StaffStudentLookupService.cs` (tìm kiếm đa khóa và tổng hợp hồ sơ 360 độ thiếu nhi cho GLV/BĐH tại Trang chủ).
  - `TokenService.cs` (tạo và rotate JWT/Refresh Token).

### 1.3. Data Access Layer
- **Đặc điểm:** Không sử dụng Repository Pattern. Sử dụng trực tiếp `ParishDbContext` (kế thừa `DbContext` của EF Core) để truy xuất dữ liệu trong cả Controllers và Services.
- *Bằng chứng:* Khai báo `private readonly ParishDbContext _context;` phủ khắp các lớp Controller và Service.

### 1.4. Background Jobs Layer
- **Đặc điểm:** Sử dụng **Hangfire** (PostgreSql Storage) để xử lý các tác vụ nền không đồng bộ (Asynchronous processing).
- *Bằng chứng:* `BackgroundExportJobService.cs`, `Program.cs` (đăng ký `RecurringJob.AddOrUpdate`).
  - Xuất báo cáo Excel (`GenerateClassExcelReportJobAsync`).
  - Đồng bộ điểm danh Write-Through trực tiếp từ `ScanCheckIn` / `SaveBatch` xuống Database trong cùng request, kết hợp Hangfire job dự phòng mỗi 10 phút.
  - Tự động đánh vắng học sinh cuối ngày.

## 2. Request Flow (Luồng xử lý dữ liệu)

### Flow 1: Luồng xử lý CRUD cơ bản (Xác nhận từ source)
**HTTP Request** (từ Client) -> **Routing & Rate Limiter Middleware** -> **Controller** (Kiểm tra quyền `[Authorize]`) -> **Direct Data Access** (truy vấn qua `_context`) -> **PostgreSQL Database** -> **Controller** -> Trả về **Razor View / JSON Response**.
*(Bằng chứng: `AnnouncementsController.cs`)*

### Flow 2: Luồng Điểm danh QR Code Tốc độ cao & Hoàn tác 5 Giây (`FEAT-P5-04`) (Xác nhận từ source)
1. **Quét QR & Lưu lưới điểm danh (Write-Through):** **HTTP Request** (`POST /Attendance/ScanCheckIn` hoặc `POST /Attendance/SaveBatch`) -> **AttendanceController** -> **RedisAttendanceService** (`ProcessQrCheckInViaRedisAsync()` / `SaveBatchToRedisAsync()`) -> Cập nhật **Redis Cache** (`ATT:CLASS:{classId}:DATE:{date}`, `IsDirty = true`; với QR Cache Miss sẽ tra cứu `Enrollments` hoặc mã cũ trong `StudentCodeHistories` và nạp alias key O(1)) -> Gọi ngay `SyncDirtyAttendancesToDatabaseAsync()` trong cùng Request để ghi **Write-Through trực tiếp** xuống **PostgreSQL** -> Trả về JSON (`QrScanResponseDto` hoặc `{ success, message }`).
2. **Hoàn tác 5 giây:** **Client** -> `POST /Attendance/UndoScanCheckIn` (`UndoScanRequestDto`) -> **AttendanceController.UndoScanCheckIn()** -> **RedisAttendanceService.UndoScanCheckInAsync()** thu hồi trạng thái trên Redis ClassSheet và reset bản ghi trong PostgreSQL (`Attendances`, `MassCampaignRecords`). Nếu đang Offline, Client gọi `ParishOfflineEngine.undoOfflineQrScan(clientRecordId)` để xóa khỏi IndexedDB.
*(Bằng chứng: `AttendanceController.cs`, `RedisAttendanceService.cs`, `AttendanceViewModels.cs`, `ScanQr.cshtml`, `offline-attendance.js`)*

### Flow 3: Luồng Đồng bộ Dữ liệu Nền (Xác nhận từ source)
**Hangfire Worker** (cơ chế dự phòng định kỳ mỗi 10 phút nếu DB có lỗi tạm thời) -> **RedisAttendanceService.SyncDirtyAttendancesToDatabaseAsync()** -> Lấy các phân vùng còn cờ `IsDirty = true` từ **Redis** -> Sử dụng **IExecutionStrategy & Transaction** -> Lưu hàng loạt xuống **PostgreSQL Database**.
*(Bằng chứng: `Program.cs`, `RedisAttendanceService.cs`)*

### Flow 4: Luồng Tra cứu Tức thời 360 Độ Thiếu nhi Nội bộ tại Trang chủ (`FEAT-P5-01`)
1. **Gợi ý tìm kiếm (Autocomplete):** **Client (`staff-student-lookup.js`)** -> `GET /Home/StaffSearchSuggestions?term=...` -> **HomeController.StaffSearchSuggestions()** (`[Authorize]`) -> **StaffStudentLookupService.SearchStudentsQuickAsync()** -> Truy vấn `Students` (Họ tên, Tên Thánh, Mã hiện hành) kết hợp `StudentCodeHistories` (Mã 2 năm cũ) -> Trả về danh sách `StaffStudentQuickSearchResultDto`.
2. **Xem chi tiết 360 độ & Ghi nhận lịch sử:** **Client** -> `GET /Home/StaffStudentLookup?studentId=...&option=...` -> **HomeController.StaffStudentLookup()** (`[Authorize]`) -> **ICacheService.GetOrSetAsync(`staff_lookup:student_360:{studentId}`, TTL 5 phút)** -> (Cache Miss) **StaffStudentLookupService.GetStudent360DetailAsync()** tổng hợp từ `Students`, `Enrollments`, `Attendances`, `MassCampaigns`, `GradeRecords`, `StudentCodeHistories`, `StudentSacramentRecords`, `ParentMessageTemplates` -> Ghi log tra cứu vào bảng `StaffQuickLookupLogs` qua `RecordLookupLogAsync()` -> Trả về JSON `StaffStudent360DetailDto` hiển thị theo 4 tab Option (`OVERVIEW`, `ATTENDANCE_DETAIL`, `ACADEMIC_2Y_SACRAMENT`, `PARENT_CONTACT`).
*(Bằng chứng: `HomeController.cs`, `StaffStudentLookupService.cs`, `_StaffStudentLookupWidget.cshtml`, `staff-student-lookup.js`)*

### Flow 5: Luồng Chuyển đổi Giao diện Mobile-First & Desktop (`FEAT-P5-02`)
**HTTP Request** -> **Razor Layout (`Views/Shared/_Layout.cshtml`)** -> Gọi `Context.Request.IsMobileDevice()` (`HttpRequestExtensions.cs` kiểm tra query `?device=mobile|desktop`, Client Hint header `Sec-CH-UA-Mobile`, và Regex `User-Agent`):
- **Nhánh `isMobile == true`:** Render `_LayoutMobileHeader.cshtml` (Header thu gọn + Offcanvas Menu tối ưu cuộn cảm ứng) + `.mobile-main-wrapper` + `_LayoutMobileBottomNav.cshtml` (Thanh điều hướng đáy cố định 5 phân khu, nút trung tâm Quét QR nổi, hỗ trợ Safe Area Inset iOS/Android).
- **Nhánh `isMobile == false`:** Giữ nguyên toàn bộ cấu trúc Header, Master Workspace 5 phân khu, Sub-navigation và Footer của màn hình Desktop.
*(Bằng chứng: `HttpRequestExtensions.cs`, `_Layout.cshtml`, `_LayoutMobileHeader.cshtml`, `_LayoutMobileBottomNav.cshtml`, `mobile-layout.css`)*

### Flow 6: Luồng Quản lý Kế hoạch Lớp & Phiên bản Giáo án Theo Ngày Sửa (`FEAT-P5-07`)
1. **Nạp & Phân tích file đa định dạng:** **Client (`Views/LessonPlans/Index.cshtml`)** -> `POST /LessonPlans/ParseCurriculumFile` -> **LessonPlansController.ParseCurriculumFile()** -> **ClassCurriculumService.ParseUploadedCurriculumFileAsync()** (hỗ trợ `.xlsx`, `.docx`, `.csv`, `.md`, `.pdf`) -> Trả về JSON `CurriculumParseResultDto`.
2. **Lưu phiên bản, Sinh `.xlsx` & Đồng bộ DB:** **Client** -> `POST /LessonPlans/SaveCurriculum` (`SaveCurriculumRequestDto`) -> **LessonPlansController.SaveCurriculum()** (kiểm tra quyền `CanManageClassLessonAsync`) -> **ClassCurriculumService.SaveCurriculumAsync()** -> Sử dụng `IExecutionStrategy` + transaction cập nhật `ClassCurriculumDocuments`, tạo bản ghi lịch sử `ClassCurriculumVersions`, sinh file Excel đính kèm trong `PrivateUploads/curriculums` (`KeHoach_{Class}_{Version}_{YYYYMMDD}.xlsx`) và đồng bộ xuống `ClassLessonPlans`.
3. **Xem lịch sử, Tải file & Khôi phục (Rollback):** **Client** -> `GET /LessonPlans/GetVersionDetail/{versionId}`, `GET /LessonPlans/DownloadCurriculumVersion/{versionId}`, `GET /LessonPlans/ExportCurrentCurriculumExcel`, hoặc `POST /LessonPlans/RollbackCurriculum` -> **LessonPlansController** -> **ClassCurriculumService** (`GetVersionDetailAsync`, `GetVersionFileAsync`, `ExportCurriculumToExcelAsync`, `RollbackCurriculumAsync`).
*(Bằng chứng: `LessonPlansController.cs`, `ClassCurriculumService.cs`, `IClassCurriculumService.cs`, `CurriculumDtos.cs`, `Views/LessonPlans/Index.cshtml`)*
