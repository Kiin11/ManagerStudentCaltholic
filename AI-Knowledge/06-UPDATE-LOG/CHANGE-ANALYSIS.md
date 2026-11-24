# BÁO CÁO PHÂN TÍCH THAY ĐỔI MÃ NGUỒN (CHANGE ANALYSIS REPORT)

- **Dự án:** `ManagerStudentCaltholic`
- **Thời gian phân tích:** `2026-11-24T12:20:00+07:00`
- **Vai trò:** Senior Software Architect & Codebase Analyst
- **Baseline đối chiếu từ `AI-Knowledge/06-UPDATE-LOG/KNOWLEDGE-BASELINE.json`:**
  - `lastFeatureSync`: `FEAT-P5-08` (`lastFeatureSyncCommit = 4589783`, kèm commit tài liệu `e183ccd` ngày `2026-11-23`)
  - `lastIndexedCommit`: `2a3263d` *(ghi chú trong baseline: chỉ mục đầy đủ `03-CODE-INDEX/` vẫn giữ mốc `2a3263d` chờ bắt kịp các commit Phase 6/7 giữa `2a3263d..4589783`)*
- **Commit mục tiêu (`HEAD` trên nhánh `main`):** `e147f88` (`Merge pull request #99 from Kiin11/Fix-Sec-Inf`), bao gồm:
  - `997507f` (`chore(config): decouple Google Drive account defaults into parish-infra-config`)
  - `7386ebb` (`Merge pull request #96 from Kiin11/Phase-F5.8`)
  - `9eeafed` ➔ `dbd1571` (nhánh `Fix-Sec-Inf` vá bảo mật, đồng thời, múi giờ, hiệu năng và bộ unit test xUnit)
- **Phạm vi diff chính (`e183ccd..HEAD`):** **93 tệp** trong git diff, gồm **91 tệp** mã nguồn/test/cấu hình/tài liệu gốc (`9 Added`, `78 Modified`, `4 Deleted`, `0 Renamed`) và **2 tệp** trong `AI-Knowledge/` đã chỉnh sửa ở commit `997507f` (`04-INTEGRATIONS/EXTERNAL-APIS.md`, `05-FEATURES/FEAT-P5-08-GOOGLE-DRIVE-STORAGE-AND-LOG-BACKJOB.md`).
- **Khoảng chênh nền `2a3263d..e183ccd`:** Còn các tệp từ Phase 6 (`FEAT-P6-01`), `FEAT-P5-08` và dọn `.env`/`appsettings.Development.json` đã cập nhật vào DB/Feature docs trước đó nhưng chưa tái quét toàn bộ `03-CODE-INDEX/`.

---

## 1. Phân loại các File Thay đổi (`e183ccd..HEAD`)

### 1.1. Added (`9` tệp mới tạo)

| STT | Đường dẫn | Tầng (Layer) | Số dòng (+/-) |
|---|---|---|---|
| 1 | `ManagerStudentCaltholic/Services/AgeValidationHelper.cs` | Service / Helper | +38 / -0 |
| 2 | `ManagerStudentCaltholic/Services/FileUploadSecurityHelper.cs` | Service / Security Helper | +187 / -0 |
| 3 | `ManagerStudentCaltholic/Services/StaffSelfActionGuard.cs` | Service / Security Guard | +31 / -0 |
| 4 | `ManagerStudentCaltholic/Services/VietnamTimeHelper.cs` | Service / Time Helper | +73 / -0 |
| 5 | `tests/ManagerStudentCaltholic.Tests/ManagerStudentCaltholic.Tests.csproj` | Test Project | +34 / -0 |
| 6 | `tests/ManagerStudentCaltholic.Tests/AgeValidationTests.cs` | Unit Test | +41 / -0 |
| 7 | `tests/ManagerStudentCaltholic.Tests/GradeAndSacramentLogicTests.cs` | Unit Test | +321 / -0 |
| 8 | `tests/ManagerStudentCaltholic.Tests/SecurityHelpersTests.cs` | Unit Test | +175 / -0 |
| 9 | `tests/ManagerStudentCaltholic.Tests/VietnamTimeHelperTests.cs` | Unit Test | +40 / -0 |

### 1.2. Modified (`78` tệp mã nguồn, view & cấu hình ngoài `AI-Knowledge/`)

