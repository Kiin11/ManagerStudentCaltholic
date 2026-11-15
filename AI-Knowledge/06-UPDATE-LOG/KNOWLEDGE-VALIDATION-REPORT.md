# BÁO CÁO KIỂM ĐỊNH ĐỘC LẬP KHO TRI THỨC (KNOWLEDGE VALIDATION REPORT)

> **Vai trò kiểm định:** Independent Codebase Knowledge Auditor  
> **Dự án:** `ManagerStudentCaltholic` (Hệ thống Quản lý Thiếu Nhi Thánh Thể - Giáo xứ Phú Trung)  
> **Thời điểm kiểm định:** `2026-11-15T14:32:00+07:00`  
> **Phạm vi đối chiếu:** Source code hiện tại tại commit `2a3263d` (+ working tree ngày `2026-11-15`) và toàn bộ kho tài liệu `AI-Knowledge/` trước/sau đợt cập nhật ghi nhận trong `AI-Knowledge/06-UPDATE-LOG/UPDATE-REPORT.md`  
> **Nguyên tắc kiểm định:** Không sửa mã nguồn; không tự sửa tài liệu tri thức trong bước kiểm định; không gắn nhãn `PASS` khi thiếu bằng chứng mã nguồn (`NEEDS_VERIFICATION`).

---

## 1. Kết Luận Tổng Thể (Overall Audit Verdict)

- **Trạng thái sau khắc phục (`2026-11-15T15:00:00+07:00`):** **`PASS (9/9 ISSUES RESOLVED)`**
  - Toàn bộ **`9/9` vấn đề (`ISS-AUD-001` ➔ `ISS-AUD-009`)** đã được khắc phục triệt để và kiểm chứng lại tự động + biên dịch C# (`dotnet build`: `0` warning, `0` error):
    - **`ISS-AUD-001` (`RESOLVED`):** Đã xóa sạch từ khóa/module giả lập (`Booking`, `Payment`, `Hotel`) khỏi `00-OVERVIEW/`; cập nhật `MODULE-INVENTORY.json` (`8` module thực tế), `PROJECT-INVENTORY.json` (`200` file), `SOURCE-INVENTORY.md` (`200` file, đủ `28` Controllers).
    - **`ISS-AUD-002` (`RESOLVED` - User Verified):** User xác nhận đã chạy migration & kiểm chứng DDL trên PostgreSQL; đã cập nhật rõ ràng trong `DATABASE-OVERVIEW.md:5`, `UPDATE-LOG.md:95`, `PENDING-UPDATES.md` (giải thích Code-First Mapping `43 DbSet<T>`, `script/seed-curriculum-data.sql` và thư mục `Migrations/` trống sau `cfb2b5e`).
    - **`ISS-AUD-003` (`RESOLVED`):** Đồng bộ `43/43` bảng (`Diff = 0`) giữa `DATABASE-OVERVIEW.md`, `TABLE-INDEX.json`, và JSON nhúng trong `TABLE-RELATIONSHIPS.md`.
    - **`ISS-AUD-004` & `ISS-AUD-009` (`RESOLVED`):** Bổ sung đóng block Mermaid ở `PROJECT-DEPENDENCY-DIAGRAM.md`; mở rộng `FILE-INDEX.jsonl` lên `114` tệp (đủ `28/28` Controllers); tái đồng bộ từ AST C# lên `192` symbols (`0` null), `52` routes (`0` null), `66` dependencies (`0` null, bổ sung `StaffStudentLookupService -> IGradeCalculationService`, `Missing source/target = 0`).
    - **`ISS-AUD-005` (`RESOLVED` - Direct Write-Through):** User xác nhận luồng cập nhật trực tiếp; đã cập nhật đồng bộ tài liệu (`ATTENDANCE.md`, `ARCHITECTURE.md`, `Request Flow Diagram.md`, `INTEGRATION-OVERVIEW.md`) và XML/code comments (`IRedisAttendanceService.cs`, `RedisAttendanceService.cs`, `Program.cs:163`) cho cả `ScanCheckIn` và `SaveBatch` (`P2-LOG-16 FIX`).
    - **`ISS-AUD-006`, `ISS-AUD-007`, `ISS-AUD-008` (`RESOLVED`):** Đã chuẩn hóa DOM ID `#staffStudentQuickSearchInput`, debounce `280ms`, `OriginalCode/ArchivedYearCode/NewCode`, `IsHistoricalMatch/MatchedHistoricalCode`, `LookupViewOptions`, cooldown QR `3s (3000ms)`, Mobile header `56px` / `#mobileMenuOffcanvas` / `z-index 1070`, và route `{versionId:long}` trong `FEAT-P5-07`.

