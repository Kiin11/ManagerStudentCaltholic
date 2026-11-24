# BÁO CÁO PHÂN TÍCH TÁC ĐỘNG (IMPACT ANALYSIS REPORT)

- **Dự án:** `ManagerStudentCaltholic`
- **Thời gian phân tích:** `2026-11-24T12:20:00+07:00`
- **Phạm vi đối chiếu:** `e183ccd` (`4589783` + docs `FEAT-P5-08`) ➔ `HEAD` (`e147f88`), kèm ghi chú khoảng nền `2a3263d..e183ccd` cho `03-CODE-INDEX/`.

---

## 1. Phân loại Mức độ Ảnh hưởng Tổng thể

### **Mức độ: HIGH (Ảnh hưởng diện rộng trên nhiều module, API, security & hiệu năng)**

**Căn cứ kỹ thuật đã xác minh trong mã nguồn:**
1. **Bổ sung 4 lớp nền tảng bảo mật & chuẩn hóa dùng chung xuyên suốt 21 Controllers và 17 Services:** `VietnamTimeHelper`, `AgeValidationHelper`, `FileUploadSecurityHelper`, `StaffSelfActionGuard`.
2. **Thay đổi hợp đồng API / HTTP status & chữ ký Interface:**
   - Breaking signature: `IClassCurriculumService.GetVersionFileAsync` đổi từ `byte[]` sang `Stream`.
   - Overload mới có `ClaimsPrincipal` / `ClaimsPrincipal?`: `IRedisAttendanceService` (`ProcessQrCheckInViaRedisAsync`, `UndoScanCheckInAsync`), `IStaffStudentLookupService` (`SearchStudentsQuickAsync`, `GetRecentLookupsAsync`, thêm `CanCallerAccessStudentAsync`), `IGradeCalculationService` (`EvaluateGradeFormula` với `Dictionary<string, decimal?>`).
   - Nhiều endpoint AJAX/MVC trước đây trả `200 JSON` lỗi hoặc `Forbid()` / `400` / `404` nay trả **`403 Forbidden`** hoặc **`429 TooManyRequests`** chuẩn xác (yêu cầu đồng bộ xử lý phía UI JS ở `ScanQr.cshtml`, `Attendance/Index.cshtml`, `offline-attendance.js`, `Lookup/Result.cshtml`).
3. **Thay đổi chính sách bảo mật biên (Edge Security), Rate Limiting & Startup Config:** Bật `GlobalLimiter` (`200 req/min`), thu hẹp `KnownNetworks` cho `ForwardedHeaders`, thêm endpoint `/health`, đặt `RedisConnection = ""` và tắt mặc định `GoogleDrive:Enabled` / `DriveLogUpload:Enabled` trong `appsettings.json`.
4. **Thay đổi cơ chế đồng bộ & hoàn tác Điểm danh (`RedisAttendanceService`, `AttendanceController.SyncOfflineQueue`, `offline-attendance.js`):** Khóa phân dải `ExecuteWithPartitionsFlushLockAsync`, idempotency key 24h `ATT:OFFLINE:IDEMP:*`, nâng IndexedDB lên `DB_VERSION = 2`.

---

## 2. Ma trận Phụ thuộc & Thành phần Bị Ảnh hưởng

Quy ước bằng chứng:
- **[Xác nhận được]**: Có lời gọi trực tiếp, kế thừa/DI hoặc tham chiếu cú pháp đã kiểm chứng tại dòng code cụ thể.
- **[Suy luận kiến trúc]**: Tương tác gián tiếp qua Redis key, cấu hình hạ tầng ngoài repo (`parish-infra-config` / Nginx / Docker Compose), hoặc tệp không thuộc phạm vi mã nguồn C# của repo.

### 2.1. Các Helper Nền tảng Mới & Test Suite