#### Startup, Extensions & Configuration (`5` tệp)
| STT | Đường dẫn | Tầng (Layer) | Số dòng (+/-) |
|---|---|---|---|
| 1 | `ManagerStudentCaltholic/Program.cs` | Entrypoint / Pipeline | +31 / -3 |
| 2 | `ManagerStudentCaltholic/Extensions/SecurityExtensions.cs` | Extension / Rate Limiting | +77 / -5 |
| 3 | `ManagerStudentCaltholic/Extensions/ServiceCollectionExtensions.cs` | Extension / DI & Proxy | +90 / -11 |
| 4 | `ManagerStudentCaltholic/appsettings.json` | Configuration | +9 / -4 |
| 5 | `ManagerStudentCaltholic/Dockerfile` | Deployment / Docker | +2 / -0 |

#### Interfaces, Options & ViewModels (`10` tệp)
| STT | Đường dẫn | Tầng (Layer) | Số dòng (+/-) |
|---|---|---|---|
| 6 | `ManagerStudentCaltholic/Interface/Services/IClassCurriculumService.cs` | Interface | +2 / -2 |
| 7 | `ManagerStudentCaltholic/Interface/Services/IGradeCalculationService.cs` | Interface | +8 / -0 |
| 8 | `ManagerStudentCaltholic/Interface/Services/IRedisAttendanceService.cs` | Interface | +24 / -0 |
| 9 | `ManagerStudentCaltholic/Interface/Services/IStaffStudentLookupService.cs` | Interface | +4 / -0 |
| 10 | `ManagerStudentCaltholic/Models/Options/DriveLogUploadOptions.cs` | Model / Options | +1 / -1 |
| 11 | `ManagerStudentCaltholic/Models/Options/GoogleDriveOptions.cs` | Model / Options | +2 / -2 |
| 12 | `ManagerStudentCaltholic/Models/ViewModels/AttendanceViewModels.cs` | Model / ViewModel & DTO | +4 / -0 |
| 13 | `ManagerStudentCaltholic/Models/ViewModels/LookupViewModels.cs` | Model / ViewModel & DTO | +6 / -1 |
| 14 | `ManagerStudentCaltholic/Models/ViewModels/PrintingViewModels.cs` | Model / DTO | +1 / -0 |
| 15 | `ManagerStudentCaltholic/Models/ViewModels/StudentViewModel.cs` | Model / ViewModel | +3 / -11 |

#### Services (`17` tệp)
| STT | Đường dẫn | Tầng (Layer) | Số dòng (+/-) |
|---|---|---|---|
| 16 | `ManagerStudentCaltholic/Services/AttendanceDisputeService.cs` | Service | +56 / -17 |
| 17 | `ManagerStudentCaltholic/Services/BackgroundExportJobService.cs` | Service / Hangfire Job | +116 / -32 |
| 18 | `ManagerStudentCaltholic/Services/ClassCurriculumService.cs` | Service | +62 / -43 |
| 19 | `ManagerStudentCaltholic/Services/ClassEvaluationService.cs` | Service | +194 / -125 |
| 20 | `ManagerStudentCaltholic/Services/DriveLogUploadService.cs` | Service | +62 / -25 |
| 21 | `ManagerStudentCaltholic/Services/GoogleDriveStorageService.cs` | Service / Storage | +30 / -4 |
| 22 | `ManagerStudentCaltholic/Services/GradeCalculationService.cs` | Service | +30 / -12 |
| 23 | `ManagerStudentCaltholic/Services/GradePermissionService.cs` | Service / AuthZ | +99 / -30 |
| 24 | `ManagerStudentCaltholic/Services/LessonPlanImportService.cs` | Service | +24 / -2 |
| 25 | `ManagerStudentCaltholic/Services/LogRetentionBackgroundService.cs` | Service / Background | +81 / -52 |
| 26 | `ManagerStudentCaltholic/Services/MassCampaignService.cs` | Service | +60 / -19 |
| 27 | `ManagerStudentCaltholic/Services/ProfileHistoryService.cs` | Service | +7 / -7 |
| 28 | `ManagerStudentCaltholic/Services/PromotionEvaluationService.cs` | Service | +26 / -7 |
| 29 | `ManagerStudentCaltholic/Services/RedisAttendanceService.cs` | Service / Cache & Sync | +886 / -319 |
| 30 | `ManagerStudentCaltholic/Services/SacramentEvaluationService.cs` | Service | +39 / -17 |
| 31 | `ManagerStudentCaltholic/Services/StaffStudentLookupService.cs` | Service | +162 / -8 |
| 32 | `ManagerStudentCaltholic/Services/StudentExcelService.cs` | Service / Excel | +215 / -113 |