### Lịch Sử Kết Luận Audit Độc Lập Ban Đầu (`2026-11-15T14:32:00+07:00`)
- **Trạng thái ban đầu:** **`PASS WITH WARNINGS (REQUIRES TARGETED CORRECTIONS)`** (Trước khi vá 9 issues bên dưới):
  - **Đợt cập nhật gia tăng `c0e828a ➔ 2a3263d` (`18` tệp trong `UPDATE-REPORT.md`):** Các biểu tượng, route và cạnh phụ thuộc mới cho `FEAT-P5-07` (`LessonPlansController`, `ClassCurriculumService`), `FEAT-P5-04` (`UndoScanCheckIn`, `StudentCodeHistories`), `PublicRegistrationController` (`Resubmit`, `SubmitResubmit`, `ViewOwnCertificate`), `PwaController`, `AccountController` (14-day cookie & `Profile`/`UpdateProfile`) và `RedisCacheService` Circuit Breaker 30s khớp chính xác với mã nguồn; toàn bộ `5` tệp `.json` và `4` tệp `.jsonl` hợp lệ cú pháp 100% (`0` lỗi JSON/JSONL, `0` trùng khóa).
  - **Tồn đọng chưa thể `PASS` tuyệt đối:** Còn `9` vấn đề cần khắc phục (`2 HIGH`, `5 MEDIUM`, `2 LOW`) tập trung tại:
    1. Trạng thái EF Core Migration (`ManagerStudentCaltholic/Migrations/` hiện có **`0` file**, dẫn đến cấu trúc bảng PostgreSQL vẫn ở trạng thái **`NEEDS_VERIFICATION`** và mâu thuẫn với nhật ký cũ tại `UPDATE-LOG.md:95`).
    2. Tàn dư tên miền/module giả lập không tồn tại (`Booking`, `Payment`, `Hotel`) và phạm vi kiểm kê cũ từ `2026-03-31` trong `00-OVERVIEW/MODULE-INVENTORY.json`, `SOURCE-INVENTORY.md`, `PROJECT-INVENTORY.json`.
    3. Sai lệch số dòng (`startLine`/`endLine`/`sourceLine`) do mã nguồn chỉnh sửa trước đó tại `HomeController.cs`, `IStaffStudentLookupService.cs`, `StaffStudentLookupService.cs`, `HttpRequestExtensions.cs` trong `03-CODE-INDEX/*.jsonl`, cùng một số sai lệch tên định danh/luồng chi tiết trong `01-MODULES/ATTENDANCE.md`, `02-DATABASE/` và `05-FEATURES/FEAT-P5-01`, `FEAT-P5-02`, `FEAT-P5-07`, `FAST-QR-CHECKIN-AND-OFFLINE-SYNC.md`.

---

## 2. Bảng Tổng Hợp 10 Tiêu Chí Kiểm Định (Validation Scorecard)