| Thành phần thay đổi | Thành phần gọi đến (Callers) | Thành phần được gọi (Callees) | Bằng chứng & Dòng code |
|---|---|---|---|
| `VietnamTimeHelper` | `Program.cs` (`:178`), 12 Controllers (`Absence`, `Attendance`, `Facilities`, `Governance`, `LessonPlans`, `Lookup`, `MassCampaigns`, `ParentMessages`, `PublicRegistration`, `Sacraments`, `Users`), 11 Services (`AttendanceDispute`, `BackgroundExportJob`, `ClassCurriculum`, `ClassEvaluation`, `DriveLogUpload`, `LessonPlanImport`, `LogRetentionBackground`, `MassCampaign`, `ProfileHistory`, `RedisAttendance`, `SacramentEvaluation`, `StudentExcel`), 7 Razor Views, `LookupViewModels`, `StudentViewModel`, `VietnamTimeHelperTests` **[Xác nhận được]** | `System.TimeZoneInfo` (`Asia/Ho_Chi_Minh`, `SE Asia Standard Time`, custom UTC+7) **[Xác nhận được]** | `Services/VietnamTimeHelper.cs:7-71`; `Program.cs:178`; `VietnamTimeHelperTests.cs:14-35` |
| `AgeValidationHelper` | `StudentViewModel.CalculateAgeAtDate` (`:74`), `AccountController` (`:450-452`), `GovernanceController` (`:299-311`), `StudentsController` (`:335-337,486-488,1091-1094`), `UsersController` (`:224-234,799-801`), `ProfileHistoryService` (`:308-362`), `AgeValidationTests` **[Xác nhận được]** | `System.DateTime.AddYears` **[Xác nhận được]** | `Services/AgeValidationHelper.cs:7-37` |
| `FileUploadSecurityHelper` | `AbsenceController` (`:190,197`), `BackgroundJobsController` (`:203,209`), `LessonPlansController` (`:375-574`), `PublicRegistrationController` (`:372-537`), `StudentRegistrationsController` (`:146,154`), `StudentsController` (`:586,595`), `UsersController` (`:647,655`), `ClassCurriculumService` (`:249,624`), `LessonPlanImportService` (`:44`), `StudentExcelService` (`:126`), `SecurityHelpersTests` **[Xác nhận được]** | `System.IO.Path`, `System.IO.Stream` **[Xác nhận được]** | `Services/FileUploadSecurityHelper.cs:8-186` |
| `StaffSelfActionGuard` | `AbsenceController` (`:503-504`), `AttendanceController` (`:176,247,386,1704,1778,2530`), `GradesController` (`:310,354`), `AttendanceDisputeService` (`:275-277`), `ClassEvaluationService` (`:386,441`), `MassCampaignService` (`:390,492,586`), `RedisAttendanceService` (`:849,1301,3111`), `SecurityHelpersTests` **[Xác nhận được]** | `ParishDbContext.Users` (`LinkedStudentId`, `StudentId`) **[Xác nhận được]** | `Services/StaffSelfActionGuard.cs:9-30` |

### 2.2. Phân hệ Điểm danh QR, Offline Sync & Khiếu nại (`ATTENDANCE`, `FAST-QR`, `FEAT-P5-04`)

| Thành phần thay đổi | Callers | Callees | Bằng chứng & Dòng code |
|---|---|---|---|
| `IRedisAttendanceService` / `RedisAttendanceService` | `AttendanceController` (`:22-2477`), `AbsenceController` (`:575-588`), `AttendanceWriteBehindBackgroundService` (`:198-273`), Hangfire `RecurringJob` trong `Program.cs` (`:182-214`), DI `ServiceCollectionExtensions` (`:208`) **[Xác nhận được]** | `ParishDbContext`, `ICacheService`, `IAttendanceFlushSignal`, `VietnamTimeHelper`, `StaffSelfActionGuard`, `GradeLevel.IsAfternoonSession`, `AttendanceTimeHelper`, `RedisAttendanceKeys`, `UserRole`, `AttendanceStatus` **[Xác nhận được]** | `IRedisAttendanceService.cs:42-65`; `RedisAttendanceService.cs:814-3283` |
| `AttendanceController` | `Views/Attendance/Index.cshtml`, `ScanQr.cshtml`, `MonthlySummary.cshtml`, `wwwroot/js/offline-attendance.js` **[Xác nhận được]** | `IRedisAttendanceService`, `IAttendanceDisputeService`, `ICacheService`, `StaffSelfActionGuard`, `VietnamTimeHelper`, `ParishDbContext` **[Xác nhận được]** | `AttendanceController.cs:176-2538` |
| `AttendanceDisputeService` | `AttendanceController` (`:2499,2538`), `LookupController` (`:177,500`), DI (`:212`) **[Xác nhận được]** | `ParishDbContext`, `IStudentCodeRolloverService`, `ICacheService`, `VietnamTimeHelper`, `StaffSelfActionGuard`, `GradeLevel.IsAfternoonSession` **[Xác nhận được]** | `AttendanceDisputeService.cs:31-483` |
| `wwwroot/js/offline-attendance.js` | `Views/Attendance/Index.cshtml`, `Views/Attendance/ScanQr.cshtml`, `Views/Pwa/Index.cshtml` **[Xác nhận được]** | IndexedDB (`DB_VERSION = 2`, `students_cache`, `pending_scans`), `AttendanceController.SyncOfflineQueue` **[Xác nhận được]** | `offline-attendance.js`; `AttendanceViewModels.cs:343` |

