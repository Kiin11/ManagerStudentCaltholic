# HƯỚNG DẪN KHẮC PHỤC CÁC VẤN ĐỀ KHO TRI THỨC (REQUIRED CORRECTIONS)

> **Phạm vi kiểm định:** Đối chiếu `AI-Knowledge/` sau đợt cập nhật ngày `2026-11-24` với mã nguồn thực tế trên nhánh `main` tại commit `37a30f3` (bao gồm `PR #99` `Fix-Sec-Inf` tại `e147f88` và OAuth2 Refresh Token Drive tại `37a30f3`).  
> **Tổng số vấn đề cần xử lý:** `10` (`3 HIGH`, `5 MEDIUM`, `2 LOW`).  
> **Lưu ý:** Toàn bộ `9` vấn đề của đợt kiểm định trước (`ISS-AUD-001`..`ISS-AUD-009`) đã được xác nhận khắc phục xong trước đợt kiểm định này.

---

## Priority 1 — Mức độ Nghiêm trọng Cao (`HIGH`)

### 1. `[ISS-AUD-010]` Đồng bộ lại sơ đồ luồng Điểm danh QR (`Write-Behind` thay cho `Write-Through`)
- **Tài liệu cần sửa:**
  1. `AI-Knowledge/00-OVERVIEW/Request Flow Diagram.md` (`lines 22-36`)
  2. `AI-Knowledge/05-FEATURES/FEAT-P5-04-QR-SCANNER-UPGRADE.md` (`line 46`)
- **Hiện trạng sai lệch:**
  - `Request Flow Diagram.md:23-24` vẫn vẽ `Ctrl->>R_Svc: Write-Through SyncDirtyAttendancesToDatabaseAsync()` và `Batch Upsert ngay trong Request xuống PostgreSQL`.
  - `FEAT-P5-04-QR-SCANNER-UPGRADE.md:46` vẫn ghi `──> Cập nhật lưới điểm danh Redis (IsDirty = true) & Write-Through DB`.
- **Giá trị đúng cần cập nhật (theo `AttendanceController.cs:307`, `AttendanceWriteBehindBackgroundService.cs:10-97`, `ATTENDANCE.md`, `FAST-QR-CHECKIN-AND-OFFLINE-SYNC.md`):**
  - Luồng `ScanCheckIn` online dùng **Redis-First & Write-Behind**: `RedisAttendanceService` cập nhật Redis (`IsDirty = true`) và gọi `IAttendanceFlushSignal.NotifyDirty(...)` để `AttendanceWriteBehindBackgroundService` gom nhóm Batch Upsert xuống PostgreSQL theo cửa sổ `~5 giây` hoặc ngưỡng `25` lượt quét (cùng flush khi tắt ứng dụng `StopAsync` và Hangfire job `*/2 * * * *` dự phòng). Chỉ thực hiện ghi thẳng 1 dòng xuống PostgreSQL khi `!IsRedisAvailable`. (Cơ chế Write-Through chỉ áp dụng cho `SaveBatch` và `QuickUpdateSingle`.)

### 2. `[ISS-AUD-012]` Chỉnh dòng/chữ ký trong `SYMBOL-INDEX.jsonl` và bổ sung `11` symbol đang thiếu khỏi `DEPENDENCY-INDEX.jsonl`
- **Tài liệu cần sửa:**
  - `AI-Knowledge/03-CODE-INDEX/SYMBOL-INDEX.jsonl`
