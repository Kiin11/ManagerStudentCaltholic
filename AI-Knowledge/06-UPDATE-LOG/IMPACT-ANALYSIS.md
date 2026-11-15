# BÁO CÁO PHÂN TÍCH TÁC ĐỘNG (IMPACT ANALYSIS REPORT)

- **Dự án:** `ManagerStudentCaltholic`
- **Thời gian phân tích:** `2026-11-15T14:05:00+07:00`
- **Baseline đối chiếu:** Commit `c0e828a` $\rightarrow$ Commit `2a3263d` (+ chỉnh sửa cục bộ 2026-11-15)

---

## 1. Phân loại Mức độ Ảnh hưởng Tổng thể

### **Mức độ: HIGH (Ảnh hưởng diện rộng)**

**Căn cứ kỹ thuật:**
1. **Thay đổi Database Schema (`ParishDbContext.cs`):** Bổ sung 2 bảng mới (`ClassCurriculumDocuments`, `ClassCurriculumVersions`) và thay đổi cấu trúc/khóa ngoại trên bảng `ClassLessonPlans`.
2. **Thay đổi Luồng Điểm danh QR & Offline Sync (`FEAT-P5-04`):** Bổ sung endpoint mới `POST /Attendance/UndoScanCheckIn`, thay đổi DTO phản hồi `QrScanResponseDto`, thay đổi logic `RedisAttendanceService.ProcessQrCheckInViaRedisAsync` (hỗ trợ `SessionMode` & tra cứu `StudentCodeHistories`) và `offline-attendance.js`.
3. **Thay đổi Chính sách Bảo mật & Phiên đăng nhập (`UserAccount`):** Nâng thời hạn cookie phiên và Refresh Token từ 7 ngày lên **14 ngày**, mở rộng quyền xem/cập nhật hồ sơ GLV khác trên `AccountController.Profile` / `UpdateProfile`, lưu bền vững Data Protection Key Ring xuống ổ đĩa, thay đổi validation mật khẩu tối thiểu trên DTO quản trị user (`CreateUserRequestDto`, `ResetPasswordRequestDto`).
4. **Thêm Luồng Nghiệp vụ Tuyển sinh Mới (`PublicRegistration`):** Thêm `Resubmit` và `SubmitResubmit` cho phép phụ huynh bổ sung & nộp lại hồ sơ bị từ chối.
5. **Thay đổi Hạ tầng Bộ đệm (`RedisCacheService`):** Thêm Circuit Breaker 30 giây kết hợp `IMemoryCache` fallback ảnh hưởng tới toàn bộ các service/controller gọi `ICacheService`.

---

## 2. Ma trận Phụ thuộc & Thành phần Bị Ảnh hưởng

Quy ước phân loại bằng chứng:
- **[Xác nhận được]**: Có lời gọi, khai báo DI, hoặc tham chiếu cú pháp trực tiếp trong mã nguồn đã đọc.
- **[Suy luận kiến trúc]**: Chia sẻ dữ liệu qua bảng DB, Redis key, hoặc tương tác gián tiếp qua UI/Router chưa có lời gọi code trực tiếp.

### 2.1. Phân hệ Quản lý Kế hoạch Lớp & Phiên bản Giáo án (`FEAT-P5-07`)

| Thành phần thay đổi | Thành phần gọi đến (Callers) | Thành phần được gọi (Callees) | Bằng chứng & Dòng code |
|---|---|---|---|
| `IClassCurriculumService` | `LessonPlansController` [Xác nhận được], `ServiceCollectionExtensions` (DI `AddScoped`) [Xác nhận được] | Không (Interface) | `LessonPlansController.cs:17,22`; `ServiceCollectionExtensions.cs:89` |
| `ClassCurriculumService` | `ServiceCollectionExtensions` (DI) [Xác nhận được] | `ParishDbContext`, `ILogger<ClassCurriculumService>`, `ClosedXML`, `PdfPig`, hệ thống file `PrivateUploads/curriculums` [Xác nhận được] | `ClassCurriculumService.cs:17-32` |
| `LessonPlansController` (`ParseCurriculumFile`, `SaveCurriculum`, `RollbackCurriculum`, `GetVersionDetail`, `DownloadCurriculumVersion`, `ExportCurrentCurriculumExcel`) | `Views/LessonPlans/Index.cshtml` (JS `fetch` / links) [Xác nhận được] | `IClassCurriculumService`, `ParishDbContext` (`Classes`, `ClassTeachers`, `ClassCurriculumVersions`, `ClassCurriculumDocuments`) [Xác nhận được] | `LessonPlansController.cs:150-303` |
| `ClassCurriculumDocuments`, `ClassCurriculumVersions`, `ClassLessonPlans` | `ClassCurriculumService`, `LessonPlansController`, `WebApplicationExtensions.SeedSampleCurriculumData` [Xác nhận được] | `Classes`, `AcademicYears`, `Users` qua FK [Xác nhận được] | `ParishDbContext.cs:321-381`; `WebApplicationExtensions.cs:89-93` |
| `ClassLessonPlans` | `RedisAttendanceService` / các màn hình theo dõi bài học [Suy luận kiến trúc] | `ClassCurriculumDocuments` (`CurriculumDocumentId`) [Xác nhận được] | `ClassLessonPlan.cs:18-19`; `ParishDbContext.cs:352-356` |

