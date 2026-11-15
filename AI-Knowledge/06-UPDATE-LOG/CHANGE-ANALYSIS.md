# BÁO CÁO PHÂN TÍCH THAY ĐỔI MÃ NGUỒN (CHANGE ANALYSIS REPORT)

- **Dự án:** `ManagerStudentCaltholic`
- **Thời gian phân tích:** `2026-11-15T14:05:00+07:00`
- **Vai trò:** Senior Software Architect & Codebase Analyst
- **Commit Baseline (`KNOWLEDGE-BASELINE.json`):** `c0e828a` (`perf(mobile): optimize mobile layout rendering, remove scroll listener & prevent SW reload loop`)
- **Commit Hiện tại (`HEAD`):** `2a3263d` (`add`) kèm thay đổi cục bộ chưa commit tại `appsettings.json`, `appsettings.Development.json`, `docker-compose.yml`, `.env`
- **Tổng số tệp thay đổi trong phạm vi mã nguồn & cấu hình:** **53 tệp** (`7 Added`, `46 Modified`, `0 Deleted`, `0 Renamed`), chưa tính 3 tệp sinh tự động trong `ManagerStudentCaltholic/bin/Debug/net8.0/` và 2 tệp untracked (`nginx/default.conf`, `script/seed-curriculum-data.sql`).

---

## 1. Phân loại các File Thay đổi (File Classification)

### 1.1. Added (7 tệp mới tạo)
| STT | Đường dẫn | Tầng (Layer) | Số dòng (+/-) |
|---|---|---|---|
| 1 | `ManagerStudentCaltholic/Interface/Services/IClassCurriculumService.cs` | Interface / Service | +48 / -0 |
| 2 | `ManagerStudentCaltholic/Models/DTOs/CurriculumDtos.cs` | Model / DTO | +75 / -0 |
| 3 | `ManagerStudentCaltholic/Models/Entities/ClassCurriculumDocument.cs` | Model / Entity | +71 / -0 |
| 4 | `ManagerStudentCaltholic/Models/Entities/ClassCurriculumVersion.cs` | Model / Entity | +55 / -0 |
| 5 | `ManagerStudentCaltholic/Models/ViewModels/LessonPlansViewModel.cs` | Model / ViewModel | +22 / -0 |
| 6 | `ManagerStudentCaltholic/Services/ClassCurriculumService.cs` | Service | +1052 / -0 |
| 7 | `ManagerStudentCaltholic/Views/PublicRegistration/Resubmit.cshtml` | Razor View | +494 / -0 |

*Ghi chú tệp untracked ngoài Git HEAD:* `nginx/default.conf`, `script/seed-curriculum-data.sql`.

