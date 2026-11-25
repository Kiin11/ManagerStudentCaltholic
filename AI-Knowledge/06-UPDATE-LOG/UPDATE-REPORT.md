# BÁO CÁO CẬP NHẬT TÀI LIỆU TRI THỨC HỆ THỐNG (UPDATE-REPORT)

> **Dự án:** `ManagerStudentCaltholic`  
> **Phạm vi cập nhật:** Commit `e183ccd` (`4589783` + docs `FEAT-P5-08`) ➔ `37a30f3` (bao gồm `PR #99` `Fix-Sec-Inf` tại `e147f88` và OAuth2 Refresh Token Google Drive tại `37a30f3` trên nhánh `main`)  
> **Thời gian thực hiện:** `2026-11-24T12:32:00+07:00`  
> **Vai trò thực hiện:** Senior Software Architect

---

## 1. Danh sách File Knowledge Đã Cập nhật (`Modified`)

Chỉ những tài liệu thực sự bị ảnh hưởng bởi **91** tệp thay đổi ngoài `AI-Knowledge/` (`9 Added`, `78 Modified`, `4 Deleted`, `0 Renamed`) mới được cập nhật; các tài liệu không bị ảnh hưởng (`02-DATABASE/*`, `AI-KNOWLEDGE-STRUCTURE.md`, `05-FEATURES/FEAT-P5-02-MOBILE-FIRST-UI.md`, `00-OVERVIEW/MODULE-INVENTORY.json`, `PROJECT-DEPENDENCY-DIAGRAM.md`, `Architecture Diagram.md`, `Request Flow Diagram.md`) được giữ nguyên:

