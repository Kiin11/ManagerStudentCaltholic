# BÁO CÁO CẬP NHẬT TÀI LIỆU TRI THỨC HỆ THỐNG (UPDATE-REPORT)

> **Dự án:** `ManagerStudentCaltholic`  
> **Phạm vi cập nhật:** Commit `c0e828a` ➔ `2a3263d` (+ thay đổi working tree tại `appsettings.json`, `appsettings.Development.json`, `docker-compose.yml`)  
> **Thời gian thực hiện:** `2026-11-15T13:50:56+07:00`  
> **Vai trò thực hiện:** Senior Software Architect

---

## 1. Danh sách File Knowledge Đã Cập nhật (`Modified`)

Chỉ những tài liệu thực sự bị ảnh hưởng bởi `53` tệp mã nguồn & cấu hình thay đổi (`7 Added`, `46 Modified`, `0 Deleted`, `0 Renamed`) mới được cập nhật; các tài liệu không bị ảnh hưởng (`00-OVERVIEW/CODING-RULES.md`, `00-OVERVIEW/SOURCE-INVENTORY.md`, `00-OVERVIEW/PROJECT-INVENTORY.json`, `00-OVERVIEW/MODULE-INVENTORY.json`, `00-OVERVIEW/PROJECT-DEPENDENCY-DIAGRAM.md`, `04-INTEGRATIONS/EXTERNAL-APIS.md`, và 4 file đặc tả `FEAT-P5-01/02/04/07` hiện hữu) được giữ nguyên 100%:

| STT | Đường dẫn File Knowledge | Nội dung cập nhật chính |
|---|---|---|
| 1 | `AI-Knowledge/AI-KNOWLEDGE-STRUCTURE.md` | Cập nhật sơ đồ cây thư mục Mục 2 phản ánh đúng toàn bộ file thực tế trong `00-OVERVIEW/`, `01-MODULES/`, `05-FEATURES/` và `06-UPDATE-LOG/`. |
| 2 | `AI-Knowledge/00-OVERVIEW/PROJECT.md` | Bổ sung `FEAT-P5-04` (QR HUD, `SessionMode`, `StudentCodeHistories`, hoàn tác 5s), `FEAT-P5-07` (`LessonPlansController`, `ClassCurriculumService`), `PublicRegistration/Resubmit`, thời hạn Cookie & Refresh Token 14 ngày + `Profile(long? id)`, `PwaController.Offline` (`/Offline`), Circuit Breaker 30s Redis và Nginx/Docker Compose. |
| 3 | `AI-Knowledge/00-OVERVIEW/ARCHITECTURE.md` | Bổ sung `ClassCurriculumService.cs` vào tầng Service; cập nhật `Flow 2` (hoàn tác 5s & cache alias mã năm cũ) và thêm `Flow 6` cho luồng quản lý kế hoạch lớp & version ngày sửa (`FEAT-P5-07`). |
| 4 | `AI-Knowledge/00-OVERVIEW/TECH-STACK.md` | Ghi nhận Hybrid Auth thời hạn **14 ngày** (`ParishSessionCookie` + `RefreshTokenExpirationDays`), `DataProtection` lưu khóa xuống `dataprotection-keys`, `RedisCacheService` Circuit Breaker 30s + `IMemoryCache` fallback, `LoggingExtensions` override `Antiforgery = Fatal`, và bổ sung `ClassCurriculumService.cs` cho `ClosedXML`/`PdfPig` cùng `wwwroot/js/offline-attendance.js`. |
| 5 | `AI-Knowledge/01-MODULES/ATTENDANCE.md` | Bổ sung tính năng `FEAT-P5-04` (Viewfinder HUD `ScanQr.cshtml`, `SessionMode`, tra cứu mã cũ từ `StudentCodeHistories`, `HistoricalCodes` cho Offline cache, `UndoScanRequestDto`), thêm `Flow 4` (Hoàn tác 5 giây Online & Offline) và dòng `QR Undo Check-in` trong Component Mapping Table. |
| 6 | `AI-Knowledge/02-DATABASE/DATABASE-OVERVIEW.md` | Bổ sung ghi chú cấu hình Fluent API cho `ClassLessonPlans`, `ClassCurriculumDocuments`, `ClassCurriculumVersions` (`ParishDbContext.cs:321-381`), hàm seed `WebApplicationExtensions.SeedSampleCurriculumData`, và cảnh báo `NEEDS_VERIFICATION` do `Migrations/` hiện rỗng. |
| 7 | `AI-Knowledge/02-DATABASE/TABLE-INDEX.json` | Thêm chi tiết cấu trúc cột, PK, FK và Index từ source code thực tế của 3 bảng: `ClassLessonPlans`, `ClassCurriculumDocuments`, `ClassCurriculumVersions`. |
| 8 | `AI-Knowledge/02-DATABASE/TABLE-RELATIONSHIPS.md` | Bổ sung Mục 7 (`Lesson Plans & Class Curriculum Versioning - FEAT-P5-07`), cập nhật sơ đồ `erDiagram` Mermaid và khối JSON `TABLE-INDEX.json` cho 3 bảng trên. |
| 9 | `AI-Knowledge/03-CODE-INDEX/FILE-INDEX.jsonl` | Bổ sung 10 dòng file index cho `PublicRegistrationController.cs`, `PwaController.cs`, `StudentsController.cs`, `ClassLessonPlan.cs`, `PublicRegistrationSubmitDto.cs`, `AttendanceViewModels.cs`, `Views/PublicRegistration/Resubmit.cshtml`, `Views/Attendance/ScanQr.cshtml`, `Views/LessonPlans/Index.cshtml`, `wwwroot/js/offline-attendance.js`. |
| 10 | `AI-Knowledge/03-CODE-INDEX/SYMBOL-INDEX.jsonl` | Bổ sung 40 dòng symbol mới với chữ ký và số dòng xác minh trực tiếp (`AccountController.Profile/UpdateProfile`, `ClassLessonPlan`, `ClassCurriculum*`, `CurriculumDtos`, `LessonPlanWorkspaceViewModel`, `IClassCurriculumService`, `ClassCurriculumService` + 8 methods, `LessonPlansController` + 6 actions, `UndoScan*`, `PublicRegistration*`, `PwaController*`). |
| 11 | `AI-Knowledge/03-CODE-INDEX/ROUTE-INDEX.jsonl` | Cập nhật `startLine` thực tế cho 7 action của `LessonPlansController` và bổ sung 11 route mới (`Attendance/UndoScanCheckIn`, `Attendance/GetOfflineStudentsCache`, `PublicRegistration/*`, `/Offline`, `/icons/{fileName}`). |
| 12 | `AI-Knowledge/03-CODE-INDEX/DEPENDENCY-INDEX.jsonl` | Chuẩn hóa `sourceLine` của `ClassCurriculumService` (`17`, `23`) và bổ sung 13 cạnh phụ thuộc (`INJECTS`, `CALLS`) đã xác minh cú pháp cho `LessonPlansController`, `AttendanceController.UndoScanCheckIn`, `RedisAttendanceService`, `PublicRegistrationController` và `PwaController`. |
| 13 | `AI-Knowledge/04-INTEGRATIONS/INTEGRATION-OVERVIEW.md` | Cập nhật `Redis Cache` (Circuit Breaker 30s + `IMemoryCache` fallback trong `RedisCacheService.cs`) và `IndexedDB` (tên tệp thực tế `wwwroot/js/offline-attendance.js`, hỗ trợ `HistoricalCodes` & `undoOfflineQrScan`). |
| 14 | `AI-Knowledge/05-FEATURES/FAST-QR-CHECKIN-AND-OFFLINE-SYNC.md` | Cập nhật `Flow 1` (`SessionMode`, tra cứu `StudentCodeHistories` + alias key), thêm `Flow 3` (Hoàn tác 5 giây Online & Offline), cập nhật `Code Mapping`, `Data Mapping`, `State Transitions`, `Dependencies`, và `Component Mapping`. |
| 15 | `AI-Knowledge/06-UPDATE-LOG/KNOWLEDGE-BASELINE.json` | Nâng `lastIndexedCommit` từ `c0e828a` lên `2a3263d`, cập nhật `lastUpdatedAt` và trạng thái `completed`. |
| 16 | `AI-Knowledge/06-UPDATE-LOG/UPDATE-LOG.md` | Ghi biên bản chi tiết phiên cập nhật `c0e828a` ➔ `2a3263d` (+ Working Tree `2026-11-15`). |
| 17 | `AI-Knowledge/06-UPDATE-LOG/PENDING-UPDATES.md` | Cập nhật trạng thái đồng bộ tới `2a3263d` và tổng hợp các mục `NEEDS_VERIFICATION`. |
| 18 | `AI-Knowledge/06-UPDATE-LOG/UPDATE-REPORT.md` | Cập nhật báo cáo tổng kết cho đợt đồng bộ hiện tại. |

*Ghi chú: 4 tệp phân tích `CHANGE-ANALYSIS.md`, `IMPACT-ANALYSIS.md`, `KNOWLEDGE-UPDATE-PLAN.md`, `AFFECTED-FILES.json` trong `AI-Knowledge/06-UPDATE-LOG/` đã được tạo/cập nhật ở bước phân tích trước đó và giữ nguyên trong bước cập nhật này.*

---

## 2. Danh sách File Knowledge Tạo Mới (`NEW FILE`)