### 2.3. Phân hệ Tra cứu 360, Hồ sơ, Lớp học & Học vụ (`FEAT-P5-01`, `FEAT-P6-01`, `Grades`, `Sacraments`)

| Thành phần thay đổi | Callers | Callees | Bằng chứng & Dòng code |
|---|---|---|---|
| `IStaffStudentLookupService` / `StaffStudentLookupService` | `HomeController` (`:43,62,78,93,108,131,140`), DI (`:216`) **[Xác nhận được]** | `ParishDbContext`, `IGradeCalculationService`, `VietnameseStringHelper`, `GradeLevel.IsAfternoonSession`, `UserRole` **[Xác nhận được]** | `IStaffStudentLookupService.cs:12-17`; `StaffStudentLookupService.cs:59-772` |
| `HomeController` (`StaffSearchSuggestions`, `StaffStudentLookup`, `StaffRecentLookups`, `StaffLookupOptions`) | `Views/Home/Index.cshtml`, `wwwroot/js/staff-student-lookup.js` **[Xác nhận được]** | `IStaffStudentLookupService`, `ICacheService` (`staff_lookup:student_360:{studentId}`), `IFeatureNavigationService`, `IDeviceStudentRecognitionService` **[Xác nhận được]** | `HomeController.cs:43-140` |
| `IGradeCalculationService` / `GradeCalculationService` | `GradesController`, `LookupController`, `ParentMessagesController`, `PrintReportsController`, `ClassEvaluationService`, `ProfileHistoryService`, `PromotionEvaluationService`, `SacramentEvaluationService`, `StaffStudentLookupService`, `GradeAndSacramentLogicTests` **[Xác nhận được]** | `NCalc.Expression`, `GradeConfiguration` / `AttendanceScoreRule` **[Xác nhận được]** | `IGradeCalculationService.cs:32`; `GradeCalculationService.cs:27-135` |
| `GradesController` & `ClassEvaluationService` | `Views/Grades/Index.cshtml` **[Xác nhận được]** | `ParishDbContext` (`ExecutionStrategy` + `Transaction`), `IGradeCalculationService`, `StaffSelfActionGuard`, `VietnamTimeHelper`, `GradeLevel.IsAfternoonSession` **[Xác nhận được]** | `GradesController.cs:49-670`; `ClassEvaluationService.cs:100-608` |
| `SacramentsController` & `SacramentEvaluationService` | `Views/Sacraments/Index.cshtml`, `Evaluation.cshtml`, `PrintApprovalSheet.cshtml`, `GradeAndSacramentLogicTests` **[Xác nhận được]** | `ParishDbContext`, `IGradeCalculationService`, `VietnamTimeHelper`, `GradeLevel.IsAfternoonSession`, `ClosedXML` **[Xác nhận được]** | `SacramentsController.cs:59-1040`; `SacramentEvaluationService.cs:49-595` |
| `PromotionEvaluationService` | `PromotionController` (`:89,90,109,266,267`) **[Xác nhận được]** | `ParishDbContext`, `IGradeCalculationService`, `IStudentPlacementService`, `AttendanceStatus` **[Xác nhận được]** | `PromotionEvaluationService.cs:107-316` |
| `GradePermissionService` | `ServiceCollectionExtensions` (DI `:188`) **[Xác nhận được]** — chưa có controller/service nào gọi trong `HEAD` | `ParishDbContext`, `ICacheService`, `CacheKeys`, `GradeLevel.IsAfternoonSession` **[Xác nhận được]** | `GradePermissionService.cs:19-176` |
| `AccountController`, `StudentsController`, `UsersController`, `GovernanceController`, `ProfileHistoryService`, `ClassesController`, `MassCampaignService` | Views tương ứng (`Account`, `Students`, `Users`, `Governance`, `Classes`, `MassCampaigns`) **[Xác nhận được]** | `AgeValidationHelper`, `VietnamTimeHelper`, `FileUploadSecurityHelper`, `StaffSelfActionGuard`, `ParishDbContext`, `ICacheService` (`CacheKeys.UserPrincipalState`) **[Xác nhận được]** | `AccountController.cs:450`; `UsersController.cs:224,799`; `GovernanceController.cs:299`; `StudentsController.cs:335-1094` |