| STT | Đường dẫn File Knowledge | Nội dung cập nhật chính |
|---|---|---|
| 1 | `AI-Knowledge/00-OVERVIEW/PROJECT.md` | Bổ sung 4 helper chuẩn (`VietnamTimeHelper`, `AgeValidationHelper`, `FileUploadSecurityHelper`, `StaffSelfActionGuard`), test project `tests/ManagerStudentCaltholic.Tests`, endpoint `GET /health`, chuẩn tên service `dotnet_app`, `GlobalLimiter` & `ReverseProxy`. |
| 2 | `AI-Knowledge/00-OVERVIEW/ARCHITECTURE.md` | Bổ sung 4 helper nền tảng vào Service Layer; cập nhật `Flow 1` (`ReverseProxy`, `GlobalLimiter`, `PhysicalFile`/`Stream`), `Flow 2` (kiểm tra quyền lớp/`StaffSelfActionGuard`, khóa nguyên tử `ClassFlushLock -> ClassSheetLock`, grace `15s`, `alreadySynced` fallback), `Flow 4` (`[Authorize(Policy = "RequireStaff")]` & `CanCallerAccessStudentAsync`). |
| 3 | `AI-Knowledge/00-OVERVIEW/CODING-RULES.md` | Bổ sung quy chuẩn bắt buộc: backend anti-IDOR + fail-closed `BranchHead` + `StaffSelfActionGuard`, `GlobalLimiter` & Redis lockout/honeypot, `FileUploadSecurityHelper` + `CSP: sandbox`, chống `@Html.Raw`/unescaped `innerHTML`, `VietnamTimeHelper`, `AgeValidationHelper`, và tối ưu RAM (`PhysicalFile`/`Stream`, SQL projection, transaction `CreateExecutionStrategy`). |
| 4 | `AI-Knowledge/00-OVERVIEW/TECH-STACK.md` | Bổ sung `ReverseProxy` config, `GlobalLimiter`, `/health` + Docker `HEALTHCHECK`, singleton `LogRetentionBackgroundService`, `xUnit 2.9.3` + `EF Core InMemory`, cảnh báo PostgreSQL TLS / Redis password non-Dev, `RedisConnection = ""` mặc định, và IndexedDB `DB_VERSION = 2`. |
| 5 | `AI-Knowledge/00-OVERVIEW/SOURCE-INVENTORY.md` | Nâng kiểm kê lên `209` files (bổ sung 4 helper + 5 file trong `tests/ManagerStudentCaltholic.Tests/`), ghi nhận `docker-compose.yml` & `nginx/default.conf` đã tách khỏi app repo sang `parish-infra-config`. |
| 6 | `AI-Knowledge/00-OVERVIEW/PROJECT-INVENTORY.json` | Bổ sung 9 entry máy đọc cho 4 helper mới và 5 file test xUnit. |
| 7 | `AI-Knowledge/01-MODULES/ATTENDANCE.md` | Cập nhật `Flow 1` (`ProcessQrCheckInViaRedisAsync(request, User)`, `StaffSelfActionGuard`, `QrScanUndoMetaKey`), `Flow 2` (`ExecuteWithPartitionsFlushLockAsync`, `ATT:OFFLINE:IDEMP:*`, retry `23505`, đánh giá lại phía Server, `skippedRecords`), `Flow 4` (`ClassFlushLock -> ClassSheetLock`, grace `15s`, người quét gốc, `QR_UNDO`), và phân quyền + chống enumeration/spam cho `AbsenceController`/`AttendanceDisputeService`. |
| 8 | `AI-Knowledge/03-CODE-INDEX/FILE-INDEX.jsonl` | Thêm 9 dòng cho 4 helper (`Services/*.cs`) và 5 tệp trong `tests/ManagerStudentCaltholic.Tests/`. |
| 9 | `AI-Knowledge/03-CODE-INDEX/SYMBOL-INDEX.jsonl` | Cập nhật chữ ký `Stream` cho `IClassCurriculumService.GetVersionFileAsync` & `ClassCurriculumService.GetVersionFileAsync`, line range `LogRetentionBackgroundService` & `SubmitRequestWithFile`, và bổ sung 25 symbol mới (4 helper + methods, overloads mới trên `IRedisAttendanceService`/`IStaffStudentLookupService`/`IGradeCalculationService`, `SecurityExtensions.GetClientIpKey`, `ServiceCollectionExtensions.AddReverseProxyConfiguration`, `StudentExcelService.ExportClassListToStreamAsync`). |
| 10 | `AI-Knowledge/03-CODE-INDEX/ROUTE-INDEX.jsonl` | Cập nhật `startLine` thực tế cho 11 route bị dịch dòng (`HomeController`, `LessonPlansController`, `AttendanceController.UndoScanCheckIn`) và bổ sung `GET /health` (`Program.cs:216`). |
| 11 | `AI-Knowledge/03-CODE-INDEX/DEPENDENCY-INDEX.jsonl` | Cập nhật `sourceLine` cho 22 cạnh hiện có và bổ sung 17 cạnh `INJECTS` / `CALLS` mới (`StaffSelfActionGuard`, `FileUploadSecurityHelper`, `AgeValidationHelper`, `VietnamTimeHelper`, `CanCallerAccessStudentAsync`, `ExecuteWithPartitionsFlushLockAsync`, `FinalizePartitionsAfterExternalSyncAsync`, `PublicRegistrationController` -> `ICacheService`/`IImageOptimizationService`, `BackgroundExportJobService` -> `ExportClassListToStreamAsync`). |
| 12 | `AI-Knowledge/04-INTEGRATIONS/INTEGRATION-OVERVIEW.md` | Cập nhật cảnh báo PostgreSQL SSL, Redis `""` mặc định & khóa chống brute-force/idempotency, chế độ xác thực kép OAuth2 Refresh Token / Service Account (`37a30f3`), 64-stripe `FolderLockStripes` + `MaxUploadBytes = 30 MB`, `VietnamTimeHelper` cho Hangfire, `LogRetentionBackgroundService` singleton dọn `tmp_logs`/`cloud_tmp` >2h, và IndexedDB `DB_VERSION = 2`. |
| 13 | `AI-Knowledge/04-INTEGRATIONS/EXTERNAL-APIS.md` | Bổ sung chế độ ưu tiên OAuth2 Refresh Token (`GoogleAuthorizationCodeFlow` + `UserCredential` ở `37a30f3`), 64-stripe `FolderLockStripes` (`Files.List`) và giới hạn spool `MaxUploadBytes = 30 MB` (`Files.Create`). |
| 14 | `AI-Knowledge/05-FEATURES/FAST-QR-CHECKIN-AND-OFFLINE-SYNC.md` | Cập nhật bảo mật `ScanCheckIn` (`403` + `StaffSelfActionGuard`), `QrScanUndoMetaKey`, luồng `SyncOfflineQueue` (`ExecuteWithPartitionsFlushLockAsync`, `ATT:OFFLINE:IDEMP:*`, retry `23505`, đánh giá Server theo giờ VN, `skippedRecords`), `offline-attendance.js` `v2`, và luồng Undo nguyên tử (`15s` grace, fallback server). |
| 15 | `AI-Knowledge/05-FEATURES/FEAT-P5-01-STAFF-QUICK-LOOKUP.md` | Cập nhật `[Authorize(Policy = "RequireStaff")]`, overload nhận `ClaimsPrincipal`, `CanCallerAccessStudentAsync` (`403` trước cache Redis), và xử lý chống XSS trong `staff-student-lookup.js`. |
| 16 | `AI-Knowledge/05-FEATURES/FEAT-P5-04-QR-SCANNER-UPGRADE.md` | Cập nhật sơ đồ Undo 5s (tắt khi `isDuplicate === true`, `403`, khóa `ClassFlushLock -> ClassSheetLock`, grace `15s`, audit `QR_UNDO`, fallback server undo khi offline đã đồng bộ, DOM `textContent`). |
| 17 | `AI-Knowledge/05-FEATURES/FEAT-P5-07-CLASS-CURRICULUM-MANAGEMENT.md` | Cập nhật `GetVersionFileAsync` trả `Stream`, giới hạn 15 MB + magic bytes, kiểm tra `ClassCurriculumDocumentId == documentId`, fail-closed khi `ClassRoom == null`, và chống XSS trong modal phiên bản. |
| 18 | `AI-Knowledge/05-FEATURES/FEAT-P5-08-GOOGLE-DRIVE-STORAGE-AND-LOG-BACKJOB.md` | Cập nhật `GoogleDriveOptions` (`OAuthClientId`, `OAuthClientSecret`, `OAuthRefreshToken` ở `37a30f3`), xác thực kép OAuth2/Service Account, 64-stripe `FolderLockStripes`, `MaxUploadBytes = 30 MB`, `MaxLogFileBytes = 200 MB` + `RetryDelaySeconds` + xóa bù log verified, `LogsController` đọc tail `5 MB`/`5.000` dòng, ma trận phân quyền `AVATAR` trong `CloudMediaController`, `CSP: sandbox` PDF & `attachment` `ABSENCE_DOC`. |
| 19 | `AI-Knowledge/05-FEATURES/FEAT-P6-01-PROBATIONARY-TEACHER-AND-AFTERNOON-SESSION.md` | Cập nhật phạm vi Dự Bị ca sáng + `StaffSelfActionGuard` trên tất cả module nghiệp vụ, chặn gán `Parent`, và chuẩn hóa kiểm tra `>= 18` tuổi qua `AgeValidationHelper` (năm nhuận `29/02`) trên `StudentsController`, `AccountController`, `UsersController`, `GovernanceController`. |
| 20 | `AI-Knowledge/06-UPDATE-LOG/KNOWLEDGE-BASELINE.json` | Nâng `lastFeatureSync` lên `"Fix-Sec-Inf (PR #99)"` và `lastFeatureSyncCommit` lên `"e147f88"`; giữ ghi chú rõ về `lastIndexedCommit = "2a3263d"` cho đợt quét bù Phase 6/7. |
| 21 | `AI-Knowledge/06-UPDATE-LOG/UPDATE-LOG.md` | Bổ sung biên bản đồng bộ cho `e147f88` (`PR #99` + `997507f`). |
| 22 | `AI-Knowledge/06-UPDATE-LOG/PENDING-UPDATES.md` | Cập nhật trạng thái đồng bộ và danh sách tồn đọng `NEEDS_VERIFICATION`. |
| 23 | `AI-Knowledge/06-UPDATE-LOG/UPDATE-REPORT.md` | Báo cáo tổng kết đợt đồng bộ này. |