- **Không có (`0` file tạo mới trong đợt cập nhật này).** Các đặc tả tính năng `FEAT-P5-04-QR-SCANNER-UPGRADE.md`, `FEAT-P5-07-CLASS-CURRICULUM-MANAGEMENT.md` và `UPDATE-REPORT.md` đã tồn tại sẵn từ commit `2a3263d` nên chỉ thực hiện cập nhật đồng bộ nơi thực sự bị sai lệch hoặc thiếu thông tin.

---

## 3. Danh sách File Knowledge Đã Xóa (`DELETE FILE`)

- **Không có (`0` file bị xóa).** Không có tệp mã nguồn, controller, hay feature nào bị xóa khỏi repository trong khoảng `c0e828a..2a3263d`.

---

## 4. Các Module và Feature Bị Ảnh Hưởng

| Module / Feature | Mức độ ảnh hưởng | Tóm tắt thay đổi |
|---|---|---|
| **Module `LessonPlans` (`FEAT-P5-07`)** | **HIGH** | Thêm `IClassCurriculumService`, `ClassCurriculumService`, 2 bảng `ClassCurriculumDocuments` / `ClassCurriculumVersions`, 6 action API trong `LessonPlansController`, giao diện 3 tab ở `Views/LessonPlans/Index.cshtml`, và đồng bộ hai chiều với `ClassLessonPlans`. |
| **Module `Attendance` (`FEAT-P5-04`)** | **HIGH** | Bổ sung `POST /Attendance/UndoScanCheckIn`, nhận diện thẻ mã năm cũ qua `StudentCodeHistories` + cache alias Redis O(1), hỗ trợ `SessionMode`, `HistoricalCodes` cho IndexedDB, sửa lỗi HTML modal trọng số chuyên cần trong `Attendance/Index.cshtml`, thiết kế lại Viewfinder HUD `ScanQr.cshtml`. |
| **Module `UserAccount` & `Security`** | **HIGH** | Nâng thời hạn Cookie `RememberMe` (`ParishSessionCookie`) và `RefreshTokenExpirationDays` từ 7 lên **14 ngày** (`AccountController`, `ServiceCollectionExtensions`, `TokenService`, `appsettings*.json`, `Login.cshtml`), mở rộng `Profile(long? id)` / `UpdateProfile`, lưu bền Data Protection Key Ring xuống `/app/dataprotection-keys` (`ServiceCollectionExtensions`, `Dockerfile`, `docker-compose.yml`), điều chỉnh validation DTO tạo/reset mật khẩu tối thiểu 6 ký tự. |
| **Module `Cache` (`RedisCacheService`)** | **HIGH** | Thêm Circuit Breaker **30 giây** (`IsRedisCircuitOpen` / `TripRedisCircuit`) kết hợp `IMemoryCache` fallback cho mọi service dùng `ICacheService`. |
| **Module `PublicRegistration`** | **MEDIUM** | Thêm luồng bổ sung & nộp lại hồ sơ bị từ chối (`GET Resubmit`, `POST SubmitResubmit`, `PublicRegistrationResubmitDto`, `Views/PublicRegistration/Resubmit.cshtml`, kiểm tra magic bytes file tải lên). |
| **Module `PWA` & `Mobile UI` (`FEAT-P5-02`)** | **MEDIUM** | Sửa `PwaController.Offline()` trả `View("Index")` cho `/Offline`; hoàn thiện `_LayoutMobileBottomNav.cshtml`, `_LayoutMobileHeader.cshtml`, `mobile-layout.css`, `Lookup/Result.cshtml`, `AccessDenied.cshtml`, `_Layout.cshtml`. |
| **Modules `Student`, `Grades`, `Promotion`, `Common`** | **LOW - MEDIUM** | `StudentsController.Create` gán đúng `AcademicYearId` cho `Enrollment`; `FeatureNavigationService` thay động `{class_id}`; sửa lỗi cấu trúc Razor/JS tại `Grades/Index.cshtml`, `Promotion/Index.cshtml`, `Students/Index.cshtml`; giới hạn `_StaffStudentLookupWidget` chỉ hiển thị ở tab Chung (`Home/Index.cshtml`). |

---

## 5. Các Dependency Thay Đổi (Đã Xác Minh Từ Source Code)

1. **Đăng ký DI & Bảo mật (`Extensions/ServiceCollectionExtensions.cs`):**
   - `services.AddScoped<IClassCurriculumService, ClassCurriculumService>();` (dòng 89).
   - `ExpireTimeSpan = TimeSpan.FromDays(14)` cho `ParishSessionCookie` (dòng 154).
   - `AddDataProtection().SetApplicationName("ManagerStudentCaltholic").PersistKeysToFileSystem(keysDirectory)` tại `dataprotection-keys` (dòng 250-258).