### 1.2. Modified (46 tệp mã nguồn & cấu hình)
| STT | Đường dẫn | Tầng (Layer) | Số dòng (+/-) |
|---|---|---|---|
| 1 | `ManagerStudentCaltholic/Controllers/AccountController.cs` | Controller | +50 / -12 |
| 2 | `ManagerStudentCaltholic/Controllers/AttendanceController.cs` | Controller | +68 / -1 |
| 3 | `ManagerStudentCaltholic/Controllers/LessonPlansController.cs` | Controller | +217 / -27 |
| 4 | `ManagerStudentCaltholic/Controllers/PublicRegistrationController.cs` | Controller | +250 / -2 |
| 5 | `ManagerStudentCaltholic/Controllers/PwaController.cs` | Controller | +1 / -1 |
| 6 | `ManagerStudentCaltholic/Controllers/StudentsController.cs` | Controller | +1 / -2 |
| 7 | `ManagerStudentCaltholic/Controllers/UsersController.cs` | Controller | +61 / -10 |
| 8 | `ManagerStudentCaltholic/Data/ParishDbContext.cs` | Data / ORM | +32 / -0 |
| 9 | `ManagerStudentCaltholic/Extensions/LoggingExtensions.cs` | Extension / Logging | +3 / -2 |
| 10 | `ManagerStudentCaltholic/Extensions/ServiceCollectionExtensions.cs` | Extension / DI & Auth | +15 / -2 |
| 11 | `ManagerStudentCaltholic/Extensions/WebApplicationExtensions.cs` | Extension / Seeding | +172 / -4 |
| 12 | `ManagerStudentCaltholic/Interface/Services/IRedisAttendanceService.cs` | Interface / Service | +6 / -1 |
| 13 | `ManagerStudentCaltholic/Models/DTOs/PublicRegistrationSubmitDto.cs` | Model / DTO | +19 / -1 |
| 14 | `ManagerStudentCaltholic/Models/Entities/ClassLessonPlan.cs` | Model / Entity | +8 / -2 |
| 15 | `ManagerStudentCaltholic/Models/ViewModels/AttendanceViewModels.cs` | Model / ViewModel & DTO | +30 / -1 |
| 16 | `ManagerStudentCaltholic/Models/ViewModels/AuthViewModels.cs` | Model / ViewModel | +35 / -14 |
| 17 | `ManagerStudentCaltholic/Models/ViewModels/BatchTeacherViewModels.cs` | Model / ViewModel | +2 / -2 |
| 18 | `ManagerStudentCaltholic/Services/FeatureNavigationService.cs` | Service | +73 / -10 |
| 19 | `ManagerStudentCaltholic/Services/RedisAttendanceService.cs` | Service | +286 / -14 |
| 20 | `ManagerStudentCaltholic/Services/RedisCacheService.cs` | Service / Cache | +26 / -0 |
| 21 | `ManagerStudentCaltholic/Services/TokenService.cs` | Service / Security | +1 / -1 |
| 22 | `ManagerStudentCaltholic/Views/Account/AccessDenied.cshtml` | Razor View | +85 / -21 |
| 23 | `ManagerStudentCaltholic/Views/Account/Login.cshtml` | Razor View | +1 / -1 |
| 24 | `ManagerStudentCaltholic/Views/Account/Profile.cshtml` | Razor View | +603 / -198 |
| 25 | `ManagerStudentCaltholic/Views/Attendance/Index.cshtml` | Razor View | +271 / -187 |
| 26 | `ManagerStudentCaltholic/Views/Attendance/ScanQr.cshtml` | Razor View | +1058 / -391 |
| 27 | `ManagerStudentCaltholic/Views/Grades/Index.cshtml` | Razor View | +102 / -103 |
| 28 | `ManagerStudentCaltholic/Views/Home/Guides/_GuideAcademic.cshtml` | Razor Partial View | +5 / -2 |
| 29 | `ManagerStudentCaltholic/Views/Home/Index.cshtml` | Razor View | +2 / -2 |
| 30 | `ManagerStudentCaltholic/Views/LessonPlans/Index.cshtml` | Razor View | +945 / -119 |
| 31 | `ManagerStudentCaltholic/Views/Lookup/Result.cshtml` | Razor View | +546 / -215 |
| 32 | `ManagerStudentCaltholic/Views/Promotion/Index.cshtml` | Razor View | +0 / -10 |
| 33 | `ManagerStudentCaltholic/Views/PublicRegistration/CheckStatus.cshtml` | Razor View | +478 / -58 |
| 34 | `ManagerStudentCaltholic/Views/PublicRegistration/Index.cshtml` | Razor View | +146 / -53 |
| 35 | `ManagerStudentCaltholic/Views/PublicRegistration/Success.cshtml` | Razor View | +50 / -22 |
| 36 | `ManagerStudentCaltholic/Views/Shared/_Layout.cshtml` | Razor Layout | +3 / -3 |
| 37 | `ManagerStudentCaltholic/Views/Shared/_LayoutMobileBottomNav.cshtml` | Razor Partial View | +65 / -45 |
| 38 | `ManagerStudentCaltholic/Views/Shared/_LayoutMobileHeader.cshtml` | Razor Partial View | +27 / -5 |
| 39 | `ManagerStudentCaltholic/Views/Students/Index.cshtml` | Razor View | +0 / -119 |
| 40 | `ManagerStudentCaltholic/Views/Users/Index.cshtml` | Razor View | +435 / -189 |
| 41 | `ManagerStudentCaltholic/wwwroot/css/mobile-layout.css` | Static Asset / CSS | +193 / -62 |
| 42 | `ManagerStudentCaltholic/wwwroot/js/offline-attendance.js` | Static Asset / JS | +119 / -12 |
| 43 | `ManagerStudentCaltholic/appsettings.json` | Configuration | +1 / -1 |
| 44 | `ManagerStudentCaltholic/appsettings.Development.json` | Configuration | +1 / -1 |
| 45 | `ManagerStudentCaltholic/Dockerfile` | Deployment / Docker | +4 / -4 |
| 46 | `docker-compose.yml` | Deployment / Docker | +9 / -6 |