*(Lưu ý: 4 tệp phân tích `CHANGE-ANALYSIS.md`, `IMPACT-ANALYSIS.md`, `KNOWLEDGE-UPDATE-PLAN.md`, `AFFECTED-FILES.json` trong `AI-Knowledge/06-UPDATE-LOG/` đã được tạo ở bước phân tích trước đó và giữ nguyên.)*

---

## 2. Danh sách File Knowledge Tạo Mới (`NEW FILE`)

- **Không có (`0` file tạo mới trong bước cập nhật này).** Các tài liệu hiện hữu đã bao phủ toàn bộ module/feature bị tác động. *(Ghi chú trong `KNOWLEDGE-UPDATE-PLAN.md`: có thể cân nhắc bổ sung sau các tài liệu riêng cho nhóm Học vụ/Bí tích/BackgroundExport nếu cần).*

---

## 3. Danh sách File Knowledge Đã Xóa (`DELETE FILE`)

- **Không có (`0` file trong `AI-Knowledge/` bị xóa).** *(Ngoài `AI-Knowledge/`, ở PR `#99` đã xóa 3 bản sao lỗi thời ở root là `KNOWLEDGE-ISSUES.jsonl`, `KNOWLEDGE-VALIDATION-REPORT.md`, `REQUIRED-CORRECTIONS.md` và tệp cũ `script/fix-db-1.txt`).*