### 2.4. Phân hệ Giáo án, Lưu trữ Google Drive, Media Proxy, Tuyển sinh & Hiệu năng (`FEAT-P5-07`, `FEAT-P5-08`)

| Thành phần thay đổi | Callers | Callees | Bằng chứng & Dòng code |
|---|---|---|---|
| `CloudMediaController.File` | Razor Views & Services tham chiếu `/CloudMedia/File/{id}` **[Suy luận kiến trúc]** | `ParishDbContext`, `IGoogleDriveStorageService` (`DownloadAsync`), `UserRole`, `GradeLevel.IsAfternoonSession` **[Xác nhận được]** | `CloudMediaController.cs:39-485` |
| `IClassCurriculumService` / `ClassCurriculumService` | `LessonPlansController` (`:157-360`), DI (`:186`) **[Xác nhận được]** | `ParishDbContext`, `IGoogleDriveStorageService`, `FileUploadSecurityHelper`, `VietnamTimeHelper`, `ClosedXML`/`OpenXml`/`PdfPig` **[Xác nhận được]** | `IClassCurriculumService.cs:46`; `ClassCurriculumService.cs:210-1159` |
| `LessonPlanImportService` | DI (`:185`), `FacilitiesController` (tiêm tại `:20,28,35` nhưng chưa thấy lệnh gọi method) **[Xác nhận được]** | `FileUploadSecurityHelper`, `VietnamTimeHelper`, `ClosedXML`/`OpenXml`/`PdfPig` **[Xác nhận được]** | `LessonPlanImportService.cs:14-207` |
| `GoogleDriveStorageService` & `DriveLogUploadService` | `CloudMediaController`, `AbsenceController`, `AccountController`, `AnnouncementsController`, `FacilitiesController`, `LessonPlansController`, `LogsController`, `SacramentsController`, `StudentsController`, `ClassCurriculumService`, `LocalMediaMigrationService`, `DriveLogUploadBackgroundService` **[Xác nhận được]** | Google Drive v3 API, `ParishDbContext`, `IMemoryCache`, `VietnamTimeHelper`, `GoogleDriveOptions`, `DriveLogUploadOptions` **[Xác nhận được]** | `GoogleDriveStorageService.cs:28-1119`; `DriveLogUploadService.cs:17-320` |
| `LogRetentionBackgroundService` & `BackgroundExportJobService` & `StudentExcelService` & `LogsController` & `BackgroundJobsController` | `ServiceCollectionExtensions` (`:183,199-201`), Hangfire enqueue (`BackgroundJobsController:64-187`), `StudentsController`, `ClassesController`, `Views/Logs/Index.cshtml` **[Xác nhận được]** | `StudentExcelService.ExportClassListToStreamAsync` (`BackgroundExportJobService:68-74` ép kiểu tường minh!), `FileUploadSecurityHelper`, `VietnamTimeHelper`, `ParishDbContext`, `XLWorkbook` **[Xác nhận được]** | `ServiceCollectionExtensions.cs:183-201`; `BackgroundExportJobService.cs:36-329`; `StudentExcelService.cs:13-407`; `LogsController.cs` |
| `LookupController`, `AbsenceController`, `PublicRegistrationController`, `StudentRegistrationsController` | `Views/Lookup/*`, `Views/PublicRegistration/*` **[Xác nhận được]** | `ICacheService` (Redis lockout keys `lookup:fail:*`, `absence:fail:*`, `pubreg:fail:*`), `IAttendanceDisputeService`, `IImageOptimizationService`, `FileUploadSecurityHelper`, `VietnamTimeHelper`, `StaffSelfActionGuard` **[Xác nhận được]** | `LookupController.cs`; `AbsenceController.cs`; `PublicRegistrationController.cs` |

