# PROJECT OVERVIEW

## 1. Mục đích và Vai trò Project
Dự án **ManagerStudentCaltholic** là một ứng dụng Web Monolithic quản lý toàn diện các hoạt động của một trường Giáo lý thuộc Giáo xứ (cụ thể: Giáo xứ Phú Trung). 
- **Vai trò:** Hệ thống hóa việc quản lý hồ sơ thiếu nhi, xếp lớp, điểm danh (tích hợp mã QR và Offline-first), đánh giá học lực, xét duyệt điều kiện lãnh nhận Bí tích, và thông tin liên lạc với phụ huynh.

## 2. Project References & Entry Points
- **Entry Point:** `Program.cs` (Khởi tạo WebApplication, Kestrel server, cấu hình Middleware, ForwardedHeaders, Rate Limiter, Hangfire, Serilog, endpoint sức khỏe `GET /health`, và Route mặc định).
- **Cấu trúc Solution:** Là một dự án ứng dụng chính duy nhất (`ManagerStudentCaltholic.csproj` — Single-project Monolith chứa toàn bộ Data, Services, Controllers, Views), đi kèm project kiểm thử đơn vị độc lập `tests/ManagerStudentCaltholic.Tests/ManagerStudentCaltholic.Tests.csproj` (xUnit, loại trừ khỏi Docker build context).
- **Nhóm Helper Chuẩn hóa Nền tảng (`Services/`):** `VietnamTimeHelper` (múi giờ UTC+7), `AgeValidationHelper` (ràng buộc `>= 18` tuổi & năm nhuận `29/02`), `FileUploadSecurityHelper` (Magic Bytes, MIME allow-list, Path Traversal containment), và `StaffSelfActionGuard` (chặn tự điểm danh/tự chấm điểm cho `LinkedStudentId`).

## 3. Các Module Chính (Xác minh từ Controllers & Services)
1. **Quản lý Hồ sơ & Lớp học (Student & Class Management):** 
   - *Bằng chứng:* `StudentsController.cs`, `ClassesController.cs`, `EnrollmentsController.cs`. Xử lý thêm, sửa, xóa, phân bổ lớp, import Excel.
2. **Quản lý Điểm danh & Chuyên cần (Attendance):** 
   - *Bằng chứng:* `AttendanceController.cs`, `RedisAttendanceService.cs`, `Views/Attendance/ScanQr.cshtml`, `wwwroot/js/offline-attendance.js`. Điểm danh qua quét QR (khung ngắm Viewfinder HUD, chuyển đổi nhanh buổi quét `SessionMode`, cảnh báo & cache alias thẻ mã năm cũ từ `StudentCodeHistories`, hoàn tác 5 giây qua `UndoScanCheckIn` — `FEAT-P5-04`), xử lý Offline-first (IndexedDB), lưu cache Redis.
3. **Quản lý Học vụ, Giáo án & Sổ điểm (Academic, Curriculum & Grades):** 
   - *Bằng chứng:* `GradesController.cs`, `PromotionController.cs`, `LessonPlansController.cs`, `GradeCalculationService.cs`, `ClassCurriculumService.cs`. Nhập điểm, đánh giá xếp loại, kết chuyển năm học; quản lý Kế hoạch Lớp & Giáo án theo version ngày sửa (nạp file `.xlsx/.docx/.csv/.md/.pdf`, biên tập tuần trực tuyến, xuất `.xlsx`, xem lịch sử & rollback — `FEAT-P5-07`).
4. **Quản lý Sổ bộ Bí tích (Sacraments):** 
   - *Bằng chứng:* `SacramentsController.cs`, `SacramentRegistryService.cs`. Theo dõi tiến trình 4 mốc Bí tích, lập tờ trình xin Cha xứ phê duyệt.
5. **Chiến dịch Đi Lễ (Mass Campaigns):**
   - *Bằng chứng:* `MassCampaignsController.cs`, `MassCampaign.cs`. Theo dõi chuỗi đi lễ liên tục (Streak).