- **Hiện trạng sai lệch:**
  - Có **`56 / 264`** bản ghi bị lệch `startLine` / chữ ký across `20` tệp (`AbsenceController.cs`, `AccountController.cs`, `AnnouncementsController.cs`, `AttendanceController.cs`, `AttendanceViewModels.cs`, `ClassCurriculumService.cs`, `ClassesController.cs`, `CurriculumDtos.cs`, `FacilitiesController.cs`, `GradesController.cs`, `HomeController.cs`, `IRedisAttendanceService.cs`, `LessonPlansController.cs`, `LogsController.cs`, `PublicRegistrationController.cs`, `RedisAttendanceService.cs`, `SacramentsController.cs`, `StaffStudentLookupService.cs`, `StudentsController.cs`, `UsersController.cs`), điển hình như `IRedisAttendanceService.ProcessQrCheckInViaRedisAsync` (`:31`), `UndoScanCheckInAsync` (`:36`), `ClassCurriculumService.SaveCurriculumAsync` (`:298`), `ExportCurriculumToExcelAsync` (`:546`), `AccountController.Profile` (`:399`) / `UpdateProfile` (`:419`).
  - **`11` symbolKey được tham chiếu trong `DEPENDENCY-INDEX.jsonl` bị thiếu khai báo trong `SYMBOL-INDEX.jsonl`:**
    1. `ManagerStudentCaltholic.Controllers.AbsenceController.Review`
    2. `ManagerStudentCaltholic.Controllers.BackgroundJobsController.DownloadExport`
    3. `ManagerStudentCaltholic.Controllers.GovernanceController.ChangeExecutiveRole`
    4. `ManagerStudentCaltholic.Interface.Services.IDriveLogUploadService.RunUploadCycleAsync`
    5. `ManagerStudentCaltholic.Models.ViewModels.StudentViewModel.CalculateAgeAtDate`
    6. `ManagerStudentCaltholic.Services.AttendanceDisputeService.ReviewDisputeByTeacherAsync`
    7. `ManagerStudentCaltholic.Services.BackgroundExportJobService.GenerateClassExcelReportJobAsync`
    8. `ManagerStudentCaltholic.Services.ClassEvaluationService.SubmitClassEvaluationAsync`
    9. `ManagerStudentCaltholic.Services.LogRetentionBackgroundService.CleanUpOldLogsAndTempExports`
    10. `ManagerStudentCaltholic.Services.MassCampaignService.UpdateSingleCellAsync`
    11. `ManagerStudentCaltholic.Services.VietnamTimeHelper.Today`
- **Hành động khắc phục:**
  - Chạy quét lại toàn bộ `SYMBOL-INDEX.jsonl` theo `HEAD` hiện hành (`37a30f3`), cập nhật chính xác `startLine`/`endLine`/`signature`, và bổ sung đủ `11` symbol bị thiếu (cũng như các symbol của `AttendanceWriteBehindBackgroundService`, `ProfileHistoryService`, `IProfileHistoryService`), sau đó nâng `lastIndexedCommit` trong `KNOWLEDGE-BASELINE.json` từ `2a3263d` lên `37a30f3`.

### 3. `[ISS-AUD-013]` Cập nhật `startLine` cho `43` route bị lệch và bổ sung route thiếu trong `ROUTE-INDEX.jsonl`
- **Tài liệu cần sửa:**
  - `AI-Knowledge/03-CODE-INDEX/ROUTE-INDEX.jsonl`
- **Hiện trạng sai lệch:**
  - **`43 / 69`** route bị lệch `startLine` across `10` Controller (`AbsenceController.cs`, `AccountController.cs`, `AttendanceController.cs`, `ClassesController.cs`, `LessonPlansController.cs`, `LogsController.cs`, `LookupController.cs`, `PublicRegistrationController.cs`, `SacramentsController.cs`, `StudentsController.cs`).
  - Route đang hoạt động `POST /Attendance/FlushRedisToDatabase` (`Controllers/AttendanceController.cs:2475`) chưa được thêm vào `ROUTE-INDEX.jsonl`.
- **Hành động khắc phục:**
  - Đồng bộ lại toàn bộ `startLine` trong `ROUTE-INDEX.jsonl` theo AST/line thực tế trên `HEAD` (`37a30f3`) và bổ sung `POST /Attendance/FlushRedisToDatabase` cùng các route mới của Phase 6/7 nếu cần.

---

## Priority 2 — Mức độ Trung bình (`MEDIUM`)