### 2.2. Phân hệ Điểm danh & Máy quét QR HUD (`FEAT-P5-04`)

| Thành phần thay đổi | Thành phần gọi đến (Callers) | Thành phần được gọi (Callees) | Bằng chứng & Dòng code |
|---|---|---|---|
| `AttendanceController.UndoScanCheckIn` | `Views/Attendance/ScanQr.cshtml` (`fetch('/Attendance/UndoScanCheckIn')`) [Xác nhận được] | `IRedisAttendanceService.UndoScanCheckInAsync` [Xác nhận được] | `AttendanceController.cs:234-245` |
| `RedisAttendanceService.ProcessQrCheckInViaRedisAsync` | `AttendanceController.ScanCheckIn` [Xác nhận được] | `ICacheService`, `ParishDbContext` (`AcademicYears`, `Enrollments`, `StudentCodeHistories`, `Attendances`), `AttendanceTimeHelper` [Xác nhận được] | `RedisAttendanceService.cs:382-465` |
| `RedisAttendanceService.UndoScanCheckInAsync` | `AttendanceController.UndoScanCheckIn` [Xác nhận được] | `ICacheService` (Redis ClassSheet + Dirty Index), `ParishDbContext` (`Attendances`, `MassCampaignRecords`) [Xác nhận được] | `RedisAttendanceService.cs:692-785` |
| `AttendanceController.GetOfflineStudentCache` | `wwwroot/js/offline-attendance.js` [Suy luận kiến trúc] | `ParishDbContext` (`Enrollments`, `StudentCodeHistories`) [Xác nhận được] | `AttendanceController.cs:1456-1482` |
| `ParishOfflineEngine.undoOfflineQrScan` | `Views/Attendance/ScanQr.cshtml` [Xác nhận được] | IndexedDB `STORE_PENDING` trong `wwwroot/js/offline-attendance.js` [Xác nhận được] | `offline-attendance.js:620-637` |

### 2.3. Phân hệ Tuyển sinh Công khai (`PublicRegistration`)

| Thành phần thay đổi | Thành phần gọi đến (Callers) | Thành phần được gọi (Callees) | Bằng chứng & Dòng code |
|---|---|---|---|
| `PublicRegistrationController.Resubmit` & `SubmitResubmit` | `Views/PublicRegistration/CheckStatus.cshtml` (link tới `Resubmit`), `Views/PublicRegistration/Resubmit.cshtml` (form POST `SubmitResubmit`) [Xác nhận được] | `ParishDbContext` (`AcademicYears`, `StudentRegistrationRequest`), `IWebHostEnvironment` (`PrivateUploads/baptism_certs`) [Xác nhận được] | `PublicRegistrationController.cs:209-405` |

### 2.4. Phân hệ Tài khoản, Phân quyền & Bảo mật (`UserAccount` & `Security`)

| Thành phần thay đổi | Thành phần gọi đến (Callers) | Thành phần được gọi (Callees) | Bằng chứng & Dòng code |
|---|---|---|---|
| `AccountController.Login` (`AddDays(14)`) & `ServiceCollectionExtensions.AddHybridAuthentication` (`FromDays(14)`) | Người dùng đăng nhập qua `Views/Account/Login.cshtml` [Xác nhận được] | `HttpContext.SignInAsync`, `ITokenService.GenerateRefreshToken` [Xác nhận được] | `AccountController.cs:141-158`; `ServiceCollectionExtensions.cs:154`; `TokenService.cs:79` |
| `AccountController.Profile(long? id)` & `UpdateProfile` | `Views/Users/Index.cshtml`, `Views/Shared/_LayoutMobileHeader.cshtml` [Xác nhận được] | `ParishDbContext` (`Users`, `ClassTeachers`) [Xác nhận được] | `AccountController.cs:295-492` |
| `UsersController.Create` & `ResetPassword` | `Views/Users/Index.cshtml` [Xác nhận được] | `ParishDbContext.Users`, `IPasswordHasherService` [Xác nhận được] | `UsersController.cs:87-230` |
| `ServiceCollectionExtensions.AddStrictSecurityConfigurations` (`PersistKeysToFileSystem`) | `Program.cs` [Suy luận kiến trúc], Docker volume `/app/dataprotection-keys` [Xác nhận được] | `Microsoft.AspNetCore.DataProtection` [Xác nhận được] | `ServiceCollectionExtensions.cs:250-258`; `Dockerfile:18`; `docker-compose.yml:28` |

### 2.5. Hạ tầng Cache, PWA & Navigation