2. **Quan hệ `IMPLEMENTS` & `INJECTS`:**
   - `ClassCurriculumService` implements `IClassCurriculumService` (`ClassCurriculumService.cs:17`), injects `ParishDbContext` và `ILogger<ClassCurriculumService>` (`:23`).
   - `LessonPlansController` injects `ParishDbContext`, `IClassCurriculumService`, `ILogger<LessonPlansController>` (`LessonPlansController.cs:20`).
   - `RedisAttendanceService` injects `ParishDbContext`, `ICacheService`, `ILogger<RedisAttendanceService>` (`RedisAttendanceService.cs:17`).
   - `PublicRegistrationController` injects `ParishDbContext`, `IWebHostEnvironment`, `ILogger<PublicRegistrationController>` (`PublicRegistrationController.cs:21`).
   - `PwaController` injects `IWebHostEnvironment` (`PwaController.cs:11`).
3. **Quan hệ `CALLS` Mới Tại Controller:**
   - `LessonPlansController` gọi 6 phương thức trên `IClassCurriculumService` tại `ParseCurriculumFile` (`163`), `SaveCurriculum` (`192`), `GetVersionDetail` (`221`), `DownloadCurriculumVersion` (`244`), `ExportCurrentCurriculumExcel` (`268`), `RollbackCurriculum` (`295`).
   - `AttendanceController.UndoScanCheckIn` gọi `IRedisAttendanceService.UndoScanCheckInAsync` (`AttendanceController.cs:243`).

---

## 6. Những Điểm Chưa Xác Minh (`NEEDS_VERIFICATION`)

1. **Trạng thái Migration & DDL trên PostgreSQL cho `ClassCurriculumDocuments`, `ClassCurriculumVersions` và cột mới của `ClassLessonPlans`:**
   - Thư mục `ManagerStudentCaltholic/Migrations/` hiện có `0` tệp migration trong Git repository. Không thể xác nhận từ repo liệu database đích đã chạy DDL tạo 2 bảng mới và các cột `CurriculumDocumentId`, `WeekNumber`, `Semester` trên `ClassLessonPlans` hay chưa.
2. **Hai tệp Untracked ngoài Git `HEAD` (`nginx/default.conf`, `script/seed-curriculum-data.sql`):**
   - Chưa nằm trong commit `2a3263d`; cần xác nhận có thuộc cấu hình chuẩn triển khai chính thức hay không.
3. **Bảng `StaffQuickLookupLogs`, `LookupViewOptions` & Đặc tả `UserHomepageWidgetConfigs` / `TASK-P5-02.2..4`:**
   - Tiếp tục giữ trạng thái `NEEDS_VERIFICATION` kế thừa từ các phiên kiểm toán trước do thiếu file migration tương ứng và chưa thấy Entity `UserHomepageWidgetConfigs` trong source.

---

## 7. Những Nội Dung Cần Developer Kiểm Tra Thủ Công

1. **Kiểm tra & đồng bộ Database Schema PostgreSQL trước khi chạy ứng dụng:**
   - Xác minh sự tồn tại thực tế của các bảng `ClassCurriculumDocuments`, `ClassCurriculumVersions`, các cột mới trên `ClassLessonPlans`, cũng như `StaffQuickLookupLogs` và `LookupViewOptions` trong PostgreSQL trước khi bật `SeedSampleCurriculumData` hoặc truy cập `/LessonPlans` và `/Home/Index`.
   - Cân nhắc tạo lại EF Core Migration baseline hợp lệ vào `ManagerStudentCaltholic/Migrations/` nếu triển khai theo quy trình `dotnet ef database update`.
2. **Kiểm tra cấu hình Docker Volume & Nginx Reverse Proxy:**
   - Rà soát `docker-compose.yml` (`dataprotection_keys:/app/dataprotection-keys`), `Dockerfile`, và quyết định đưa `nginx/default.conf` vào quản lý Git nếu sử dụng làm cấu hình reverse proxy chính thức.
3. **Kiểm thử luồng Phiên 14 ngày & Làm mới JWT Token:**
   - Cookie `RememberMe` (`ParishSessionCookie`) có `ExpireTimeSpan = 14 ngày` (Sliding), còn JWT Access Token giữ hạn `30 phút` nên client gọi API bằng Bearer Token cần đảm bảo cơ chế `/Account/RefreshToken` hoạt động ổn định trong suốt vòng đời Refresh Token 14 ngày.
4. **Kiểm thử tính năng mới trên trình duyệt / Mobile:**
   - Kiểm tra quét QR + Hoàn tác 5 giây (Online & Offline + thẻ năm cũ) tại `/Attendance/ScanQr`.
   - Kiểm tra biên tập kế hoạch, nạp file đa định dạng và rollback version tại `/LessonPlans`.
   - Kiểm tra nộp lại hồ sơ bị từ chối tại `/PublicRegistration/Resubmit` và truy cập `/Offline`.