---

## 4. Các Module và Feature Bị Ảnh Hưởng

| Module / Feature | Mức độ ảnh hưởng | Tóm tắt thay đổi |
|---|---|---|
| **Module `Attendance` / `FAST-QR` / `FEAT-P5-04`** | **HIGH** | Phân quyền chi tiết từng action + `StaffSelfActionGuard`, idempotency offline `ATT:OFFLINE:IDEMP:*` + retry `23505`, khóa nguyên tử `ClassFlushLock -> ClassSheetLock` + grace `15s` + `QR_UNDO`, IndexedDB `v2`, `VietnamTimeHelper`, chống XSS. |
| **Module `Academic` / `Grades` / `Sacraments` / `Promotion`** | **HIGH** | Bọc `ExecutionStrategy` + transaction kiểm tra `IsGradeLocked` & `AcademicYear.IsCurrent`, sửa `EvaluateGradeFormula` (`null != 0.0`), sửa lỗi chia `0` (`NaN`) khi 0 buổi Chúa Nhật, khóa IDOR `batchId`/`grade`, chặn tự chấm điểm `LinkedStudentId`, chống XSS. |
| **Module `StaffLookup` (`FEAT-P5-01`)** | **HIGH** | Bắt buộc `[Authorize(Policy = "RequireStaff")]`, lọc phạm vi lớp/khối trước khi đọc/trả cache `staff_lookup:student_360:{studentId}`, thoát inline `onclick` XSS. |
| **Module `CloudStorage` / `Logging` (`FEAT-P5-08`)** | **HIGH** | Khóa IDOR `AVATAR` trong `CloudMediaController`, `CSP: sandbox` PDF + `attachment` `ABSENCE_DOC`, tách default secrets (`Enabled = false`), giới hạn `30 MB`/`200 MB` + 64-stripe lock, tail read log `5 MB`/`5.000` dòng, dọn temp `> 2h` & bỏ đăng ký DI trùng. |
| **Module `LessonPlans` (`FEAT-P5-07`) & `Facilities`** | **HIGH** | Đổi `GetVersionFileAsync` sang `Stream`, khóa `versionId` khác `documentId` & `ClassRoom == null`, magic bytes + 15 MB, escape HTML modal & lịch phòng. |
| **Module `UserAccount` / `Classes` / `Students` (`FEAT-P6-01`)** | **HIGH** | Thống nhất kiểm tra `>= 18` tuổi (`AgeValidationHelper` chuẩn `29/02`) trên cả `UsersController` & `GovernanceController`, chặn non-Leadership tự sửa DOB/ngày tuyên hứa, đồng bộ `LinkedUser`, chặn gán `Parent`, siết `GetAvailableTeachers`. |
| **Module `Lookup` (`/TraCuu`), `Absence`, `PublicRegistration`** | **HIGH** | Redis lockout (`IP` & `IP+Code`), gộp lỗi chung, `WebsiteUrlHoneypot`, chặn trùng/quá 5 đơn nghỉ phép, WebP re-encode, `PhysicalFile` + `CSP: sandbox`. |
| **Hạ tầng `Security`, `RateLimiter`, `Docker`, `BackgroundJobs`** | **HIGH** | Bật `GlobalLimiter` (`200 req/min/IP`), cấu hình `ReverseProxy` qua `appsettings.json`, thêm `GET /health` + `HEALTHCHECK`, `PhysicalFile` & SQL `GroupBy` cho export Excel, xóa tệp trùng root. |

---

## 5. Các Dependency Thay Đổi (Đã Xác Minh Từ Source Code)

1. **4 Helper tĩnh mới được gọi chéo toàn hệ thống (`Services/`):**
   - `VietnamTimeHelper` ➔ gọi bởi `Program.cs`, 12 Controllers, 12 Services, 7 Razor Views, 2 ViewModels và `VietnamTimeHelperTests`.
   - `AgeValidationHelper` ➔ gọi bởi `StudentViewModel.CalculateAgeAtDate`, `AccountController`, `GovernanceController`, `StudentsController`, `UsersController`, `ProfileHistoryService`, `AgeValidationTests`.
   - `FileUploadSecurityHelper` ➔ gọi bởi `AbsenceController`, `BackgroundJobsController`, `LessonPlansController`, `PublicRegistrationController`, `StudentRegistrationsController`, `StudentsController`, `UsersController`, `ClassCurriculumService`, `LessonPlanImportService`, `StudentExcelService`, `SecurityHelpersTests`.
   - `StaffSelfActionGuard` ➔ gọi bởi `AbsenceController`, `AttendanceController`, `GradesController`, `AttendanceDisputeService`, `ClassEvaluationService`, `MassCampaignService`, `RedisAttendanceService`, `SecurityHelpersTests`.