*(Ngoài ra có 3 tệp build artifact dưới `ManagerStudentCaltholic/bin/Debug/net8.0/`: `ManagerStudentCaltholic.staticwebassets.endpoints.json`, `ManagerStudentCaltholic.staticwebassets.runtime.json`, `appsettings.json`).*

### 1.3. Deleted (0 tệp) & Renamed (0 tệp)
- Không có tệp mã nguồn nào bị xóa hoặc đổi tên trong phạm vi `c0e828a..HEAD`.

---

## 2. Phân tích Chi tiết Các Thay đổi

### 2.1. Class, Method, Property Mới

#### Phân hệ Kế hoạch Lớp & Giáo án (`FEAT-P5-07`)
- **`ManagerStudentCaltholic.Models.Entities.ClassCurriculumDocument`** (`Models/Entities/ClassCurriculumDocument.cs`):
  - Đại diện tài liệu kế hoạch phân phối chương trình hiện hành theo lớp (`ClassRoomId`) và niên khóa (`AcademicYearId`).
  - Các property chính: `Id`, `ClassRoomId`, `AcademicYearId`, `Title`, `Description`, `CurrentVersion`, `VersionNumber`, `ContentJson`, `LastAttachmentFileName`, `LastAttachmentFilePath`, `LastAttachmentFileSize`, `LastAttachmentContentType`, `CreatedBy`, `CreatedByUserId`, `CreatedAt`, `UpdatedBy`, `UpdatedByUserId`, `UpdatedAt`, `IsActive`, cùng navigation collections `Versions` và `LessonPlans`.
- **`ManagerStudentCaltholic.Models.Entities.ClassCurriculumVersion`** (`Models/Entities/ClassCurriculumVersion.cs`):
  - Lưu lịch sử từng phiên bản ngày sửa và bản chụp file `.xlsx` (`KeHoach_{Class}_{Version}_{YYYYMMDD}.xlsx`).
  - Properties: `Id`, `ClassCurriculumDocumentId`, `VersionNumber`, `VersionTag`, `ChangeSummary`, `ContentJson`, `FileName`, `StoredFileName`, `FilePath`, `FileSize`, `ContentType`, `CreatedBy`, `CreatedByUserId`, `CreatedAt`.
- **`ManagerStudentCaltholic.Models.DTOs` (`Models/DTOs/CurriculumDtos.cs`):**
  - 5 DTO mới: `CurriculumItemDto`, `ClassCurriculumDocumentDto`, `ClassCurriculumVersionDto`, `SaveCurriculumRequestDto`, `CurriculumParseResultDto`.
- **`ManagerStudentCaltholic.Models.ViewModels.LessonPlanWorkspaceViewModel`** (`Models/ViewModels/LessonPlansViewModel.cs`):
  - ViewModel workspace kế hoạch lớp gồm `Classes`, `CurrentYear`, `SelectedClassId`, `CurriculumDocument`, `Versions`, `LegacyDocuments`.
- **`ManagerStudentCaltholic.Interface.Services.IClassCurriculumService`** & **`ManagerStudentCaltholic.Services.ClassCurriculumService`**:
  - 8 methods mới: `GetCurriculumByClassAsync`, `GetVersionsAsync`, `GetVersionDetailAsync`, `ParseUploadedCurriculumFileAsync`, `SaveCurriculumAsync`, `RollbackCurriculumAsync`, `ExportCurriculumToExcelAsync`, `GetVersionFileAsync`.
- **`WebApplicationExtensions.SeedSampleCurriculumData`** (`Extensions/WebApplicationExtensions.cs`):
  - Khởi tạo dữ liệu mẫu cho `ClassCurriculumDocuments` và `ClassCurriculumVersions` khi khởi động nếu bảng rỗng.

#### Phân hệ Điểm danh QR & HUD (`FEAT-P5-04`)
- **`ManagerStudentCaltholic.Models.ViewModels.UndoScanRequestDto`** (`Models/ViewModels/AttendanceViewModels.cs`, dòng 108-117):
  - Properties: `EnrollmentId`, `ClassId`, `StudentCode`, `AttendanceDate`, `ActivityType`, `SessionMode`.
- **`IRedisAttendanceService.UndoScanCheckInAsync(UndoScanRequestDto dto)`** & **`RedisAttendanceService.UndoScanCheckInAsync(UndoScanRequestDto dto)`** (`Services/RedisAttendanceService.cs`, dòng 692-785):
  - Thu hồi trạng thái điểm danh trên Redis ClassSheet (`ATT:CLASS:{classId}:DATE:{date}`), đồng bộ reset bản ghi trong `Attendances` và `MassCampaignRecords`.