#### Controllers (`21` tệp)
| STT | Đường dẫn | Tầng (Layer) | Số dòng (+/-) |
|---|---|---|---|
| 33 | `ManagerStudentCaltholic/Controllers/AbsenceController.cs` | Controller | +211 / -61 |
| 34 | `ManagerStudentCaltholic/Controllers/AccountController.cs` | Controller | +88 / -35 |
| 35 | `ManagerStudentCaltholic/Controllers/AttendanceController.cs` | Controller | +741 / -392 |
| 36 | `ManagerStudentCaltholic/Controllers/BackgroundJobsController.cs` | Controller | +6 / -6 |
| 37 | `ManagerStudentCaltholic/Controllers/ClassesController.cs` | Controller | +114 / -28 |
| 38 | `ManagerStudentCaltholic/Controllers/CloudMediaController.cs` | Controller / Media Proxy | +330 / -46 |
| 39 | `ManagerStudentCaltholic/Controllers/DashboardController.cs` | Controller | +33 / -5 |
| 40 | `ManagerStudentCaltholic/Controllers/FacilitiesController.cs` | Controller | +6 / -5 |
| 41 | `ManagerStudentCaltholic/Controllers/GovernanceController.cs` | Controller | +54 / -0 |
| 42 | `ManagerStudentCaltholic/Controllers/GradesController.cs` | Controller | +324 / -114 |
| 43 | `ManagerStudentCaltholic/Controllers/HomeController.cs` | Controller | +18 / -4 |
| 44 | `ManagerStudentCaltholic/Controllers/LessonPlansController.cs` | Controller | +110 / -98 |
| 45 | `ManagerStudentCaltholic/Controllers/LogsController.cs` | Controller | +151 / -62 |
| 46 | `ManagerStudentCaltholic/Controllers/LookupController.cs` | Controller | +92 / -20 |
| 47 | `ManagerStudentCaltholic/Controllers/MassCampaignsController.cs` | Controller | +25 / -1 |
| 48 | `ManagerStudentCaltholic/Controllers/ParentMessagesController.cs` | Controller | +55 / -13 |
| 49 | `ManagerStudentCaltholic/Controllers/PublicRegistrationController.cs` | Controller | +228 / -107 |
| 50 | `ManagerStudentCaltholic/Controllers/SacramentsController.cs` | Controller | +211 / -49 |
| 51 | `ManagerStudentCaltholic/Controllers/StudentRegistrationsController.cs` | Controller | +12 / -12 |
| 52 | `ManagerStudentCaltholic/Controllers/StudentsController.cs` | Controller | +59 / -14 |
| 53 | `ManagerStudentCaltholic/Controllers/UsersController.cs` | Controller | +87 / -35 |

#### Razor Views, Static JS & Repo Docs (`25` tệp)
| STT | Đường dẫn | Tầng (Layer) | Số dòng (+/-) |
|---|---|---|---|
| 54 | `ManagerStudentCaltholic/Views/Attendance/Index.cshtml` | Razor View | +102 / -34 |
| 55 | `ManagerStudentCaltholic/Views/Attendance/MonthlySummary.cshtml` | Razor View | +4 / -1 |
| 56 | `ManagerStudentCaltholic/Views/Attendance/ScanQr.cshtml` | Razor View | +135 / -50 |
| 57 | `ManagerStudentCaltholic/Views/Classes/Index.cshtml` | Razor View | +20 / -8 |
| 58 | `ManagerStudentCaltholic/Views/Dashboard/Index.cshtml` | Razor View | +2 / -1 |
| 59 | `ManagerStudentCaltholic/Views/Enrollments/Manage.cshtml` | Razor View | +19 / -2 |
| 60 | `ManagerStudentCaltholic/Views/Facilities/Index.cshtml` | Razor View | +10 / -6 |
| 61 | `ManagerStudentCaltholic/Views/Grades/Index.cshtml` | Razor View | +25 / -14 |
| 62 | `ManagerStudentCaltholic/Views/LessonPlans/Index.cshtml` | Razor View | +54 / -30 |
| 63 | `ManagerStudentCaltholic/Views/Lookup/Index.cshtml` | Razor View | +1 / -0 |
| 64 | `ManagerStudentCaltholic/Views/Lookup/Result.cshtml` | Razor View | +21 / -11 |
| 65 | `ManagerStudentCaltholic/Views/ParentMessages/Index.cshtml` | Razor View | +2 / -1 |
| 66 | `ManagerStudentCaltholic/Views/Pwa/Index.cshtml` | Razor View | +17 / -6 |
| 67 | `ManagerStudentCaltholic/Views/Sacraments/Evaluation.cshtml` | Razor View | +2 / -2 |
| 68 | `ManagerStudentCaltholic/Views/Sacraments/Index.cshtml` | Razor View | +23 / -5 |
| 69 | `ManagerStudentCaltholic/Views/Sacraments/PrintApprovalSheet.cshtml` | Razor View | +2 / -1 |
| 70 | `ManagerStudentCaltholic/Views/Students/Index.cshtml` | Razor View | +79 / -12 |
| 71 | `ManagerStudentCaltholic/Views/Users/Index.cshtml` | Razor View | +19 / -15 |
| 72 | `ManagerStudentCaltholic/wwwroot/js/offline-attendance.js` | Static Asset / JS | +75 / -12 |
| 73 | `ManagerStudentCaltholic/wwwroot/js/staff-student-lookup.js` | Static Asset / JS | +43 / -21 |
| 74 | `.dockerignore` | Repo / Docker Config | +7 / -0 |
| 75 | `.gitignore` | Repo / Git Config | +3 / -0 |
| 76 | `README.md` | Documentation | +8 / -1 |
| 77 | `docs/DOCKER-BUILD-DEPLOY.md` | Documentation | +24 / -1 |
| 78 | `gemini.md` | Repo Playbook | +3 / -3 |