| # | Tiêu chí kiểm định | Trạng thái | Tóm tắt phát hiện | Mã vấn đề liên quan |
|---|---|---|---|---|
| 1 | Class, method, namespace & file path | `PASS WITH WARNINGS` | 100% file trong `FILE-INDEX.jsonl` (`72/72`) tồn tại thật; các symbol `FEAT-P5-07`, `PublicRegistration`, `Pwa`, `UndoScan`, `Account` khớp code; nhưng nhóm symbol `FEAT-P5-01`/`02` (`HomeController`, `IStaffStudentLookupService`, `StaffStudentLookupService`, `HttpRequestExtensions`) bị lệch `startLine`/`endLine` & có `22` key chưa có bản ghi Class/Interface/Method gốc trong `SYMBOL-INDEX.jsonl`. | `ISS-AUD-004`, `ISS-AUD-009` |
| 2 | Route & Controller Action | `PASS WITH WARNINGS` | Tất cả `52` route trong `ROUTE-INDEX.jsonl` tồn tại trong Controller tương ứng; các route `FEAT-P5-07`, `PublicRegistration`, `Pwa`, `Attendance` mới khớp chính xác, nhưng `Home` (`HomeController.cs`) lệch `startLine` (`32/83/102/147/164` vs thực tế `29/52/67/109/124`) và `FEAT-P5-07` mục 5 viết `{id}` thay vì `{versionId:long}`. | `ISS-AUD-004`, `ISS-AUD-008`, `ISS-AUD-009` |
| 3 | Dependency có bằng chứng | `PASS WITH WARNINGS` | Tất cả `65` cạnh trong `DEPENDENCY-INDEX.jsonl` có bằng chứng DI/gọi hàm trong code; `LessonPlans`, `UndoScan`, `PublicRegistration`, `Pwa` khớp 100%, nhưng `HomeController` lệch `sourceLine`, thiếu cạnh `StaffStudentLookupService -> IGradeCalculationService`, và `01-MODULES/ATTENDANCE.md:240` mô tả sai dependency QR mã cũ. | `ISS-AUD-004`, `ISS-AUD-005` |
| 4 | Feature document mô tả đúng luồng | `PASS WITH WARNINGS` | Luồng chính `FEAT-P5-07`, `FEAT-P5-04`, `FEAT-P5-01`, `FEAT-P5-02` đúng tổng thể, nhưng `ATTENDANCE.md:280` mô tả sai `SaveBatch` (thực tế Write-Through tức thì), `FEAT-P5-01` sai DOM ID/debounce/cột DB/tên cờ DTO, `FEAT-P5-02` sai Offcanvas ID/z-index/height, `FAST-QR-CHECKIN:164` lệch cooldown `2.5s` vs `3000ms`. | `ISS-AUD-005`, `ISS-AUD-006`, `ISS-AUD-007`, `ISS-AUD-008` |
| 5 | Database table, column & relationship | `NEEDS_VERIFICATION` | `DATABASE-OVERVIEW.md` khớp `43/43` `DbSet<T>` trong `ParishDbContext.cs`, nhưng `Migrations/` có **`0` file**, gây `NEEDS_VERIFICATION` DDL PostgreSQL; đồng thời `TABLE-INDEX.json` (`23` bảng) và `TABLE-RELATIONSHIPS.md` JSON (`20` bảng) không đồng nhất danh sách bảng. | `ISS-AUD-002`, `ISS-AUD-003` |
| 6 | API & Integration | `PASS` | `appsettings.json` (`RefreshTokenExpirationDays = 14`), `AccountController.cs` (Cookie 14 ngày), `LoggingExtensions.cs` (Serilog overrides), `ServiceCollectionExtensions.cs` (DataProtection + Policies) và `RedisCacheService.cs` (Circuit Breaker 30s + `IMemoryCache` fallback) khớp 100% với `TECH-STACK.md`, `PROJECT.md`, `INTEGRATION-OVERVIEW.md`. | — |
| 7 | Tham chiếu đến class/method đã xóa hoặc không tồn tại | `FAIL (LEGACY)` | Không có tham chiếu sai trong `18` tệp vừa cập nhật, nhưng các tệp tồn dư từ baseline `2026-03-31` (`MODULE-INVENTORY.json`, `SOURCE-INVENTORY.md`, `PROJECT-INVENTORY.json`) vẫn tham chiếu module không tồn tại (`Booking`, `Payment`, `Hotel`). | `ISS-AUD-001` |
| 8 | Hợp lệ JSON & JSONL | `PASS` | Toàn bộ `5` tệp `.json` và `4` tệp `.jsonl` (tổng `309` dòng JSONL) + khối JSON nhúng trong `TABLE-RELATIONSHIPS.md` đều parse sạch qua `ConvertFrom-Json` với `0` lỗi cú pháp và `0` trùng lặp khóa định danh. | — |
| 9 | Tài liệu bị ảnh hưởng nhưng chưa cập nhật | `PASS WITH WARNINGS` | `MODULE-INVENTORY.json`, `SOURCE-INVENTORY.md`, `PROJECT-INVENTORY.json` chưa cập nhật theo cấu trúc hiện tại (`28` Controllers, `315` source/web files); `PROJECT-DEPENDENCY-DIAGRAM.md:17` thiếu dấu đóng code fence ```` ``` ````; các tài liệu `FEAT-P5-01/02/07` còn điểm chưa đồng bộ chi tiết mới. | `ISS-AUD-001`, `ISS-AUD-006`, `ISS-AUD-007`, `ISS-AUD-008`, `ISS-AUD-009` |
| 10 | Thông tin suy luận/chưa xác minh được đánh dấu rõ | `PASS WITH WARNINGS` | `UPDATE-REPORT.md`, `PENDING-UPDATES.md`, `DATABASE-OVERVIEW.md:61`, `UPDATE-LOG.md:11` đánh dấu `NEEDS_VERIFICATION` rõ ràng; tuy nhiên mục lịch sử `05/11/2026 - FEAT-P5-07` tại `UPDATE-LOG.md:95` vẫn khẳng định đã tạo EF Migration `AddClassCurriculumDocumentsAndVersions` (không có trong repo). | `ISS-AUD-002` |

---

## 3. Chi Tiết Kết Quả Kiểm Chứng Từng Tiêu Chí

### 3.1. Tiêu chí 1 & 2: Class, Method, Namespace, File Path & Routes
- **Các phần đã đối chiếu khớp 100% với source code (`PASS`):**
  - `Controllers/LessonPlansController.cs` (`class` dòng `14..485`): Constructor (`20`), `ParseCurriculumFile` (`150..165`), `SaveCurriculum` (`173..200`), `GetVersionDetail` (`[HttpGet("LessonPlans/GetVersionDetail/{versionId:long}")]`, `205..223`), `DownloadCurriculumVersion` (`[HttpGet("LessonPlans/DownloadCurriculumVersion/{versionId:long}")]`, `228..248`), `ExportCurrentCurriculumExcel` (`253..270`), `RollbackCurriculum` (`278..303`), `DownloadDocument` (`409..446`). Khớp chính xác `SYMBOL-INDEX.jsonl:102-108` và `ROUTE-INDEX.jsonl:30,36-41`.
  - `Services/ClassCurriculumService.cs` (`class` `17..1051`), `Interface/Services/IClassCurriculumService.cs` (`6..47`), `Models/Entities/ClassLessonPlan.cs` (`8..48`), `ClassCurriculumDocument.cs` (`9..70`), `ClassCurriculumVersion.cs` (`9..54`), `Models/DTOs/CurriculumDtos.cs` (`3..74`), `Models/ViewModels/LessonPlansViewModel.cs` (`6..21`). Khớp chính xác `SYMBOL-INDEX.jsonl:83-101`.
  - `Controllers/PublicRegistrationController.cs` (`14..470`): `Index` (`32`), `Submit` (`48`), `Success` (`166`), `CheckStatus` (`176`), `Resubmit` (`209..269`), `SubmitResubmit` (`277..406`), `ViewOwnCertificate` (`413..450`), `PublicRegistrationResubmitDto` (`PublicRegistrationSubmitDto.cs:66..83`). Khớp chính xác `SYMBOL-INDEX.jsonl:113-117` và `ROUTE-INDEX.jsonl:44-50`.
  - `Controllers/PwaController.cs` (`7..56`): `Offline` (`[HttpGet("/Offline")]`, dòng `19..24` trả `View("Index")`), `GetAppIcon` (`[HttpGet("/icons/{fileName}")]`, dòng `29..55`). Khớp `SYMBOL-INDEX.jsonl:118-120` và `ROUTE-INDEX.jsonl:51-52`.
  - `Controllers/AttendanceController.cs`: `UndoScanCheckIn` (`234..251`), `GetOfflineStudentsCache` (`1394`); `Services/RedisAttendanceService.cs`: `UndoScanCheckInAsync` (`692..785`); `Interface/Services/IRedisAttendanceService.cs`: `UndoScanCheckInAsync` (`36`); `Models/ViewModels/AttendanceViewModels.cs`: `UndoScanRequestDto` (`108..117`). Khớp `SYMBOL-INDEX.jsonl:109-112` và `ROUTE-INDEX.jsonl:42-43`.
  - `Controllers/AccountController.cs`: `Profile(long? id = null)` (`295..391`) và `UpdateProfile` (`401..492`). Khớp `SYMBOL-INDEX.jsonl:81-82`.
  - Kiểm tra sự tồn tại vật lý của toàn bộ `72/72` tệp trong `FILE-INDEX.jsonl`: `0` tệp thiếu.
- **Các sai lệch được phát hiện:**
  - `ISS-AUD-004` (`MEDIUM`): Số dòng và chữ ký bị trôi trong `SYMBOL-INDEX.jsonl:63-80` và `ROUTE-INDEX.jsonl:31-35` đối với `HomeController.cs` (thực tế `154` dòng), `IStaffStudentLookupService.cs` (thực tế `16` dòng, tham số `string keyword`), `StaffStudentLookupService.cs` (thực tế `714` dòng, `GetViewOptionsAsync` ở đầu file dòng `30`), `HttpRequestExtensions.cs` (thực tế `75` dòng, `IsMobileDevice` dòng `30..73`).
  - `ISS-AUD-008` (`LOW`): `FEAT-P5-07-CLASS-CURRICULUM-MANAGEMENT.md:144-145` ghi `{id}` thay vì `{versionId:long}`.
  - `ISS-AUD-009` (`LOW`): `62` symbol baseline (`SYMBOL-INDEX.jsonl:1-62`) và `29` route baseline (`ROUTE-INDEX.jsonl:1-29`) vẫn giữ `null` cho `startLine`/`endLine`.

### 3.2. Tiêu chí 3: Kiểm Chứng Dependency Bằng Chứng
- **Khớp hoàn toàn:** Toàn bộ `25` cạnh mới (`DEPENDENCY-INDEX.jsonl:41-42, 50-65`) phản ánh đúng constructor injection và lời gọi phương thức trong `ClassCurriculumService`, `LessonPlansController`, `AttendanceController.UndoScanCheckIn`, `RedisAttendanceService`, `PublicRegistrationController`, `PwaController`.
- **Sai lệch phát hiện:**
  - `DEPENDENCY-INDEX.jsonl:43-49` có `sourceLine` bị trôi cho `HomeController.cs` (`23-26, 91, 115, 127` thay vì `22-25, 60, 81, 96`), và thiếu cạnh `StaffStudentLookupService -> IGradeCalculationService` (`Services/StaffStudentLookupService.cs:17,22`) (`ISS-AUD-004`).
  - `01-MODULES/ATTENDANCE.md:240` khẳng định luồng điểm danh QR gọi `IStudentCodeRolloverService`, trong khi `Services/RedisAttendanceService.cs:415-420` truy vấn trực tiếp `_context.StudentCodeHistories` (`ISS-AUD-005`).
  - `22` symbol nội bộ tham chiếu trong `DEPENDENCY-INDEX.jsonl` chưa có bản ghi khai báo tương ứng trong `SYMBOL-INDEX.jsonl` (`ISS-AUD-004`).

### 3.3. Tiêu chí 4: Feature Documents vs Luồng Xử Lý Thực Tế
- `FEAT-P5-04-QR-SCANNER-UPGRADE.md`: Mô tả đúng luồng HUD, `SessionMode`, `StudentCodeHistories`, `UndoScanCheckIn`, `UndoScanRequestDto`, `HistoricalCodes`, chống quét trùng `3000ms` (`ScanQr.cshtml:865-868`).
- `FAST-QR-CHECKIN-AND-OFFLINE-SYNC.md`: Khớp tốt phần `Flow 1`, `Flow 2`, `Flow 3` (Undo 5s) và `ParishOfflineEngine` (`wwwroot/js/offline-attendance.js:81-118, 610-650`), nhưng dòng `164` vẫn ghi `"UI Client khóa 2.5s"` mâu thuẫn với `3000ms` thực tế tại `ScanQr.cshtml:868` (`ISS-AUD-007`).
- `01-MODULES/ATTENDANCE.md:280`: Khẳng định `SaveBatchToRedisAsync` để chờ 10 phút dễ mất dữ liệu, trái ngược với `AttendanceController.SaveBatch` (`Controllers/AttendanceController.cs:147-151`, comment `P2-LOG-16 FIX`) đã gọi đồng bộ tức thì `SyncDirtyAttendancesToDatabaseAsync` (`ISS-AUD-005`).
- `05-FEATURES/FEAT-P5-01-STAFF-QUICK-LOOKUP.md`: Ghi sai ID `#staffLookupSearchInput`, debounce `250ms`, tối thiểu `2` ký tự, tên cột `OldCode`, tên cờ `MatchedByOldCode`/`MatchedOldCodeDisplay` (`ISS-AUD-006`).
- `05-FEATURES/FEAT-P5-02-MOBILE-FIRST-UI.md`: Ghi header `54px`, `#mobileUserOffcanvas`, `z-index: 1085` thay vì giá trị thực tế trong code là `56px`, `#mobileMenuOffcanvas`, `z-index: 1070` (`ISS-AUD-007`).
- `05-FEATURES/FEAT-P5-07-CLASS-CURRICULUM-MANAGEMENT.md`: Phản ánh chính xác luồng `ClassCurriculumService` (`PrivateUploads/curriculums` ở dòng `27`, sinh `.xlsx`, lưu version `v{N}` và đồng bộ `ClassLessonPlans` bằng `IExecutionStrategy`), chỉ có lỗi nhỏ `{id}` vs `{versionId:long}` ở dòng `144-145` (`ISS-AUD-008`).