#### Phân hệ Tuyển sinh Công khai (`PublicRegistration`)
- **`ManagerStudentCaltholic.Models.ViewModels.PublicRegistrationResubmitDto`** (`Models/DTOs/PublicRegistrationSubmitDto.cs`, dòng 66-83):
  - Kế thừa `PublicRegistrationSubmitDto`, bổ sung `Id`, `OriginalParentPhone`, `OriginalDateOfBirth`, `RejectionReason`, `ExistingCertificatePath`, `Status`, `CreatedAt`, `ReviewedAt`.

#### Hạ tầng Cache (`RedisCacheService`)
- **`ManagerStudentCaltholic.Services.RedisCacheService`** (`Services/RedisCacheService.cs`, dòng 15-22):
  - Bổ sung cơ chế Circuit Breaker 30 giây (`CircuitBreakDuration`, `_circuitOpenUntilTicks`, `IsRedisCircuitOpen()`, `TripRedisCircuit()`) kết hợp đọc/ghi dự phòng trên `IMemoryCache` khi Redis ngoại tuyến.

---

### 2.2. Class, Method, Property Bị Sửa hoặc Xóa
- **`ClassLessonPlan`** (`Models/Entities/ClassLessonPlan.cs`): Bổ sung `CurriculumDocumentId`, `CurriculumDocument`, `WeekNumber`, `Semester`.
- **`QrScanRequestDto`** (`Models/ViewModels/AttendanceViewModels.cs`, dòng 75-76): Thêm `SessionMode`, `CampaignId`.
- **`QrScanResponseDto`** (`AttendanceViewModels.cs`, dòng 95-104): Thêm `IsHistoricalCode`, `OldCode`, `CurrentCode`, `WarningMessage`, `EnrollmentId`, `ClassId`, `SessionMode`, `ScanDate`.
- **`OfflineStudentCacheItemDto`** (`AttendanceViewModels.cs`, dòng 304): Thêm `HistoricalCodes` (`List<string>`).
- **`CreateUserRequestDto`** (`Models/ViewModels/AuthViewModels.cs`, dòng 43-97): Chuẩn hóa setter trim chuỗi cho `ChristianName`, `Email`, `PhoneNumber`, `ManagedGradeLevel`; bắt buộc `ChristianName` với `[Required]`, nâng độ dài `LastName` cho phép lên 50 trên DTO; bỏ yêu cầu `ComplexityPattern` 8 ký tự trên `CreateUserRequestDto.Password` và `ResetPasswordRequestDto.NewPassword` (giảm về `[MinLength(6)]`).
- **`BatchCreateTeachersRequestDto`** (`Models/ViewModels/BatchTeacherViewModels.cs`, dòng 25): Đổi `DefaultPassword` thành `string?`.
- **`AccountController.Login`** (`Controllers/AccountController.cs`, dòng 144): Khi `model.RememberMe == true`, nâng `ExpiresUtc` từ `AddDays(7)` lên `AddDays(14)`.
- **`AccountController.Profile`** (`Controllers/AccountController.cs`, dòng 295) & **`UpdateProfile`** (dòng 401): Bổ sung tham số `long? id = null` / `[Bind("Id,...")]`, cho phép `Admin`, `ExecutiveBoard`, `SpiritualDirector` xem hồ sơ GLV khác (`UpdateProfile` cho phép `Admin`, `ExecutiveBoard`), đặt `ViewBag.IsViewingOther` và `ViewBag.TargetUserId`.
- **`UsersController.Create`** (`Controllers/UsersController.cs`, dòng 87) & **`ResetPassword`** (dòng 193): Tách chuỗi `LastName` thành họ đệm (`FirstName <= 100`) và tên chính (`LastName <= 10`), kiểm tra `ManagedGradeLevel` bắt buộc cho `BranchHead` và hỗ trợ gán `ManagedGradeLevel` cho cả `Teacher`, trả thông điệp lỗi chi tiết từ `ModelState`.
- **`StudentsController.Create`** (`Controllers/StudentsController.cs`, dòng 245): Bổ sung gán `AcademicYearId = selectedClass.AcademicYearId` khi tạo `Enrollment` mới.
- **`PwaController.Offline`** (`Controllers/PwaController.cs`, dòng 23): Sửa `return View();` thành `return View("Index");` khớp với `Views/Pwa/Index.cshtml`.
- **`RedisAttendanceService.ProcessQrCheckInViaRedisAsync`** (`Services/RedisAttendanceService.cs`, dòng 382-687): Hỗ trợ `SessionMode`, tra cứu ngược thẻ mã cũ trong `StudentCodeHistories`, cache alias key lên Redis O(1), trả `IsHistoricalCode` / `WarningMessage`.
- **`TokenService.GenerateRefreshToken`** (`Services/TokenService.cs`, dòng 79): Đổi fallback `RefreshTokenExpirationDays` từ `7` lên `14`.
- **`FeatureNavigationService`** (`Services/FeatureNavigationService.cs`): Bổ sung cập nhật rule `ACD_ATTENDANCE_DIRECT` (`SortOrder = 2`) và thay thế động `{class_id}` trong `TargetUrl` bằng lớp được phân công mặc định của người dùng.
- **`ServiceCollectionExtensions`** (`Extensions/ServiceCollectionExtensions.cs`): Đăng ký `IClassCurriculumService -> ClassCurriculumService` (dòng 89); nâng `ExpireTimeSpan` của `ParishSessionCookie` từ `TimeSpan.FromDays(7)` lên `TimeSpan.FromDays(14)` (dòng 154); thêm cấu hình `DataProtection().SetApplicationName("ManagerStudentCaltholic").PersistKeysToFileSystem(...)` tại `AddStrictSecurityConfigurations` (dòng 250-259).