*(Ghi chú: 2 tệp thuộc `AI-Knowledge/` đã được chỉnh sửa trước đó trong commit `997507f` gồm `AI-Knowledge/04-INTEGRATIONS/EXTERNAL-APIS.md` (+1/-1) và `AI-Knowledge/05-FEATURES/FEAT-P5-08-GOOGLE-DRIVE-STORAGE-AND-LOG-BACKJOB.md` (+5/-4).)*

### 1.3. Deleted (`4` tệp) & Renamed (`0` tệp)

| STT | Đường dẫn đã xóa | Tầng (Layer) | Số dòng (-deleted) | Ghi chú |
|---|---|---|---|---|
| 1 | `KNOWLEDGE-ISSUES.jsonl` | Stale Root Knowledge | -14 | Gỡ bản sao lỗi thời ở root; bản chính nằm tại `AI-Knowledge/06-UPDATE-LOG/` |
| 2 | `KNOWLEDGE-VALIDATION-REPORT.md` | Stale Root Knowledge | -86 | Gỡ bản sao lỗi thời ở root |
| 3 | `REQUIRED-CORRECTIONS.md` | Stale Root Knowledge | -117 | Gỡ bản sao lỗi thời ở root |
| 4 | `script/fix-db-1.txt` | Legacy Ad-hoc SQL | -52 | Gỡ tệp DDL cũ sai quy chuẩn tên; giữ nguyên `script/2026*.sql` và `script/seed-curriculum-data.sql` |

---

## 2. Phân tích Chi tiết Các Thay đổi

### 2.1. Class, Method, Property Mới

#### Nhóm Helper Dùng chung (`ManagerStudentCaltholic.Services`)
- **`VietnamTimeHelper`** (`Services/VietnamTimeHelper.cs:7-71`):
  - Properties: `VietnamTimeZone`, `Now`, `Today`, `TodayUtcKind`.
  - Methods: `ToVietnamTime(DateTime dt)`, `NormalizeBusinessDateUtcKind(DateTime date)`, `ResolveVietnamTimeZone()`.
- **`AgeValidationHelper`** (`Services/AgeValidationHelper.cs:7-37`):
  - Methods: `CalculateAgeAtDate(DateTime dateOfBirth, DateTime targetDate)`, `IsAtLeastAge(DateTime dateOfBirth, DateTime targetDate, int minimumYears = 18)`.
- **`FileUploadSecurityHelper`** (`Services/FileUploadSecurityHelper.cs:8-186`):
  - Methods: `HasValidPdfSignature`, `HasValidZipOoxmlSignature`, `HasValidOle2Signature`, `HasValidImageSignature`, `ValidateStreamMagicBytesAsync`, `ValidateHeaderBytes`, `ValidateFormFileMagicBytesAsync`, `GetSafeMimeType`, `TryResolveContainedPath`.
- **`StaffSelfActionGuard`** (`Services/StaffSelfActionGuard.cs:9-30`):
  - Methods: `GetLinkedStudentIdAsync(ParishDbContext context, long userId, CancellationToken cancellationToken = default)`, `IsSelfStudentAction(long? linkedStudentId, long targetStudentId)`.