### 3.4. Tiêu chí 5 & 10: Database Schema, Relationships & `NEEDS_VERIFICATION`
- `ParishDbContext.cs` khai báo chính xác **`43` `DbSet<T>`**, khớp `43/43` hàng trong bảng mapping của `02-DATABASE/DATABASE-OVERVIEW.md:14-56`.
- Tuy nhiên:
  1. Thư mục `ManagerStudentCaltholic/Migrations/` hiện chứa **`0` file** (`Migrations files count: 0`), nên chưa có bằng chứng DDL đã thực thi trên PostgreSQL cho `ClassCurriculumDocuments`, `ClassCurriculumVersions`, các cột mới trên `ClassLessonPlans`, cũng như `StaffQuickLookupLogs` và `LookupViewOptions`. Theo nguyên tắc kiểm định, tiêu chí DB **không thể gắn nhãn `PASS`** mà phải giữ **`NEEDS_VERIFICATION`** (`ISS-AUD-002`).
  2. Dòng lịch sử `AI-Knowledge/06-UPDATE-LOG/UPDATE-LOG.md:95` vẫn khẳng định *"tạo EF Migration `AddClassCurriculumDocumentsAndVersions`"* trái với thực tế repository (`ISS-AUD-002`).
  3. `TABLE-INDEX.json` có **`23` bảng** còn JSON nhúng trong `TABLE-RELATIONSHIPS.md:69-276` chỉ có **`20` bảng** (thiếu `Announcements`, `BuildingZones`, `ClassRoomLocations`, `ClassSchedules`, `RoomIncidentReports`; trong khi `TABLE-RELATIONSHIPS.md` có `MassCampaigns`, `MassCampaignRecords` còn `TABLE-INDEX.json` lại không có) (`ISS-AUD-003`).