### 2.5. Hạ tầng Pipeline, Rate Limiter & Container

| Thành phần thay đổi | Callers / Consumers | Callees / Phụ thuộc | Bằng chứng & Dòng code |
|---|---|---|---|
| `Program.cs` (`/health`, startup warnings, `VietnamTimeZone`) & `ServiceCollectionExtensions.AddReverseProxyConfiguration` & `SecurityExtensions.AddAppRateLimiter` | Kestrel HTTP pipeline, Docker `HEALTHCHECK` (`Dockerfile:92`), Hangfire recurring jobs (`Program.cs:182-214`) **[Xác nhận được]**; Nginx reverse proxy & `parish-infra-config` **[Suy luận kiến trúc]** | `IConfiguration` (`ReverseProxy:*`, `ConnectionStrings:*`), `VietnamTimeHelper`, `System.Threading.RateLimiting` **[Xác nhận được]** | `Program.cs:42,46,108-131,178,216`; `ServiceCollectionExtensions.cs:63-153`; `SecurityExtensions.cs:17-161` |

---

## 3. Các Feature và Module Bị Ảnh hưởng (Có Bằng chứng)

| Module / Feature | Mức độ Ảnh hưởng | Tóm tắt Tác động (Xác nhận từ mã nguồn) |
|---|---|---|
| **Module `Attendance` & `FAST-QR` / `FEAT-P5-04`** | **HIGH** | Siết phân quyền từng action + `StaffSelfActionGuard`, idempotency offline `ATT:OFFLINE:IDEMP:*` + xử lý `23505`, khóa nguyên tử khi hoàn tác (`ClassFlushLock -> ClassSheetLock`) + grace `15s` + audit `QR_UNDO`, IndexedDB `v2`, sửa múi giờ VN và DOM XSS. |
| **Module `Grades` & `Sacraments` & `Promotion`** | **HIGH** | Bọc transaction + kiểm tra `IsGradeLocked` & `IsCurrent`, sửa null vs `0.0` trong `EvaluateGradeFormula`, sửa lỗi chia `0` (`NaN`) khi 0 buổi Chúa Nhật, khóa IDOR `batchId`/`grade` cho `BranchHead`, chặn tự chấm điểm `LinkedStudentId`, chống XSS. |
| **Module `Staff Quick Lookup` (`FEAT-P5-01`)** | **HIGH** | Bắt buộc `[Authorize(Policy = "RequireStaff")]`, lọc phạm vi truy cập lớp/khối trong `StaffStudentLookupService` trước khi đọc/trả cache `staff_lookup:student_360:{studentId}`, thoát inline `onclick` XSS trong `staff-student-lookup.js`. |
| **Module `CloudMedia` & `Log Backjob` (`FEAT-P5-08`)** | **HIGH** | Khóa IDOR `AVATAR` trong `CloudMediaController`, `CSP: sandbox` cho PDF & `attachment` cho `ABSENCE_DOC`, tách default secrets/enabled sang cấu hình môi trường (`Options` mặc định `false`), giới hạn `30 MB` / 64-stripe lock / `200 MB` log, dọn `tmp_logs` & `cloud_tmp` >2h, sửa đăng ký singleton trùng `LogRetentionBackgroundService`. |
| **Module `LessonPlans` (`FEAT-P5-07`) & `Facilities`** | **HIGH** | Đổi `IClassCurriculumService.GetVersionFileAsync` sang trả `Stream`, chặn IDOR `versionId` khác `documentId` & `ClassRoom == null`, kiểm tra magic bytes + giới hạn 15 MB, escape HTML modal lịch sử phiên bản & lịch phòng học. |
| **Module `UserAccount`, `Classes`, `Students` (`FEAT-P6-01`)** | **HIGH** | Thống nhất kiểm tra >= 18 tuổi (`AgeValidationHelper` chuẩn năm nhuận `29/02`) trên cả tạo/nâng cấp ở `UsersController` & `GovernanceController`, chặn self-edit ngày sinh/ngày tuyên hứa ở `AccountController`, chặn gán `Parent` làm GV, siết `GetAvailableTeachers`. |
| **Module `Public Lookup` (`/TraCuu`), `Absence`, `PublicRegistration`** | **HIGH** | Khóa brute-force phân tán bằng `ICacheService` (`IP` + `IP+Code`), gộp lỗi chung chống enumeration, thêm `WebsiteUrlHoneypot`, chặn trùng/quá 5 đơn nghỉ phép, re-encode ảnh JPG/PNG sang WebP, stream `PhysicalFile` kèm `CSP: sandbox`. |
| **Hạ tầng `Security`, `RateLimiter`, `Docker`, `BackgroundJobs`, `Logs`** | **HIGH** | Bật `GlobalLimiter` (`200 req/min`), cấu hình `ReverseProxy` qua `appsettings.json`, thêm `/health` + `HEALTHCHECK`, tối ưu RAM khi đọc log/xuất Excel toàn xứ (`PhysicalFile`, `FileStream`, SQL `GroupBy`), dọn tệp trùng ở root. |