---

### 2.3. Thay đổi Controller, Route, Action

| Controller | Action | HTTP Method & Route | Thay đổi |
|---|---|---|---|
| `AttendanceController` | `ScanQr()` | `GET /Attendance/ScanQr` | Gợi ý `ViewBag.RecommendedSessionMode` theo ngày/giờ VN |
| `AttendanceController` | `UndoScanCheckIn` | `POST /Attendance/UndoScanCheckIn` | **Mới:** Hoàn tác lượt quét QR trong 5 giây (`UndoScanRequestDto`) |
| `AttendanceController` | `GetOfflineStudentCache` | `GET /Attendance/GetOfflineStudentCache` | Bổ sung `HistoricalCodes` từ `StudentCodeHistories` |
| `LessonPlansController` | `ParseCurriculumFile` | `POST /LessonPlans/ParseCurriculumFile` | **Mới:** Phân tích file kế hoạch `.xlsx/.docx/.csv/.md/.pdf` |
| `LessonPlansController` | `SaveCurriculum` | `POST /LessonPlans/SaveCurriculum` | **Mới:** Lưu phiên bản kế hoạch mới & sinh `.xlsx` |
| `LessonPlansController` | `GetVersionDetail` | `GET /LessonPlans/GetVersionDetail/{versionId:long}` | **Mới:** Xem chi tiết tuần học của một phiên bản |
| `LessonPlansController` | `DownloadCurriculumVersion` | `GET /LessonPlans/DownloadCurriculumVersion/{versionId:long}` | **Mới:** Tải file `.xlsx` theo phiên bản |
| `LessonPlansController` | `ExportCurrentCurriculumExcel` | `GET /LessonPlans/ExportCurrentCurriculumExcel` | **Mới:** Xuất Excel kế hoạch hiện hành |
| `LessonPlansController` | `RollbackCurriculum` | `POST /LessonPlans/RollbackCurriculum` | **Mới:** Khôi phục kế hoạch về phiên bản cũ |
| `PublicRegistrationController` | `Resubmit` | `GET /PublicRegistration/Resubmit` | **Mới:** Form bổ sung & nộp lại hồ sơ bị từ chối |
| `PublicRegistrationController` | `SubmitResubmit` | `POST /PublicRegistration/SubmitResubmit` | **Mới:** Lưu dữ liệu + tệp Giấy Rửa Tội thay thế |
| `AccountController` | `Profile` | `GET /Account/Profile?id={id}` | Mở rộng tham số `id` cho phép Admin/BĐH/Cha xem hồ sơ GLV |
| `PwaController` | `Offline` | `GET /Offline` | Sửa `return View("Index")` để render `Views/Pwa/Index.cshtml` |

---