#### Interface & Service Overloads / Methods Mới
- **`IRedisAttendanceService`** (`Interface/Services/IRedisAttendanceService.cs:42,52,57,65`) & **`RedisAttendanceService`** (`Services/RedisAttendanceService.cs`):
  - `ProcessQrCheckInViaRedisAsync(QrScanRequestDto request, ClaimsPrincipal? caller)` (`:817`)
  - `UndoScanCheckInAsync(UndoScanRequestDto dto, ClaimsPrincipal? caller)` (`:1266`)
  - `ExecuteWithPartitionsFlushLockAsync(...)` (`:1803`) & `FinalizePartitionsAfterExternalSyncAsync(...)` (`:1869`)
  - Helpers nội bộ mới: `FlushSingleClassByIdCoreLockedAsync` (`:2483`), `ValidateCallerScanAccessAsync` (`:3104`), `CanCallerAccessClassAsync` (`:3136`), `EvaluateSessionCheckIn` (`internal static`, `:3230`), `RedisQrScanUndoMeta` (`private class`, `:3273`).
- **`IStaffStudentLookupService`** (`Interface/Services/IStaffStudentLookupService.cs:12,13,17`) & **`StaffStudentLookupService`** (`Services/StaffStudentLookupService.cs`):
  - `SearchStudentsQuickAsync(string keyword, ClaimsPrincipal caller, int limit = 8)` (`:59`)
  - `CanCallerAccessStudentAsync(ClaimsPrincipal caller, long studentId)` (`:65`)
  - `GetRecentLookupsAsync(long userId, ClaimsPrincipal caller, int limit = 5)` (`:713`)
  - Private helpers: `ResolveCallerScopeAsync` (`:82`), `SearchStudentsQuickInternalAsync` (`:151`), `GetRecentLookupsInternalAsync` (`:719`).
- **`IGradeCalculationService`** (`Interface/Services/IGradeCalculationService.cs:32`) & **`GradeCalculationService`** (`Services/GradeCalculationService.cs:98`):
  - `EvaluateGradeFormula(string formulaExpression, Dictionary<string, decimal?> scores, IEnumerable<string>? allColumnCodes = null)`.
- **`StudentExcelService`** (`Services/StudentExcelService.cs:300-407`):
  - `ExportClassListToStreamAsync(int classId, Stream outputStream)` (`:300`), `BuildClassListWorkbookAsync` (`:307`).
- **`BackgroundExportJobService`** (`Services/BackgroundExportJobService.cs:315-329`):
  - Private records `ParishExportStudentRow` và `ParishAttendanceSummaryRow`.
- **`LogRetentionBackgroundService`** (`Services/LogRetentionBackgroundService.cs:87-131`):
  - Private method `CleanDirectory`.
- **`SecurityExtensions`** (`Extensions/SecurityExtensions.cs:17-65`):
  - `GetClientIpKey(HttpContext? httpContext)` (public static) và `ShouldBypassGlobalRateLimit(HttpContext httpContext)` (private static).
- **`ServiceCollectionExtensions`** (`Extensions/ServiceCollectionExtensions.cs:63,121`):
  - Overload `AddReverseProxyConfiguration(this IServiceCollection services, IConfiguration? configuration)` và private `TryParseIpNetwork`.

#### DTOs & ViewModels Mới
- `QrScanResponseDto.IsForbidden` (`bool`, `[JsonIgnore]`, `AttendanceViewModels.cs:144-145`)
- `OfflineSyncItemDto.SessionMode` (`string?`, `AttendanceViewModels.cs:343`)
- `StudentLookupRequestDto.WebsiteUrlHoneypot` (`string?`, `LookupViewModels.cs:25`)
- `SubmitAttendanceDisputeRequestDto.WebsiteUrlHoneypot` (`string?`, `LookupViewModels.cs:217`)
- `SubmitAbsenceRequestDto.WebsiteUrlHoneypot` (`string?`, `PrintingViewModels.cs:12`)

#### Unit Test Project Mới (`tests/ManagerStudentCaltholic.Tests/`)
- `AgeValidationTests`, `GradeAndSacramentLogicTests`, `SecurityHelpersTests`, `VietnamTimeHelperTests` (`41` unit tests tổng cộng, dùng `xunit` + `Microsoft.EntityFrameworkCore.InMemory`).

---

### 2.2. Class, Method, Property Bị Sửa hoặc Xóa

- **Thay đổi chữ ký Interface (Breaking signature):**
  - `IClassCurriculumService.GetVersionFileAsync(long versionId)` (`IClassCurriculumService.cs:46`) & `ClassCurriculumService.GetVersionFileAsync` (`:597`) đổi kiểu trả về từ `Task<(byte[] FileBytes, string ContentType, string DownloadFileName)?>` sang `Task<(Stream FileStream, string ContentType, string DownloadFileName)?>`.
- **Thay đổi Options mặc định (commit `997507f`):**
  - `GoogleDriveOptions`: `Enabled` mặc định `true` ➔ `false` (`:7`), `ServiceAccountKeyPath` mặc định `"/app/secrets/google-service-account.json"` ➔ `string.Empty` (`:8`).
  - `DriveLogUploadOptions`: `Enabled` mặc định `true` ➔ `false` (`:7`).