| Thành phần thay đổi | Thành phần gọi đến (Callers) | Thành phần được gọi (Callees) | Bằng chứng & Dòng code |
|---|---|---|---|
| `RedisCacheService` (Circuit Breaker 30s) | `RedisAttendanceService`, `HomeController`, `AcademicYearsController`, `GradesController`, các service dùng `ICacheService` [Xác nhận được] | `IDistributedCache` (`StackExchange.Redis`), `IMemoryCache` (`_memoryFallback`) [Xác nhận được] | `RedisCacheService.cs:15-99` |
| `PwaController.Offline` (`return View("Index")`) | Service Worker / Client khi truy cập `GET /Offline` [Xác nhận được] | `Views/Pwa/Index.cshtml` [Xác nhận được] | `PwaController.cs:21-24` |
| `FeatureNavigationService` (`{class_id}` replacement) | `HomeController.Index` [Xác nhận được] | `ParishDbContext` (`FeaturePermissionRules`, `Classes`) [Xác nhận được] | `FeatureNavigationService.cs:212-265` |
| `StudentsController.Create` (`AcademicYearId`) | `Views/Students/Index.cshtml` [Xác nhận được] | `ParishDbContext` (`Students`, `Enrollments`) [Xác nhận được] | `StudentsController.cs:242-248` |

---

## 3. Các Feature và Module Bị Ảnh hưởng (Có Bằng chứng)

| Module / Feature | Mức độ Ảnh hưởng | Tóm tắt Tác động |
|---|---|---|
| **Module `LessonPlans` (`FEAT-P5-07`)** | **HIGH** | Bổ sung toàn bộ luồng đọc file đa định dạng, biên tập tuần, lưu version kèm xuất `.xlsx`, xem lịch sử & rollback; thêm 2 bảng và cập nhật `ClassLessonPlans`. |
| **Module `Attendance` (`FEAT-P5-04`)** | **HIGH** | Bổ sung endpoint `UndoScanCheckIn`, thay đổi `QrScanResponseDto`, thêm nhận diện & cache alias cho thẻ năm cũ qua `StudentCodeHistories`, sửa lỗi HTML modal/trọng số tại `Attendance/Index.cshtml`. |
| **Module `PublicRegistration`** | **MEDIUM** | Bổ sung luồng `Resubmit` / `SubmitResubmit` cho phép phụ huynh cập nhật và nộp lại hồ sơ tuyển sinh bị từ chối. |
| **Module `UserAccount` & `Security`** | **HIGH** | Nâng thời hạn Cookie & Refresh Token lên 14 ngày; mở rộng quyền `Profile`/`UpdateProfile` cho Admin/BĐH/Cha; thay đổi validation mật khẩu ở `CreateUserRequestDto`/`ResetPasswordRequestDto`; lưu bền Data Protection keys. |
| **Module `Cache` (`RedisCacheService`)** | **HIGH** | Bổ sung Circuit Breaker 30 giây giúp toàn bộ API dùng `ICacheService` (như `PreWarmRedisCache`, `StaffStudentLookup`) không bị treo khi Redis ngoại tuyến. |
| **Module `Common` / `Mobile UI` & `PWA`** | **MEDIUM** | Tinh chỉnh `_LayoutMobileBottomNav.cshtml`, `_LayoutMobileHeader.cshtml`, `mobile-layout.css`, `Lookup/Result.cshtml`, `AccessDenied.cshtml`, sửa lỗi HTTP 500 tại `PwaController.Offline`. |
| **Modules `Student`, `Grades`, `Promotion`** | **LOW - MEDIUM** | `StudentsController.Create` gán đúng `AcademicYearId` cho `Enrollment`; sửa lỗi cấu trúc Razor/JS trong `Grades/Index.cshtml`, `Promotion/Index.cshtml`, `Students/Index.cshtml`. |

---

## 4. Thông tin Thiếu & Cần Xác minh (`NEEDS_VERIFICATION`)

1. **Tình trạng Schema Database trên PostgreSQL đối với `ClassCurriculumDocuments` và `ClassCurriculumVersions` (`NEEDS_VERIFICATION`):**
   - Thư mục `ManagerStudentCaltholic/Migrations/` hiện không chứa tệp EF Core Migration nào. Cần kiểm tra môi trường database đích đã tạo 2 bảng mới và cột mới trên `ClassLessonPlans` (`CurriculumDocumentId`, `WeekNumber`, `Semester`) chưa trước khi kích hoạt tính năng `FEAT-P5-07` hoặc `WebApplicationExtensions.SeedSampleCurriculumData`.
2. **File Untracked `nginx/default.conf` và `script/seed-curriculum-data.sql` (`NEEDS_VERIFICATION`):**
   - Đã xuất hiện trong working tree nhưng chưa commit vào Git HEAD (`2a3263d`). Cần xác nhận chủ sở hữu cấu hình cuối cùng trước khi đưa lên môi trường triển khai Docker/Production.
3. **Hành vi thực tế của Client khi nhấn Logout vs `ExpiresUtc` 14 ngày (`HIGH`):**
   - Đã xác nhận `ExpiresUtc` cookie (khi `RememberMe`) và `RefreshTokenExpirationDays` cùng là `14` ngày; tuy nhiên `AccessTokenExpirationMinutes` giữ ở mức `30` phút (`appsettings.json:24`) nên JWT client phụ thuộc vào cơ chế gọi `/Account/RefreshToken` để gia hạn tối đa 14 ngày.