2. **Thay đổi đăng ký DI & Pipeline (`ServiceCollectionExtensions.cs`, `SecurityExtensions.cs`, `Program.cs`):**
   - `LogRetentionBackgroundService` hợp nhất thành **1 singleton duy nhất** (`AddSingleton<LogRetentionBackgroundService>()` + `ILogMaintenanceJobService` & `AddHostedService` cùng trỏ về instance này).
   - `Program.cs` gọi `AddReverseProxyConfiguration(builder.Configuration)` và mở route `GET /health`.
3. **Thay đổi chữ ký & quan hệ gọi giữa Controller/Service:**
   - `LessonPlansController.DownloadCurriculumVersion` gọi `IClassCurriculumService.GetVersionFileAsync` (đổi sang trả `Stream`).
   - `HomeController` gọi các overload mới có `ClaimsPrincipal` và `CanCallerAccessStudentAsync` trên `IStaffStudentLookupService`.
   - `AttendanceController` gọi các overload có `ClaimsPrincipal?` và `ExecuteWithPartitionsFlushLockAsync` / `FinalizePartitionsAfterExternalSyncAsync` trên `IRedisAttendanceService`.
   - `PublicRegistrationController` inject thêm `ICacheService` và `IImageOptimizationService`.
   - `BackgroundExportJobService.GenerateClassExcelReportJobAsync` ép kiểu sang `StudentExcelService` để gọi `ExportClassListToStreamAsync`.

---

## 6. Những Điểm Chưa Xác Minh (`NEEDS_VERIFICATION`)

1. **Cấu hình Hạ tầng Ngoài Repository (`parish-infra-config`):**
   - Không có `docker-compose.yml` hay `nginx/default.conf` trong repo hiện tại để xác minh `SSL Mode=VerifyCA`/`VerifyFull`, Redis `--requirepass`, và dải mạng Docker thực tế của `parish_nginx` khớp `ReverseProxy:KnownNetworks`.
2. **Khoảng chênh `03-CODE-INDEX/` giữa `2a3263d` và `4589783`:**
   - `lastIndexedCommit` vẫn giữ mốc `2a3263d` vì chỉ mục code giữa `2a3263d..4589783` (Phase 6/7) mới được bổ sung cục bộ cho `FEAT-P5-08` và `Fix-Sec-Inf`, chưa chạy quét tổng thể lại toàn bộ repository.
3. **Hai Service chưa có Caller thực thi trực tiếp:**
   - `IGradePermissionService` (đăng ký DI tại `ServiceCollectionExtensions.cs:188`, không có caller nào gọi) và `ILessonPlanImportService` (inject tại `FacilitiesController.cs:20`, chưa thấy gọi method).

---

## 7. Những Nội Dung Cần Developer Kiểm Tra Thủ Công

1. **Kiểm tra cấu hình Production trong `parish-infra-config`:**
   - Đảm bảo truyền đủ biến môi trường cho `ConnectionStrings__DefaultConnection` (`SSL Mode=VerifyCA`/`VerifyFull`), `ConnectionStrings__RedisConnection` (kèm `password=...`, vì mặc định `appsettings.json` đã về `""`), `GoogleDrive__Enabled=true` / `DriveLogUpload__Enabled=true` (vì mặc định Options đã về `false`), và `ReverseProxy__KnownNetworks__0` khớp đúng subnet của Nginx container.
2. **Kiểm thử tích hợp trên PostgreSQL & Redis thật (Checklist Prod):**
   - Kiểm tra lỗi `403` cho IDOR / tự thao tác `LinkedStudentId`, `429` cho Rate Limiter & lockout công khai, hoàn tác 5 giây (`QR_UNDO`), đồng bộ QR Offline qua PostgreSQL thật (`23505`), và chạy `dotnet test tests/ManagerStudentCaltholic.Tests` trong CI.
3. **Quản trị Repository & Lịch sử Git (`SEC-INF-01`):**
   - Chạy `gitleaks`, xoay vòng các secret cũ trước commit `44e34841`, viết lại lịch sử Git bằng `git filter-repo` hoặc chuyển repo sang Private.