- **Xóa helper nội bộ cũ thay bằng helper dùng chung:**
  - `DriveLogUploadService.GetVietnamTimeZone()` đã xóa (thay bằng `VietnamTimeHelper.VietnamTimeZone`).
  - `LessonPlansController.HasValidMagicBytesAsync` và `PublicRegistrationController.IsValidMagicBytesAsync` đã xóa (thay bằng `FileUploadSecurityHelper.ValidateFormFileMagicBytesAsync`).
  - `StudentViewModel.CalculateAgeAtDate` (`:73-74`) sửa công thức trừ năm gây lệch ngày 29/02 sang gọi `AgeValidationHelper.CalculateAgeAtDate`.
- **Sửa logic bảo mật, đồng thời & hiệu năng cốt lõi:**
  - `GoogleDriveStorageService`: đổi `ConcurrentDictionary<string, SemaphoreSlim> FolderLocks` không giới hạn thành mảng khóa phân dải cố định `64` phần tử (`FolderLockStripes`), chặn giới hạn `MaxUploadBytes = 30 MB` khi spool.
  - `DriveLogUploadService`: xóa bù file log cục bộ đã verified nếu `DeleteLocalAfterVerifiedUpload == true`, bỏ qua file `> 200 MB`, áp dụng `RetryDelaySeconds`.
  - `LogRetentionBackgroundService`: sửa đăng ký DI bị trùng thành 1 singleton duy nhất, bổ sung dọn file tạm `> 2 giờ` tại `PrivateUploads/tmp_logs` và `PrivateUploads/cloud_tmp`.
  - `GradeCalculationService` & `ClassEvaluationService`: không ép `null` thành `0.0` trong `EvaluateGradeFormula`; làm tròn `MidpointRounding.AwayFromZero`; chặn trọng số âm / tổng trọng số `<= 0`; bọc giao dịch `CreateExecutionStrategy()` + kiểm tra `IsGradeLocked` & `AcademicYear.IsCurrent`.
  - `SacramentEvaluationService` & `PromotionEvaluationService`: sửa lỗi chia cho `0` (`NaN`) khi lớp có `0` buổi Chúa Nhật; đồng nhất hằng `AttendanceStatus.AbsentPermitted` + `LinedUp` + đếm phiên Chúa Nhật.
  - `AttendanceDisputeService`: gộp 4 thông báo lỗi xác thực thành 1 thông báo chung `GenericParentVerificationFailedMessage`; chặn `ProbationaryTeacher` ca chiều & tự duyệt đơn qua `StaffSelfActionGuard`.
  - `GradePermissionService` & `MassCampaignService`: bổ sung fail-closed `BranchHead`, lọc `AcademicYearId`, chặn `ProbationaryTeacher` ca chiều, và chặn tự điểm danh `LinkedStudentId` trong `MassCampaignService`.

---

### 2.3. Thay đổi Controller, Route, Action

- **Route mới (`Program.cs:216`):**
  - `GET /health` (`[AllowAnonymous]`) trả `{ status = "healthy" }` phục vụ Docker `HEALTHCHECK` và bypass `GlobalLimiter`.