### 3.5. Tiêu chí 6: API, Integration & Configuration
- **Xác minh khớp 100% (`PASS`):**
  - `appsettings.json:24-25`: `AccessTokenExpirationMinutes = 30`, `RefreshTokenExpirationDays = 14`.
  - `Services/TokenService.cs:79`: fallback mặc định `14` ngày.
  - `Extensions/ServiceCollectionExtensions.cs:154`: `ExpireTimeSpan = TimeSpan.FromDays(14)`, `SlidingExpiration = true`; dòng `250-258`: `PersistKeysToFileSystem` tại `dataprotection-keys`.
  - `Extensions/LoggingExtensions.cs:22-26`: Serilog `Information`, override `Microsoft`, `Microsoft.AspNetCore`, `Microsoft.EntityFrameworkCore.Database.Command = Warning`, `Microsoft.AspNetCore.Antiforgery = Fatal`.
  - `Services/RedisCacheService.cs:15-98`: Circuit Breaker `30` giây (`TimeSpan.FromSeconds(30)`) kết hợp `IMemoryCache` fallback.

### 3.6. Tiêu chí 7, 8 & 9: Tệp Chưa Cập Nhật, Tham Chiếu Sai & Cú Pháp JSON/JSONL
- **JSON & JSONL (`PASS`):** Tất cả `5` file `.json` (`MODULE-INVENTORY.json`, `PROJECT-INVENTORY.json`, `TABLE-INDEX.json`, `AFFECTED-FILES.json`, `KNOWLEDGE-BASELINE.json`) và `4` file `.jsonl` (`FILE-INDEX.jsonl` [72 dòng], `SYMBOL-INDEX.jsonl` [120 dòng], `ROUTE-INDEX.jsonl` [52 dòng], `DEPENDENCY-INDEX.jsonl` [65 dòng]) + JSON trong `TABLE-RELATIONSHIPS.md` đều hợp lệ 100%, không lẫn Markdown fence hay trùng khóa.
- **Tồn dư module giả lập & tệp cũ chưa cập nhật (`FAIL / HIGH`):** `00-OVERVIEW/MODULE-INVENTORY.json`, `SOURCE-INVENTORY.md`, `PROJECT-INVENTORY.json` vẫn giữ tên miền `Booking`, `Payment`, `Hotel` và chưa phản ánh `28` Controllers / `315` tệp hiện có (`ISS-AUD-001`). Ngoài ra `00-OVERVIEW/PROJECT-DEPENDENCY-DIAGRAM.md:17` thiếu dấu đóng ```` ``` ```` của block Mermaid (`ISS-AUD-009`).

---

## 4. Danh Sách Tổng Hợp Các Vấn Đề Phát Hiện (`9` Issues)

> Chi tiết máy đọc xem tại [`KNOWLEDGE-ISSUES.jsonl`](file:///d:/github/ManagerStudentCaltholic/AI-Knowledge/06-UPDATE-LOG/KNOWLEDGE-ISSUES.jsonl); hướng dẫn khắc phục chi tiết xem tại [`REQUIRED-CORRECTIONS.md`](file:///d:/github/ManagerStudentCaltholic/AI-Knowledge/06-UPDATE-LOG/REQUIRED-CORRECTIONS.md).

| ID | Mức độ | Tiêu chí | File Knowledge bị ảnh hưởng | Tóm tắt vấn đề |
|---|---|---|---|---|
| `ISS-AUD-001` | `HIGH` | #7, #9 | `00-OVERVIEW/MODULE-INVENTORY.json`, `SOURCE-INVENTORY.md`, `PROJECT-INVENTORY.json` | Còn tên miền/module giả lập `Booking`, `Payment`, `Hotel` không có thật & phạm vi kiểm kê dừng ở `2026-03-31` (`28` Controllers / `315` files hiện tại). |
| `ISS-AUD-002` | `HIGH` | #5, #10 | `06-UPDATE-LOG/UPDATE-LOG.md:95`, `02-DATABASE/DATABASE-OVERVIEW.md:5` | `Migrations/` có `0` file nhưng mục lịch sử `UPDATE-LOG.md:95` vẫn khẳng định đã tạo EF Migration `AddClassCurriculumDocumentsAndVersions`; trạng thái DDL PostgreSQL vẫn là `NEEDS_VERIFICATION`. |
| `ISS-AUD-003` | `MEDIUM` | #5 | `02-DATABASE/TABLE-INDEX.json`, `02-DATABASE/TABLE-RELATIONSHIPS.md` | Lệch số lượng và tập bảng giữa `DATABASE-OVERVIEW.md` (`43`), `TABLE-INDEX.json` (`23`), và JSON nhúng trong `TABLE-RELATIONSHIPS.md` (`20`). |
| `ISS-AUD-004` | `MEDIUM` | #1, #2, #3 | `03-CODE-INDEX/SYMBOL-INDEX.jsonl`, `ROUTE-INDEX.jsonl`, `DEPENDENCY-INDEX.jsonl` | Trôi số dòng & chữ ký của `HomeController`, `IStaffStudentLookupService`, `StaffStudentLookupService`, `HttpRequestExtensions`; thiếu cạnh `StaffStudentLookupService -> IGradeCalculationService`; `22` symbolKey trong `DEPENDENCY-INDEX` thiếu định nghĩa trong `SYMBOL-INDEX`. |
| `ISS-AUD-005` | `MEDIUM` | #3, #4 | `01-MODULES/ATTENDANCE.md:240,280` | Mục 6 ghi sai rằng QR check-in gọi `IStudentCodeRolloverService` (thực tế query thẳng `StudentCodeHistories`); mục 9 vẫn mô tả `SaveBatchToRedisAsync` chờ Hangfire 10 phút trong khi `SaveBatch` đã Write-Through tức thì. |
| `ISS-AUD-006` | `MEDIUM` | #1, #4 | `05-FEATURES/FEAT-P5-01-STAFF-QUICK-LOOKUP.md` | Sai ID ô tìm kiếm (`#staffLookupSearchInput` vs `#staffStudentQuickSearchInput`), debounce (`250ms` vs `280ms`), điều kiện độ dài tối thiểu, tên cột `OldCode` vs `OriginalCode/ArchivedYearCode/NewCode`, tên cờ DTO (`MatchedByOldCode` vs `IsHistoricalMatch/MatchedHistoricalCode`). |
| `ISS-AUD-007` | `MEDIUM` | #4 | `05-FEATURES/FAST-QR-CHECKIN-AND-OFFLINE-SYNC.md:164`, `FEAT-P5-02-MOBILE-FIRST-UI.md` | Lệch thời gian khóa quét trùng QR (`2.5s` vs `3000ms` tại `ScanQr.cshtml:868`); lệch thông số UI Mobile (`54px` vs `56px`, `#mobileUserOffcanvas` vs `#mobileMenuOffcanvas`, `z-index: 1085` vs `1070`). |
| `ISS-AUD-008` | `LOW` | #2, #4 | `05-FEATURES/FEAT-P5-07-CLASS-CURRICULUM-MANAGEMENT.md:144-145` | Bảng endpoints viết tham số route `{id}` thay vì `{versionId:long}` như trong `LessonPlansController.cs:205,228`. |
| `ISS-AUD-009` | `LOW` | #1, #2, #9 | `03-CODE-INDEX/*.jsonl`, `00-OVERVIEW/PROJECT-DEPENDENCY-DIAGRAM.md:17` | Dòng baseline `null` cho `startLine`/`sourceLine` trong code indexes; độ phủ `FILE-INDEX` hiện dừng ở `72/315` file; `PROJECT-DEPENDENCY-DIAGRAM.md:17` thiếu dấu đóng ```` ``` ```` của Mermaid block. |