### 4. `[ISS-AUD-011]` Cập nhật cơ chế xác thực kép OAuth2 Refresh Token / Service Account vào `TECH-STACK.md` và phần tóm tắt `FEAT-P5-08`
- **Tài liệu cần sửa:**
  1. `AI-Knowledge/00-OVERVIEW/TECH-STACK.md` (`line 22`)
  2. `AI-Knowledge/05-FEATURES/FEAT-P5-08-GOOGLE-DRIVE-STORAGE-AND-LOG-BACKJOB.md` (`line 15`)
- **Hành động khắc phục:**
  - Tại `TECH-STACK.md:22`: Bổ sung `OAuth2 Refresh Token` (`GoogleAuthorizationCodeFlow` + `UserCredential`) làm chế độ ưu tiên trước `ServiceAccountCredential` đúng theo `GoogleDriveStorageService.cs:766-836` (`37a30f3`).
  - Tại `FEAT-P5-08:15`: Sửa `"Tải tệp bằng Service Account qua Resumable Upload"` thành `"Tải tệp qua OAuth2 Refresh Token (ưu tiên) hoặc Service Account JSON với Resumable Upload"` để nhất quán với mục 3.2 cùng tài liệu.

### 5. `[ISS-AUD-014]` Cập nhật `17` `sourceLine` và bổ sung cạnh `DEPENDENCY-INDEX.jsonl`
- **Tài liệu cần sửa:**
  - `AI-Knowledge/03-CODE-INDEX/DEPENDENCY-INDEX.jsonl`
- **Hành động khắc phục:**
  - Cập nhật lại `sourceLine` cho `17` cạnh bị trôi dòng trên `AbsenceController.cs`, `AttendanceController.cs`, `UsersController.cs`, `RedisAttendanceService.cs`, `SacramentsController.cs`, `GradesController.cs`.
  - Bổ sung các cạnh phụ thuộc cho `AttendanceWriteBehindBackgroundService` (`IAttendanceFlushSignal`, `IRedisAttendanceService`) và `ProfileHistoryService` (`IProfileHistoryService`).

### 6. `[ISS-AUD-015]` Đồng bộ danh mục kiểm kê tệp nguồn (`SOURCE-INVENTORY.md`, `PROJECT-INVENTORY.json`, `FILE-INDEX.jsonl`)
- **Tài liệu cần sửa:**
  1. `AI-Knowledge/00-OVERVIEW/SOURCE-INVENTORY.md`
  2. `AI-Knowledge/00-OVERVIEW/PROJECT-INVENTORY.json`
  3. `AI-Knowledge/03-CODE-INDEX/FILE-INDEX.jsonl`
- **Hành động khắc phục:**
  - Bổ sung **`16` tệp C# lõi đang hoạt động** còn thiếu trong `SOURCE-INVENTORY.md` & `PROJECT-INVENTORY.json`: `Controllers/CloudMediaController.cs`, `Services/AttendanceWriteBehindBackgroundService.cs`, `Services/DriveLogUploadBackgroundService.cs`, `Services/DriveLogUploadService.cs`, `Services/GoogleDriveStorageService.cs`, `Services/ImageOptimizationService.cs`, `Services/LocalMediaMigrationService.cs`, `Services/ProfileHistoryService.cs`, `Interface/Services/IDriveLogUploadService.cs`, `Interface/Services/IGoogleDriveStorageService.cs`, `Interface/Services/IImageOptimizationService.cs`, `Interface/Services/ILocalMediaMigrationService.cs`, `Interface/Services/IProfileHistoryService.cs`, `Models/Entities/CloudMediaFile.cs`, `Models/Entities/GradeLevel.cs`, `Models/DTOs/CloudMediaDtos.cs`.
  - Bổ sung **`3` tệp Service/Interface** còn thiếu trong `FILE-INDEX.jsonl`: `Services/AttendanceWriteBehindBackgroundService.cs`, `Services/ProfileHistoryService.cs`, `Interface/Services/IProfileHistoryService.cs`.