- **Thay đổi phân quyền & phản hồi HTTP trên Controllers:**
  - `HomeController`: gắn `[Authorize(Policy = "RequireStaff")]` cho `StaffSearchSuggestions`, `StaffStudentLookup`, `StaffRecentLookups`, `StaffLookupOptions`; trả `403` khi `CanCallerAccessStudentAsync` = `false`; `Index` ẩn widget lookup với `UserRole.Parent`.
  - `CloudMediaController`: `File` (`GET /CloudMedia/File/{id:long}`) thêm header `Content-Security-Policy: sandbox` cho `.pdf`, ép `attachment` cho `ABSENCE_DOC`; `CanAccessMediaAsync` khóa quyền `AVATAR` (`Student` theo `CanStaffAccessStudentAsync`, `User` theo `CanStaffAccessUserAvatarAsync`), lọc `AcademicYear.IsCurrent` và chặn `ProbationaryTeacher` ca chiều.
  - `ClassesController`: `GetAvailableTeachers` siết thành `[Authorize(Roles = "Admin,SpiritualDirector,ExecutiveBoard,BranchHead")]`; `GetTeachers` trả `403` nếu không thuộc lớp; `AssignTeacher` chặn gán `UserRole.Parent`; `Index` fail-closed cho `BranchHead` rỗng khối.
  - `AttendanceController`: `ScanCheckIn`, `UndoScanCheckIn`, `SaveBatch`, `QuickUpdateSingle`, `GetAuditHistory`, `GetClassRule`, `SaveClassRule`, `GetClassDisputes`, `ReviewDispute`, `SyncOfflineQueue`, `Statistics`, `ExportStatisticsExcel`, `MonthlySummary`, `ExportMonthlySummaryExcel` đồng loạt siết scope (fail-closed `BranchHead`, `AcademicYearId`, chặn `ProbationaryTeacher` ca chiều, chặn `StaffSelfActionGuard` `403`, idempotency `ATT:OFFLINE:IDEMP:*` và xử lý trùng PostgreSQL `23505`).
  - `GradesController`: `SaveSingleCell`, `AddCustomColumn`, `DeleteCustomColumn`, `SubmitClassEvaluation` kiểm tra `IsGradeLocked` & `IsCurrent` trong transaction, trả `403` khi vi phạm hoặc tự chấm điểm.
  - `SacramentsController`: thêm `ValidateBranchHeadScopeAsync` (kiểm tra `batchId` và `grade` allow-list `CanonicalEvaluationGrades`) trên `Evaluation`, `SaveApprovalBatch`, `ExportApprovalExcel`, `PrintApprovalSheet`; `Index` trả `Forbid()` khi `classId` ngoài phạm vi.
  - `LessonPlansController`: fail-closed khi `ClassRoom == null` trên `GetVersionDetail`, `DownloadCurriculumVersion`, `ExportCurrentCurriculumExcel`, `DeleteDocument`; `DownloadCurriculumVersion` stream trực tiếp từ `Stream`; `DownloadDocument` dùng `TryResolveContainedPath` + `FileStream`.
  - `LookupController`: đổi lockout sang `ICacheService` (`lookup:fail:ip:*` và `lookup:fail:ip_code:*`), thêm `WebsiteUrlHoneypot` (`400`) và lockout trên `SubmitDispute` (`429`).
  - `AbsenceController`: thêm Redis lockout trên `SubmitRequest`/`SubmitRequestWithFile` (`429`), gộp lỗi chung chống enumeration, giới hạn 5 đơn chờ & chống nộp trùng, kiểm tra cửa sổ ngày `[Today-7, Today+60]`, `Review` trả `403` nếu tự duyệt.
  - `PublicRegistrationController` & `StudentRegistrationsController`: thêm lockout `pubreg:fail:*` (`429`) trên `CheckStatus`/`Resubmit`/`SubmitResubmit`/`ViewOwnCertificate`, re-encode JPG/PNG sang WebP qua `IImageOptimizationService`, dùng `PhysicalFile` + `Content-Security-Policy: sandbox` khi trả chứng nhận.
  - `BackgroundJobsController`: `DownloadExport` dùng `TryResolveContainedPath` + `PhysicalFile`.
  - `LogsController`: `ParseLogFileEntriesAsync` giới hạn đọc 5 MB cuối / tối đa 5.000 dòng / 64 KB stack trace bằng `StringBuilder`.
  - `AccountController`, `StudentsController`, `UsersController`, `GovernanceController`: đồng bộ ràng buộc `>= 18` tuổi (cả khi tạo `Teacher` ở `UsersController` và nâng cấp ở `GovernanceController`), chặn non-Leadership tự đổi `DateOfBirth`/`CatechistPromiseDate`, đồng bộ ngược sang `LinkedUser`, kiểm tra magic bytes `.xlsx` khi import.
  - `DashboardController`, `FacilitiesController`, `MassCampaignsController`, `ParentMessagesController`: chuẩn hóa allow-list `gradeLevel`, giờ Việt Nam `VietnamTimeHelper`, và mã lỗi `403` cho vi phạm quyền/tự điểm danh chiến dịch.

---

### 2.4. Thay đổi Razor View & Static Assets

- **Chống XSS (18 Razor Views & 2 JS):**
  - Loại bỏ `@Html.Raw` chứa dữ liệu động trong JS tại `Views/Attendance/Index.cshtml`, `Views/Dashboard/Index.cshtml` (dùng `data-grade`), `Views/Lookup/Result.cshtml` (dùng `@Json.Serialize`), `Views/Sacraments/Evaluation.cshtml`.
  - Escape HTML / dùng `createElement` + `textContent` / thay inline `onclick` bằng `data-*` + `addEventListener` tại `Views/Attendance/Index.cshtml`, `ScanQr.cshtml`, `Classes/Index.cshtml`, `Enrollments/Manage.cshtml`, `Facilities/Index.cshtml`, `Grades/Index.cshtml`, `LessonPlans/Index.cshtml`, `Pwa/Index.cshtml`, `Sacraments/Index.cshtml`, `Students/Index.cshtml`, `Users/Index.cshtml`, và `wwwroot/js/staff-student-lookup.js`.