---

## 4. Thông tin Thiếu & Cần Xác minh (`NEEDS_VERIFICATION`)

1. **Hạ tầng triển khai ngoài repository (`parish-infra-config`, `docker-compose.yml`, `nginx/default.conf`):**
   - Trong repo hiện không còn `docker-compose.yml` hay `nginx/default.conf` (đã tách ra `parish-infra-config`). Chưa có source trong workspace để xác nhận thực tế: cấu hình `SSL Mode=VerifyCA`/`VerifyFull` của PostgreSQL, cờ `--requirepass` trên container Redis, và dải mạng Docker thực tế của container Nginx so với mặc định `172.16.0.0/12` trong `appsettings.json`.
2. **Khoảng chênh `03-CODE-INDEX/` từ `2a3263d` đến `e183ccd`:**
   - Trong `KNOWLEDGE-BASELINE.json`, `lastIndexedCommit` vẫn là `2a3263d` (trong khi `lastFeatureSyncCommit` là `4589783`). Khi cập nhật `03-CODE-INDEX/*.jsonl`, cần xác minh/quét đầy đủ cả các symbol/route phát sinh từ Phase 6/7 (`2a3263d..4589783`) ngoài phần diff `e183ccd..HEAD` để tránh thiếu mục lục.
3. **Quan hệ thực thi runtime với `IGradePermissionService` & `ILessonPlanImportService`:**
   - Cả hai service đều được đăng ký DI trong `ServiceCollectionExtensions.cs` (`:185,188`) và đã được sửa trong `Fix-Sec-Inf`, nhưng qua tìm kiếm tĩnh trên `HEAD`: `IGradePermissionService` hiện không có caller nào ngoài DI; `ILessonPlanImportService` chỉ được inject vào `FacilitiesController` nhưng không có lời gọi method. Cần xác nhận giữ lại làm service dự phòng hay tích hợp về sau.
4. **Tính tương thích môi trường Production khi đổi mặc định `Options` (`997507f`):**
   - Vì `GoogleDriveOptions.Enabled` và `DriveLogUploadOptions.Enabled` đổi mặc định về `false`, và `ConnectionStrings:RedisConnection` đổi về `""`, các môi trường triển khai bắt buộc phải truyền biến môi trường tương ứng nếu muốn kích hoạt Google Drive và Redis.
5. **Kiểm thử tích hợp HTTP / DB thực tế (`PostgreSQL` + `Redis`):**
   - Trong repo mới có `41` unit tests (`tests/ManagerStudentCaltholic.Tests` dùng InMemory EF / logic thuần); chưa có integration test chạy với PostgreSQL (kiểm tra ngoại lệ `23505`) và Redis thật (`ATT:OFFLINE:IDEMP:*`, `ClassFlushLock`, `RateLimiter`).