6. **Tương tác Phụ huynh, Tuyển sinh & Cổng Tra cứu (Parent Communication, Public Registration & Internal Staff Lookup):**
   - *Bằng chứng:* `ParentMessagesController.cs`, `LookupController.cs`, `PublicRegistrationController.cs`, `HomeController.cs`, `StaffStudentLookupService.cs`. Cổng tra cứu công khai cho phụ huynh; cổng nộp hồ sơ tuyển sinh trực tuyến kèm tra cứu trạng thái, bổ sung Giấy Rửa Tội và nộp lại hồ sơ bị từ chối (`Resubmit` / `SubmitResubmit`); Cổng tra cứu 360 độ thiếu nhi nội bộ tại Trang chủ cho GLV/BĐH đã đăng nhập (`FEAT-P5-01`), tạo mẫu tin nhắn Zalo/Viber, đơn xin phép nghỉ (`AbsenceController.cs`).
7. **Quản trị Hệ thống, Tài khoản & Phân quyền (Governance, User Account & System):**
   - *Bằng chứng:* `AccountController.cs`, `UsersController.cs`, `GovernanceController.cs`, `LogsController.cs`, `TokenService.cs`. Quản lý vai trò (Role), xem System Health (RAM, Database Latency); đăng nhập Hybrid Cookie/JWT duy trì phiên `14 ngày` (khi `RememberMe` và Refresh Token) và phân quyền Admin/BĐH/Cha Tuyên Úy xem & cập nhật hồ sơ GLV khác (`Profile(long? id)`, `UpdateProfile`).
8. **Giao diện Đa nền tảng, Mobile-First Web UI & PWA (Navigation, Responsive Shell & Offline Fallback):**
   - *Bằng chứng:* `HttpRequestExtensions.cs`, `PwaController.cs`, `_Layout.cshtml`, `_LayoutMobileHeader.cshtml`, `_LayoutMobileBottomNav.cshtml`, `mobile-layout.css`. Tự động nhận diện thiết bị di động ở phía Server (`Context.Request.IsMobileDevice()`) để chuyển sang giao diện Mobile-First với Thanh điều hướng đáy 5 phân khu (`FEAT-P5-02`), giữ nguyên giao diện Desktop trên màn hình lớn, và cung cấp trang dự phòng ngoại tuyến `/Offline` (`PwaController.Offline` trả `Views/Pwa/Index.cshtml`).

## 4. External Integrations
- **Redis Cache:** Sử dụng `StackExchange.Redis` để lưu trữ tạm thời dữ liệu điểm danh, tăng tốc độ quét QR, và cache hồ sơ tra cứu 360 độ thiếu nhi nội bộ (`staff_lookup:student_360:{studentId}`), tích hợp cơ chế Circuit Breaker `30 giây` kết hợp `IMemoryCache` fallback khi Redis ngoại tuyến. (*Bằng chứng:* `RedisAttendanceService.cs`, `RedisCacheService.cs`, `HomeController.cs`, `appsettings.json`).
- **PostgreSQL Database:** Lưu trữ dữ liệu chính. (*Bằng chứng:* `ParishDbContext.cs`, `Npgsql.EntityFrameworkCore.PostgreSQL`).
- **Nginx (Reverse Proxy) & Docker Compose:** Xử lý chuyển tiếp reverse proxy (`dotnet_app` là tên service chuẩn hóa trên `README.md`, `docs/DOCKER-BUILD-DEPLOY.md`, `gemini.md`), Rate Limiting (`GlobalLimiter` 200 req/min/IP kết hợp các policy chuyên biệt), `HEALTHCHECK` gọi `/health`, và mount volume `dataprotection_keys:/app/dataprotection-keys` + `/app/PrivateUploads` (*bằng chứng:* `Dockerfile`, `appsettings.json`, `ServiceCollectionExtensions.cs`, `SecurityExtensions.cs`; cấu hình deploy `docker-compose.yml` và `nginx/default.conf` tách riêng tại `parish-infra-config` — `NEEDS_VERIFICATION`).