- **Honeypot & Giao diện Công khai:**
  - `Views/Lookup/Index.cshtml` & `Views/Lookup/Result.cshtml`: thêm trường ẩn `WebsiteUrlHoneypot` cho form tra cứu, phản ánh và đơn nghỉ phép.
- **Múi giờ Local & Offline Sync (`offline-attendance.js`, `ScanQr.cshtml`):**
  - Thay `new Date().toISOString().split('T')[0]` bằng ngày local `YYYY-MM-DD` tại `Attendance/Index.cshtml`, `LessonPlans/Index.cshtml`, `Sacraments/Index.cshtml`, `Students/Index.cshtml`, và `offline-attendance.js` (`formatLocalDateYyyyMmDd`).
  - `offline-attendance.js`: nâng IndexedDB `DB_VERSION = 2` (chuyển `by_enrollmentId` sang `{ unique: false }` hỗ trợ mã cũ), chỉ xóa bản ghi có mặt trong `data.syncedClientIds`, kiểm tra `store.get` khi hoàn tác offline; `ScanQr.cshtml` tắt Undo khi `isDuplicate === true` và fallback sang `/Attendance/UndoScanCheckIn` nếu offline đã tự đồng bộ.

---

### 2.5. Thay đổi SQL & Database Schema

- **Xác nhận qua `git diff --name-status e183ccd..HEAD`:**
  - **Không có thay đổi nào** đối với Entity (`Models/Entities/`), `Data/ParishDbContext.cs`, `Migrations/`, hay thêm tệp `.sql` mới trong `e183ccd..HEAD`.
  - Thay đổi duy nhất liên quan `script/` là xóa tệp cũ `script/fix-db-1.txt` (do không theo quy chuẩn tên `{yyyyMMdd}_*.sql` và dùng schema snake_case cũ).
  - *(Lưu ý từ khoảng nền `2a3263d..e183ccd`: Các thay đổi schema của Phase 6 & `FEAT-P5-08` nằm trong 3 script SQL ở `script/20261120_*.sql`, `script/20261123_*.sql` và đã được phản ánh vào `02-DATABASE/` ở lần cập nhật `4589783`; thư mục `ManagerStudentCaltholic/Migrations/` hiện vẫn trống).*

---

### 2.6. Thay đổi API, Integration & Configuration

- **Rate Limiting & Reverse Proxy (`Program.cs`, `SecurityExtensions.cs`, `ServiceCollectionExtensions.cs`, `appsettings.json`):**
  - Thêm mục `ReverseProxy` vào `appsettings.json` (`ForwardLimit: 1`, `KnownProxies: ["127.0.0.1", "::1"]`, `KnownNetworks: ["172.16.0.0/12"]`) thay cho dải RFC198 rộng trước đó.
  - Chuẩn hóa IPv4-mapped IPv6 và bật `GlobalLimiter` (`200 req/min/IP`), bypass `/health`, tài nguyên tĩnh và `/Attendance/ScanCheckIn`.
- **Google Drive & Redis Defaults (`appsettings.json`, `GoogleDriveOptions`, `DriveLogUploadOptions`, `Program.cs`):**
  - Đặt `ConnectionStrings:RedisConnection = ""` trong `appsettings.json` (tránh fallback về `localhost:6379` trong container khi thiếu biến môi trường).
  - Đặt `GoogleDrive:Enabled = false`, `GoogleDrive:ServiceAccountKeyPath = ""`, `DriveLogUpload:Enabled = false`.
  - Bổ sung cảnh báo khởi động ở môi trường non-Development khi DB connection dùng `Trust Server Certificate=true` hoặc thiếu `SSL Mode=`, hoặc Redis connection thiếu `password=`.
- **Docker, Gitignore & Tài liệu (`Dockerfile`, `.gitignore`, `.dockerignore`, `README.md`, `docs/DOCKER-BUILD-DEPLOY.md`, `gemini.md`):**
  - `Dockerfile`: thêm `HEALTHCHECK` gọi `http://127.0.0.1:8080/health`.
  - `.gitignore` & `.dockerignore`: bổ sung `*service-account*.json`, `credentials*.json`, `*.jks`; `.dockerignore` loại thêm `**/scripts`, `ManagerStudentCaltholic/scripts`, `tests`, `**/tests`.
  - Thống nhất tên service Docker Compose là `dotnet_app` trong `README.md`, `docs/DOCKER-BUILD-DEPLOY.md` và `gemini.md` (§7.3).