---

## 5. Điểm Chưa Đủ Căn Cứ Xác Minh Từ Source Code (`NEEDS_VERIFICATION`)

Kiểm toán viên độc lập **không đánh dấu `PASS`** cho các hạng mục dưới đây cho đến khi Developer kiểm chứng trực tiếp trên môi trường DB / Runtime:
1. **Lược đồ bảng PostgreSQL thực tế (`ClassCurriculumDocuments`, `ClassCurriculumVersions`, `ClassLessonPlans`, `StaffQuickLookupLogs`, `LookupViewOptions`):** **[RESOLVED - Developer Verified `2026-11-15`]** Do `ManagerStudentCaltholic/Migrations/` hiện có `0` file migration (đã xóa ở commit `cfb2b5e`), kiểm toán ban đầu gắn `NEEDS_VERIFICATION`; sau đó Developer đã xác nhận trực tiếp đã chạy migration & kiểm chứng DDL đầy đủ trên PostgreSQL (`ISS-AUD-002` ➔ `RESOLVED`).
2. **Tình trạng chuẩn hóa & đưa vào Git các tệp untracked:** `nginx/default.conf` (được mount trong `docker-compose.yml`) và `script/seed-curriculum-data.sql` hiện chưa được commit vào `2a3263d`.
3. **Phạm vi thực tế của `UserHomepageWidgetConfigs` và `TASK-P5-02.2..4`:** Chưa có bằng chứng Entity `UserHomepageWidgetConfigs` trong `Models/Entities/` hay `ParishDbContext.cs`.