### 2.4. Thay đổi Razor View & Static Assets
- **Tuyển sinh Công khai:** Thêm mới `Views/PublicRegistration/Resubmit.cshtml`; cập nhật `CheckStatus.cshtml` (nút mở màn hình nộp lại hồ sơ), `Index.cshtml`, `Success.cshtml`.
- **Điểm danh & Quét QR (`FEAT-P5-04`):** Thiết kế lại `Views/Attendance/ScanQr.cshtml` (Viewfinder HUD, Torch/Flip/Zoom, Session Quick-Switcher, Recent Scans Drawer, nút Hoàn tác 5s), cập nhật `wwwroot/js/offline-attendance.js` (`undoOfflineQrScan`, hỗ trợ `HistoricalCodes` & `sessionMode`), dọn phần tử trùng lặp trong `Views/Attendance/Index.cshtml`.
- **Giáo án (`FEAT-P5-07`):** Thiết kế lại `Views/LessonPlans/Index.cshtml` với 3 tab (Trình biên tập tuần trực tuyến, Lịch sử phiên bản ngày sửa, Tài liệu giáo án bổ trợ).
- **Tài khoản & Bảo mật:** Cập nhật `Views/Account/Login.cshtml` (`Duy trì đăng nhập (14 ngày)`), `Views/Account/AccessDenied.cshtml` (hỗ trợ Mobile UI & gọi `clearCacheOnLogout()`), `Views/Account/Profile.cshtml` và `Views/Users/Index.cshtml` (cho phép Admin bấm xem/sửa hồ sơ GLV, chuẩn hóa modal tạo user).
- **Sửa lỗi Razor/JS & Mobile UI:**
  - `Views/Grades/Index.cshtml`: Đưa `#modalClassEvaluation` ra khỏi `@section Scripts` về khối HTML hợp lệ.
  - `Views/Promotion/Index.cshtml`: Xóa đoạn JS thừa tham chiếu `decisions` chưa khai báo ở top-level.
  - `Views/Students/Index.cshtml`: Xóa modal `#createStudentModal` và `#editStudentModal` trùng lặp.
  - `Views/Shared/_Layout.cshtml`, `_LayoutMobileHeader.cshtml`, `_LayoutMobileBottomNav.cshtml`, `Views/Lookup/Result.cshtml`, `Views/Home/Index.cshtml`, `_GuideAcademic.cshtml`, `wwwroot/css/mobile-layout.css`.

---

### 2.5. Thay đổi SQL & Database Schema
- **`ManagerStudentCaltholic/Data/ParishDbContext.cs`** (+32/-0 dòng):
  - Bổ sung 2 `DbSet`: `ClassCurriculumDocuments`, `ClassCurriculumVersions`.
  - Cấu hình Fluent API bảng `"ClassCurriculumDocuments"` (FKs `Classes`, `AcademicYears`, `Users`; Indexes `(ClassRoomId, AcademicYearId)`, `AcademicYearId`).
  - Cấu hình Fluent API bảng `"ClassCurriculumVersions"` (FKs `ClassCurriculumDocuments`, `Users`; Indexes `(ClassCurriculumDocumentId, VersionNumber)`, `CreatedAt`).
  - Cập nhật `ClassLessonPlans`: thêm FK `CurriculumDocumentId` -> `ClassCurriculumDocuments` (`DeleteBehavior.SetNull`) và index trên `CurriculumDocumentId`.
- **Thông tin thiếu (`NEEDS_VERIFICATION`):** Thư mục `ManagerStudentCaltholic/Migrations/` hiện rỗng; chưa thấy file EF Core Migration mới tương ứng với 2 bảng `ClassCurriculumDocuments` / `ClassCurriculumVersions` trong repository. Có tệp untracked `script/seed-curriculum-data.sql` và logic seed code `WebApplicationExtensions.SeedSampleCurriculumData`.

---

### 2.6. Thay đổi API, Integration & Configuration
- **Thời hạn Token & Cookie (14 ngày):**
  - `appsettings.json` (dòng 25) & `appsettings.Development.json` (dòng 26): Đổi `"RefreshTokenExpirationDays"` từ `7` lên `14`.
- **Serilog (`Extensions/LoggingExtensions.cs`):** Ghi đè mức log của `Microsoft.AspNetCore.Antiforgery` thành `LogEventLevel.Fatal`.
- **Docker & Nginx (`Dockerfile`, `docker-compose.yml`):**
  - Tạo thư mục `/app/dataprotection-keys` trong image và mount Docker volume `dataprotection_keys:/app/dataprotection-keys` để bảo toàn Cookie/Antiforgery keys qua các lần restart container.
  - Bổ sung service `nginx` (reverse proxy) phụ thuộc `app` trong `docker-compose.yml` (cùng tệp untracked `nginx/default.conf`).