### 7. `[ISS-AUD-017]` Xác minh thực thi DDL thực tế trên PostgreSQL (`NEEDS_VERIFICATION`)
- **Tài liệu liên quan:** `AI-Knowledge/02-DATABASE/DATABASE-OVERVIEW.md` (`lines 5, 63-64`)
- **Hành động cần Developer thực hiện:**
  - Do `ManagerStudentCaltholic/Migrations/` có `0` file migration trong Git, cần chạy kiểm tra trực tiếp trên PostgreSQL đích xác nhận đã áp dụng `script/20261123_CloudMediaFiles_GoogleDrive_FeatP5_08.sql` và `script/20261120_Users_Classes_Students_FeaturePermissionRules_Phase6.sql`, sau đó gỡ ghi chú `NEEDS_VERIFICATION` khi có bằng chứng xác thực từ DBA/môi trường chạy thật.

### 8. `[ISS-AUD-018]` Xác minh cấu hình hạ tầng ngoài Repository (`parish-infra-config`) (`NEEDS_VERIFICATION`)
- **Tài liệu liên quan:** `AI-Knowledge/00-OVERVIEW/PROJECT.md` (`line 33`), `AI-Knowledge/00-OVERVIEW/TECH-STACK.md`, `AI-Knowledge/04-INTEGRATIONS/INTEGRATION-OVERVIEW.md`, `EXTERNAL-APIS.md`
- **Hành động cần Developer/DevOps thực hiện:**
  - Đối chiếu repository hạ tầng `parish-infra-config` (`docker-compose.yml`, `nginx/default.conf`, `.env`) để xác nhận: (1) PostgreSQL connection string bật `SSL Mode=VerifyCA`/`VerifyFull`, (2) Redis cấu hình `--requirepass` & `maxmemory-policy`, (3) biến `GoogleDrive__OAuth*` hoặc secret mount `:ro`, và (4) CIDR mạng Docker của `parish_nginx` khớp `ReverseProxy:KnownNetworks`.

---

## Priority 3 — Mức độ Thấp (`LOW`)

### 9. `[ISS-AUD-016]` Bổ sung mô tả cụm Cloud Storage vào `PROJECT.md` và dẫn chiếu tab Tài liệu giáo án tại `FEAT-P5-07`
- **Tài liệu cần sửa:**
  1. `AI-Knowledge/00-OVERVIEW/PROJECT.md` (`lines 12-30`)
  2. `AI-Knowledge/05-FEATURES/FEAT-P5-07-CLASS-CURRICULUM-MANAGEMENT.md` (`lines 137-147`)
- **Hành động khắc phục:**
  - Bổ sung tham chiếu `CloudMediaController.cs`, `GoogleDriveStorageService.cs`, `DriveLogUploadService.cs` vào mục 3 của `PROJECT.md`.
  - Tại bảng mục 5 của `FEAT-P5-07`, thêm ghi chú dẫn chiếu 3 endpoint tài liệu giáo án bổ trợ trên cùng `LessonPlansController` (`UploadDocument`, `DownloadDocument`, `DeleteDocument`) sang đặc tả `FEAT-P5-08`.

### 10. `[ISS-AUD-019]` Xác nhận hai Service DI chưa có caller (`IGradePermissionService`, `ILessonPlanImportService`)
- **Tài liệu liên quan:** `AI-Knowledge/03-CODE-INDEX/DEPENDENCY-INDEX.jsonl`, `AI-Knowledge/06-UPDATE-LOG/UPDATE-REPORT.md` (`lines 96-97`)
- **Hành động cần Developer thực hiện:**
  - Kiểm tra xác nhận `IGradePermissionService` (đăng ký tại `ServiceCollectionExtensions.cs:188`) và `ILessonPlanImportService` (inject tại `FacilitiesController.cs:20`) sẽ được nối vào luồng xử lý hay gỡ bỏ để cập nhật dứt điểm trạng thái trong `DEPENDENCY-INDEX.jsonl`.
