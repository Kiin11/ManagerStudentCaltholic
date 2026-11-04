# KNOWLEDGE VALIDATION AND AUDIT REPORT

## Metadata

* **Project:** `ManagerStudentCaltholic`


* **Ngày kiểm định:** 2026-11-04
* **Phạm vi đối chiếu:** Toàn bộ artifacts từ Task 01 đến Task 06 đối chiếu trực tiếp với 5 phần source code thực tế (`full_source_part_1.txt` đến `full_source_part_5.txt`).


* **Trạng thái tổng thể:** **CẦN HIỆU CHỈNH (REQUIRES CORRECTION)** — Phát hiện **18 vấn đề** (gồm **5 CRITICAL**, **6 HIGH**, **5 MEDIUM**, **2 LOW**).

---

## 1. Completeness Audit (Kiểm toán Độ đầy đủ)

### 1.1. Kiểm kê File (File Inventory & `FILE-INDEX.jsonl`)

* **Thực tế trong 5 file TXT nguồn:** Có tổng cộng **207 file** được xuất kèm header `FILE:` (21 file ở Part 1, 100 file ở Part 2, 22 file ở Part 3, 22 file ở Part 4, và 21 file ở Part 5).


* **Sai lệch trong Task 01 (`SOURCE-INVENTORY.md` & `PROJECT-INVENTORY.json`):**
* `SOURCE-INVENTORY.md` chỉ liệt kê **41 dòng** (gom gộp toàn bộ DTOs, Entities, Services vào 3 dòng chung số 32, 33, 34).
* `PROJECT-INVENTORY.json` chỉ xuất **12 file records**, bỏ sót **195 file** đã có trong source thực tế.




* **Sai lệch trong Task 05 (`FILE-INDEX.jsonl`):**
* Chỉ xuất **45 records**, bỏ sót **162 file** thực tế có trong nguồn (gồm 19 file `Interface/Services/*.cs`, 15 file `Models/DTOs/*.cs`, 36 file `Models/Entities/*.cs`, 2 file `Models/Enum/*.cs`, 24 file `Models/ViewModels/*.cs`, 4 file `Extensions/*.cs`, 2 file `Security/*.cs`, 35 file `Views/**/*.cshtml`, và các file cấu hình Docker/Nginx).





### 1.2. Phát hiện Source Code bị cắt (Truncated) và Rơi rớt giữa các Part

Trái ngược hoàn toàn với nhận định trong `MISSING-SOURCE-REPORT.md` của Task 01 (*"không phát hiện dấu hiệu bị cắt cụt ranh giới class/method nghiêm trọng"*), việc đối chiếu ranh giới cuối mỗi file TXT phát hiện **4 file bị cắt ngang (truncated)** và **hơn 18 file bị mất hoàn toàn** ở điểm nối giữa các Part:

* **Điểm cắt 1 (Cuối Part 1 → Đầu Part 2):**
* File `Controllers\PromotionController.cs` bị cắt cụt ngay giữa dòng `_logger.LogInformation("AUDIT: User {User} updated PromotionRuleConfig for YearId={YearId},...` ở cuối `full_source_part_1.txt`. Toàn bộ các action phía sau như `ExecuteRollover` và `CloneClassesToNewYear` (được gọi từ `Views\Promotion\Index.cshtml`) đều bị mất.


* Toàn bộ các Controller theo thứ tự bảng chữ cái từ `P` đến `U` nằm giữa `PromotionController.cs` và `UsersController.cs` bị mất hoàn toàn, bao gồm: `PublicRegistrationController.cs`, `PwaController.cs`, `SacramentsController.cs`, `StudentRegistrationsController.cs`, `StudentsController.cs`.




* **Điểm cắt 2 (Cuối Part 2 → Đầu Part 3):**
* File `Services\DeviceStudentRecognitionService.cs` bị cắt cụt tại dòng `if (!classIds.Any() &&...` ở cuối `full_source_part_2.txt`.


* Toàn bộ 9 class Service theo thứ tự bảng chữ cái từ `D` đến `R` nằm giữa `DeviceStudentRecognitionService.cs` và `RedisAttendanceService.cs` bị mất hoàn toàn (đối chiếu với đăng ký DI trong `ServiceCollectionExtensions.cs`), bao gồm: `FeatureNavigationService.cs`, `GradeCalculationService.cs`, `GradePermissionService.cs`, `LessonPlanImportService.cs`, `LogRetentionBackgroundService.cs`, `MassCampaignService.cs`, `PasswordHasherService.cs`, `PromotionEvaluationService.cs`, `QrCodeService.cs`.




* **Điểm cắt 3 (Cuối Part 3 → Đầu Part 4):**
* File `Views\Dashboard\Index.cshtml` bị cắt cụt tại dòng `<td class="te...` ở cuối `full_source_part_3.txt` (mất phần script khởi tạo biểu đồ Chart.js và bảng xếp hạng lớp).


* Toàn bộ các Razor Views theo thứ tự bảng chữ cái từ `D` đến `G` bị mất, bao gồm: `Views\Enrollments\Manage.cshtml`, `Views\Facilities\Index.cshtml`, `Views\FeaturePermissions\Index.cshtml`, `Views\Governance\Index.cshtml`.




* **Điểm cắt 4 (Cuối Part 4 → Đầu Part 5):**
* File `Views\Sacraments\Index.cshtml` bị cắt cụt tại dòng `dotSpan.innerHTML = item.isCompleted ? '<i class="bi bi-check-lg"></i>'...` ở cuối `full_source_part_4.txt`.


* Toàn bộ các Razor Views nằm giữa `Views\Sacraments\Index.cshtml` và `Views\Students\Index.cshtml` bị mất, bao gồm: `Views\Sacraments\PrintApprovalSheet.cshtml`, `Views\Shared\_Layout.cshtml`, `Views\Shared\_ValidationScriptsPartial.cshtml`, `Views\StudentRegistrations\Index.cshtml`.





### 1.3. Kiểm toán `SYMBOL-INDEX.jsonl` và `ROUTE-INDEX.jsonl`

* **Thiếu Symbol quan trọng:** `SYMBOL-INDEX.jsonl` (62 records) bỏ sót toàn bộ 14 class Service thực thi (`RedisAttendanceService`, `AttendanceDisputeService`, `BackgroundExportJobService`, `ClassEvaluationService`, `DeviceStudentRecognitionService`, `RedisCacheService`, `SacramentEvaluationService`, `SacramentRegistryService`, `StudentCodeGenerator`, `StudentCodeRolloverService`, `StudentExcelService`, `StudentPlacementService`, `SystemMetricsCollector`, `TokenService`) dù các file này có mặt đầy đủ trong Part 2 và Part 3.


* **Thiếu Overload:** Trong `AccountController.cs`, có 2 method `Login`: `[HttpGet] public IActionResult Login(string? returnUrl = null)` và `[HttpPost] public async Task<IActionResult> Login(LoginRequestDto model)`. `SYMBOL-INDEX.jsonl` chỉ lưu bản `POST` mà bỏ sót bản `GET`.


* **Thiếu Route:** `ROUTE-INDEX.jsonl` chỉ liệt kê **30 routes**, bỏ sót **64 action endpoints** của 16 Controllers đã có trong source code (ví dụ: `/Admin/Logs`, `/Admin/Logs/HealthStats`, `/Admin/Logs/Download` trong `LogsController.cs`; `/Grades/SaveSingleCell`, `/Grades/ToggleLock`, `/Grades/AddCustomColumn`, `/Grades/DeleteCustomColumn`, `/Grades/GetClassEvaluationSheet`, `/Grades/SubmitClassEvaluation` trong `GradesController.cs`; toàn bộ routes của `AnnouncementsController`, `BackgroundJobsController`, `DashboardController`, `EnrollmentsController`, `FacilitiesController`, `FeaturePermissionsController`, `GovernanceController`, `HomeController`, `MassCampaignsController`, `ParentMessagesController`, `PrintReportsController`, `PromotionController`, `UsersController`).



### 1.4. Kiểm toán các Module chưa được phân tích sâu

* Task 03 chỉ mới phân tích sâu 1 module (`ATTENDANCE`), còn các module lớn đã có source như `GRADES & EVALUATION`, `SACRAMENTS`, `MASS CAMPAIGNS`, `FACILITIES`, `GOVERNANCE & USERS`, `PUBLIC LOOKUP & DISPUTES` chưa có tài liệu Module Reverse Engineering riêng.



---

## 2. Consistency Audit (Kiểm toán Tính nhất quán)

### 2.1. Mâu thuẫn về cơ chế Write-Through Redis trong `SaveBatch` và `ScanCheckIn`

* **Bằng chứng từ Source (`Controllers\AttendanceController.cs`):**
* Trong action `SaveBatch`, sau khi gọi `_redisAttendance.SaveBatchToRedisAsync(request)`, code gọi ngay `await _redisAttendance.SyncDirtyAttendancesToDatabaseAsync(request.AttendanceDate);` với comment `// 2. P2-LOG-16 FIX: Đồng bộ tức thì (Write-Through) từ Redis xuống PostgreSQL ngay trong cùng Request`.


* Trong action `ScanCheckIn`, sau khi gọi `_redisAttendance.ProcessQrCheckInViaRedisAsync(request)`, code cũng gọi ngay `await _redisAttendance.SyncDirtyAttendancesToDatabaseAsync(targetDate);`.




* **Mâu thuẫn giữa các tài liệu đã tạo:**
* Trong `PROJECT-DEPENDENCY-DIAGRAM.md` (Task 02 - Sơ đồ 3), luồng `ScanCheckIn` không vẽ bước Write-Through xuống DB mà mô tả chỉ cập nhật Redis rồi đợi Hangfire 10 phút sau mới đồng bộ.
* Trong `ATTENDANCE.md` (Task 03 - Mục 9 Risk Areas), tài liệu nhận định sai sự thật: *"Mặc dù luồng `ScanCheckIn` đã fix lỗi bằng cách áp dụng Write-Through trực tiếp xuống DB (không chờ 10 phút), nhưng luồng `SaveBatchToRedisAsync` vẫn để cờ `IsDirty=true` chờ Hangfire quét"* — trái ngược trực tiếp với code thực tế tại `AttendanceController.SaveBatch`.





### 2.2. Mâu thuẫn về xử lý Mã QR năm học cũ (`_2526`) khi Quét QR Điểm danh

* Trong `Views\Home\Guides\_GuideAttendance.cshtml`, giao diện hướng dẫn ghi: *"Hệ thống tự động nhận diện cả thẻ QR mới lẫn thẻ năm học cũ (có đuôi `_2526`)"*.


* Tuy nhiên, trong `Services\RedisAttendanceService.cs` (`ProcessQrCheckInViaRedisAsync`), hệ thống chỉ truy vấn khớp chính xác `e.Student.StudentCode.ToUpper() == cleanCode` trên bảng `Enrollments` mà **không** gọi `IStudentCodeRolloverService.ResolveStudentByAnyCodeAsync` (khác với `LookupController.cs` và `AttendanceDisputeService.cs` có tiêm và gọi `IStudentCodeRolloverService`). Tài liệu `ATTENDANCE.md` (Mục 6 Dependencies) lại ghi module Attendance gọi `IStudentCodeRolloverService` cho mã QR cũ mà không phân biệt rằng chỉ `AttendanceDisputeService` gọi service này, còn `RedisAttendanceService` thì không.



### 2.3. Đứt gãy tham chiếu (Broken References) giữa `DEPENDENCY-INDEX.jsonl` và `SYMBOL-INDEX.jsonl`

* Trong `DEPENDENCY-INDEX.jsonl`, các trường `sourceSymbol` và `targetSymbol` trỏ tới:
* `ManagerStudentCaltholic.Services.RedisAttendanceService`
* `ManagerStudentCaltholic.Services.AttendanceDisputeService`
* `ManagerStudentCaltholic.Services.RedisCacheService`
* `ManagerStudentCaltholic.Services.TokenService`
* `ManagerStudentCaltholic.Services.RedisAttendanceService.ProcessQrCheckInViaRedisAsync`
* `ManagerStudentCaltholic.Interface.Services.IRedisAttendanceService.ProcessQrCheckInViaRedisAsync`
* `ManagerStudentCaltholic.Services.AttendanceTimeHelper.EvaluateCheckIn`


* **Không có bất kỳ symbolKey nào trong số trên tồn tại trong `SYMBOL-INDEX.jsonl`!**

### 2.4. Mâu thuẫn số lượng bảng giữa `DATABASE-OVERVIEW.md`, Task 05 `TABLE-INDEX.json` và Task 06 `TABLE-INDEX.json`

* Trong `Data\ParishDbContext.cs`, có **38 thuộc tính `DbSet<T>**` cộng thêm bảng `ClassLessonPlans` (cấu hình qua `modelBuilder.Entity<ClassLessonPlan>(entity => { entity.ToTable("ClassLessonPlans"); ... })` trong `OnModelCreating` dù không khai báo thuộc tính `DbSet`) = **39 bảng** thuộc schema `public`, cùng với schema `hangfire` được khai báo trong `Program.cs` (`SchemaName = "hangfire"`).


* `DATABASE-OVERVIEW.md` (Task 06) liệt kê **38 bảng** (thiếu bảng `ClassLessonPlans` và schema `hangfire`).


* `TABLE-INDEX.json` ở Task 05 chỉ có **18 bảng**.
* `TABLE-INDEX.json` ở Task 06 chỉ có **15 bảng** (bỏ mất 5 bảng `Announcements`, `BuildingZones`, `ClassRoomLocations`, `ClassSchedules`, `RoomIncidentReports` đã có ở Task 05 nhưng lại thêm 2 bảng `MassCampaigns`, `MassCampaignRecords`).

---

## 3. Evidence Audit (Kiểm toán Bằng chứng Nguồn)

Bảng phân loại mức độ xác thực của các thành phần trọng yếu trong bộ tài liệu:

| Nhận định / Quan hệ trong Tài liệu | Trạng thái Thực tế | Căn cứ (Source Evidence) | Đánh giá Audit |
| --- | --- | --- | --- |
| `StudentsController.cs`, `SacramentsController.cs` có trong source | **INFERRED** (Không có file trong TXT) | Chỉ xuất hiện gián tiếp qua Razor Views (`Views\Students\Index.cshtml`, `Views\Sacraments\Index.cshtml`) và chuỗi text trong `LogsController.cs`.

 | Task 02 ghi như file đã xác nhận là **SAI**. Phải hạ xuống `INFERRED` / `NOT_PROVIDED`. |
| `GradeCalculationService.cs`, `MassCampaignService.cs`, `FeatureNavigationService.cs` có trong source | **INFERRED** (Không có file trong TXT) | Chỉ xuất hiện trong lệnh đăng ký DI `services.AddScoped<...>()` tại `ServiceCollectionExtensions.cs`.

 | Task 02 ghi `GradeCalculationService.cs` làm bằng chứng trực tiếp là **SAI**. |
| `RequestLoggingMiddleware.cs` xử lý request logging | **CONFIRMED UNUSED** | File `Middlewares\RequestLoggingMiddleware.cs` tồn tại, nhưng `Program.cs` gọi `app.UseCustomRequestLogging()` từ `LoggingExtensions.cs`.

 | Cần ghi rõ `RequestLoggingMiddleware` không được đăng ký vào pipeline. |
| Quan hệ kế thừa & thực thi `ISoftDelete` trên 8 Entities | **CONFIRMED** | Xác nhận trực tiếp từ khai báo class và `ParishDbContext.SaveChangesAsync`.

 | Chính xác. |
| Hai hệ thống bảng điểm song song trong `ParishDbContext` | **CONFIRMED** | `GradeConfiguration`/`GradeRecord` (dùng trong `GradesController.cs`) song song với `GradeColumn`/`StudentGrade`/`GradingFormula`/`StudentGradeSummary` (khai báo trong `ParishDbContext.cs`).

 | Chưa được phân tích trong `DATABASE-OVERVIEW.md`. |

---

## 4. Hallucination Audit (Kiểm toán Suy diễn / Ảo giác AI)

1. **Áp đặt tên Domain giả định từ Prompt vào Inventory (Task 01):**
* Trong `SOURCE-INVENTORY.md`, `PROJECT-INVENTORY.json` và `MODULE-INVENTORY.json` (Task 01), AI đã gán các module giả định từ ví dụ của đề bài vào hệ thống Giáo lý: `Hotel / Class Management`, `Hotel / Facilities`, `Booking / Enrollment`, `Booking / MassCampaigns`, và tự tạo một record `PaymentPaymentModule` (`module: "Payment"`). Hệ thống thực tế hoàn toàn không có nghiệp vụ Hotel, Booking hay Payment.




2. **Tự tạo Index `"StudentCode"` trên bảng `Students` trong `TABLE-INDEX.json` (Task 05 & Task 06):**
* Cả hai file `TABLE-INDEX.json` ở Task 05 và Task 06 đều ghi bảng `Students` có `"indexes": ["IsDeleted", "StudentCode"]`.
* Kiểm tra `Data\ParishDbContext.cs` và `Models\Entities\Student.cs`: Hệ thống **chỉ** đánh index cho `IsDeleted` (`modelBuilder.Entity<Student>().HasIndex(e => e.IsDeleted);`), hoàn toàn **không có** khai báo `HasIndex(e => e.StudentCode)` trên bảng `Students` (chỉ bảng `StudentCodeHistories` mới có `HasIndex(h => h.OriginalCode)` và `HasIndex(h => h.ArchivedYearCode)`).




3. **Bỏ qua nhánh xử lý mặc định ngoài khung giờ trong `ProcessQrCheckInViaRedisAsync` (Task 03 & Task 04):**
* Trong `Services\RedisAttendanceService.cs`, khi `AttendanceTimeHelper.EvaluateCheckIn` trả về `(!isMass && !isClass)` (do quét QR ngoài khung giờ quy định), `ProcessQrCheckInViaRedisAsync` tự động gán `isClass = true; status = AttendanceStatus.Present;`. Tài liệu Task 03 và Task 04 chưa ghi nhận nhánh logic thực tế này.




4. **Bỏ qua việc `IsEligibleForSacrament` bị gán cứng `false` trong `AttendanceController.Statistics` (Task 03):**
* Trong `Controllers\AttendanceController.cs` (action `Statistics`), thuộc tính `IsEligibleForSacrament` đang bị comment công thức và gán cứng `IsEligibleForSacrament = false // IsEligibleForSacrament = (massRate >= 80.0 && classRate >= 80.0)`, trong khi action `ExportStatisticsExcel` ngay bên dưới lại tính `var isEligible = (sunRate >= 80.0 && classRate >= 80.0);`.





---

## 5. Technical Quality & Security Audit

1. **Vi phạm quy tắc định dạng JSON/JSONL (Markdown Fence):**
* Toàn bộ các file JSON và JSONL trong Task 01, Task 05 và Task 06 đều bị bọc trong cặp thẻ ````json` hoặc ````jsonl`, vi phạm quy tắc số 5 của System Instructions (*"Không thêm Markdown fence vào nội dung file JSON/JSONL"*).


2. **Không đồng nhất định dạng đường dẫn (`filePath`):**
* Task 01 sử dụng đường dẫn tuyệt đối trên máy local (`D:\github\ManagerStudentCaltholic\ManagerStudentCaltholic\Controllers\AbsenceController.cs`) hoặc đường dẫn rút gọn có dấu `...` (`D:\github\...\Controllers\AbsenceController.cs`), trong khi Task 05 sử dụng đường dẫn tương đối (`Controllers/AbsenceController.cs`).




3. **Thiếu số dòng (`startLine`, `endLine`, `sourceLine`):**
* Tất cả các trường `startLine`, `endLine` trong `SYMBOL-INDEX.jsonl`, `ROUTE-INDEX.jsonl` và `sourceLine` trong `DEPENDENCY-INDEX.jsonl` đều đang để `null` dù có thể xác định vị trí tương đối trong từng file nguồn.


4. **Cảnh báo Rò rỉ Thông tin Nhạy cảm (Secret Leakage trong Source Configuration):**
* File `appsettings.Development.json` chứa chuỗi kết nối Azure PostgreSQL (trong comment) kèm mật khẩu thật, mật khẩu PostgreSQL local, và `JwtSettings:SecretKey` thật.


* File `Models\ViewModels\BatchTeacherViewModels.cs` và `Views\Users\Index.cshtml` chứa mật khẩu khởi tạo mặc định cứng `"GLV@2026!"`.


* Mặc dù tài liệu đã tạo không làm lộ các chuỗi này, báo cáo kiểm định bắt buộc phải ghi nhận `[REDACTED]` để đội ngũ phát triển xử lý.



---

### 2. KNOWLEDGE-ISSUES.jsonl

{"issueId": "ISS-001", "severity": "CRITICAL", "artifact": "MISSING-SOURCE-REPORT.md", "location": "Section 1 & Section 3", "category": "Completeness", "description": "Báo cáo sai rằng các file TXT không bị cắt cụt. Thực tế có 4 file bị cắt ngang ở cuối các Part 1, 2, 3, 4 và ít nhất 18 file (5 Controllers, 9 Services, 4+ Views) bị mất hoàn toàn ở ranh giới chuyển tiếp giữa các Part.", "evidence": "PromotionController.cs bị cắt ở cuối full_source_part_1.txt; DeviceStudentRecognitionService.cs bị cắt ở cuối full_source_part_2.txt; Views/Dashboard/Index.cshtml bị cắt ở cuối full_source_part_3.txt; Views/Sacraments/Index.cshtml bị cắt ở cuối full_source_part_4.txt."}
{"issueId": "ISS-002", "severity": "CRITICAL", "artifact": "ATTENDANCE.md", "location": "Section 9 (Risk Areas)", "category": "Hallucination / Business Rule", "description": "Nhận định sai rằng luồng SaveBatchToRedisAsync chỉ lưu trên Redis với cờ IsDirty=true và phải chờ Hangfire 10 phút mới đồng bộ xuống DB. Thực tế AttendanceController.SaveBatch đã gọi ngay SyncDirtyAttendancesToDatabaseAsync (Write-Through) trong cùng HTTP Request.", "evidence": "Controllers/AttendanceController.cs trong hàm SaveBatch gọi await _redisAttendance.SyncDirtyAttendancesToDatabaseAsync(request.AttendanceDate) ngay sau SaveBatchToRedisAsync (P2-LOG-16 FIX)."}
{"issueId": "ISS-003", "severity": "CRITICAL", "artifact": "DEPENDENCY-INDEX.jsonl & SYMBOL-INDEX.jsonl", "location": "Cross-index references", "category": "Consistency / Broken Reference", "description": "DEPENDENCY-INDEX.jsonl tham chiếu tới 14 class Service (như RedisAttendanceService, AttendanceDisputeService, RedisCacheService, TokenService) và các method thực thi nhưng không có symbol nào trong số này được khai báo trong SYMBOL-INDEX.jsonl.", "evidence": "Services/RedisAttendanceService.cs, Services/AttendanceDisputeService.cs, Services/RedisCacheService.cs, Services/TokenService.cs có trong source nhưng bị bỏ sót khỏi SYMBOL-INDEX.jsonl."}
{"issueId": "ISS-004", "severity": "CRITICAL", "artifact": "PROJECT.md & ARCHITECTURE.md", "location": "Section 3 (PROJECT.md) & Section 1.2 (ARCHITECTURE.md)", "category": "Evidence Audit", "description": "Trích dẫn các file StudentsController.cs, SacramentsController.cs và GradeCalculationService.cs như bằng chứng trực tiếp đã xác nhận, trong khi nội dung các file này đã bị mất giữa các Part TXT và chỉ có thể suy luận (INFERRED) từ DI container và Views.", "evidence": "ServiceCollectionExtensions.cs đăng ký AddScoped(), nhưng file GradeCalculationService.cs, StudentsController.cs, SacramentsController.cs không tồn tại trong 5 file TXT."}
{"issueId": "ISS-005", "severity": "CRITICAL", "artifact": "TABLE-INDEX.json (Task 05 & Task 06)", "location": "schemas[0].tables[0] (Students)", "category": "Hallucination", "description": "Tự ý thêm index StudentCode vào bảng Students trong TABLE-INDEX.json, trong khi ParishDbContext.cs chỉ đánh index cho trường IsDeleted trên bảng Students.", "evidence": "Data/ParishDbContext.cs chỉ khai báo modelBuilder.Entity().HasIndex(e => e.IsDeleted); Models/Entities/Student.cs không có thuộc tính [Index] trên StudentCode."}
{"issueId": "ISS-006", "severity": "HIGH", "artifact": "SOURCE-INVENTORY.md, PROJECT-INVENTORY.json, FILE-INDEX.jsonl", "location": "All records", "category": "Completeness", "description": "Kiểm kê thiếu nghiêm trọng số lượng file thực tế. Nguồn có 207 file nhưng SOURCE-INVENTORY.md chỉ ghi 41 dòng, PROJECT-INVENTORY.json chỉ có 12 records, và FILE-INDEX.jsonl chỉ có 45 records (bỏ sót 162 file).", "evidence": "Đếm toàn bộ tiêu đề FILE: từ full_source_part_1.txt đến full_source_part_5.txt có đúng 207 file."}
{"issueId": "ISS-007", "severity": "HIGH", "artifact": "TABLE-INDEX.json (Task 05 vs Task 06) & DATABASE-OVERVIEW.md", "location": "schemas[0].tables", "category": "Consistency & Completeness", "description": "Mâu thuẫn số lượng bảng giữa các tài liệu: ParishDbContext.cs định nghĩa 39 bảng (38 DbSet + 1 bảng ClassLessonPlans trong OnModelCreating), DATABASE-OVERVIEW.md liệt kê 38 bảng, TABLE-INDEX.json (Task 05) liệt kê 18 bảng, còn TABLE-INDEX.json (Task 06) chỉ liệt kê 15 bảng.", "evidence": "Data/ParishDbContext.cs khai báo 38 DbSet và modelBuilder.Entity(entity => entity.ToTable("ClassLessonPlans"))."}
{"issueId": "ISS-008", "severity": "HIGH", "artifact": "ROUTE-INDEX.jsonl", "location": "All records", "category": "Completeness", "description": "Chỉ lập chỉ mục 30 routes trên tổng số 94 action endpoints có trong 22 Controllers. Bỏ sót hoàn toàn routes của 16 Controllers và các Attribute Routes quan trọng như /Admin/Logs, /Admin/Logs/HealthStats, /Admin/Logs/Download.", "evidence": "Controllers/LogsController.cs khai báo [Route("Admin/Logs")], [Route("Logs")], [Route("Admin/Logs/HealthStats")], [Route("Admin/Logs/Download")]."}
{"issueId": "ISS-009", "severity": "HIGH", "artifact": "ATTENDANCE.md & FAST-QR-CHECKIN-AND-OFFLINE-SYNC.md", "location": "Business Rules & Execution Flow", "category": "Completeness / Business Rule", "description": "Bỏ sót nhánh logic xử lý quét QR ngoài khung giờ quy định (khi !isMass && !isClass thì hệ thống tự động ép kiểu thành điểm danh Giờ học Giáo lý có mặt: isClass = true; status = AttendanceStatus.Present) và sự bất đồng bộ giữa _GuideAttendance.cshtml với RedisAttendanceService về việc quét mã QR năm cũ (_2526).", "evidence": "Services/RedisAttendanceService.cs trong hàm ProcessQrCheckInViaRedisAsync gán if (!isMass && !isClass) { isClass = true; status = AttendanceStatus.Present; } và không gọi IStudentCodeRolloverService."}
{"issueId": "ISS-010", "severity": "HIGH", "artifact": "SOURCE-INVENTORY.md & MODULE-INVENTORY.json", "location": "Module column / field", "category": "Hallucination", "description": "Sử dụng tên module giả định từ ví dụ đề bài (Hotel, Booking, PaymentPaymentModule) để phân loại các file của hệ thống Quản lý Giáo lý.", "evidence": "SOURCE-INVENTORY.md và MODULE-INVENTORY.json ở Task 01 gán Hotel / Class Management, Booking / Enrollment, PaymentPaymentModule dù codebase là ManagerStudentCaltholic."}
{"issueId": "ISS-011", "severity": "HIGH", "artifact": "DATABASE-OVERVIEW.md & ARCHITECTURE.md", "location": "Database Tables & Grade Module", "category": "Architecture Analysis", "description": "Chưa phân biệt 2 cụm bảng điểm tồn tại song song trong ParishDbContext: cụm đang dùng thực tế trong GradesController (GradeConfigurations, GradeRecords, ClassTeachers) và cụm bảng snake_case cũ/ít dùng (grade_columns, student_grades, grading_formulas, student_grade_summaries, attendance_score_rules, teacher_classes).", "evidence": "Controllers/GradesController.cs truy vấn _context.GradeConfigurations và _context.GradeRecords, trong khi Data/ParishDbContext.cs vẫn giữ các DbSet GradeColumns, StudentGrades, GradingFormulas, TeacherClasses."}
{"issueId": "ISS-012", "severity": "MEDIUM", "artifact": "SYMBOL-INDEX.jsonl", "location": "AccountController.Login", "category": "Completeness / Overload", "description": "Vi phạm quy tắc giữ nguyên overload trong SYMBOL-INDEX.jsonl: AccountController có 2 hàm Login (GET và POST) nhưng chỉ có hàm POST được đưa vào index.", "evidence": "Controllers/AccountController.cs chứa cả [HttpGet] public IActionResult Login(string? returnUrl = null) và [HttpPost] public async Task Login(LoginRequestDto model)."}
{"issueId": "ISS-013", "severity": "MEDIUM", "artifact": "ARCHITECTURE.md & FILE-INDEX.jsonl", "location": "Middleware layer", "category": "Architecture Analysis", "description": "Không ghi chú rằng RequestLoggingMiddleware.cs là class không được kích hoạt trong HTTP pipeline, vì Program.cs sử dụng inline middleware UseCustomRequestLogging() trong LoggingExtensions.cs.", "evidence": "Program.cs gọi app.UseCustomRequestLogging() được định nghĩa tại Extensions/LoggingExtensions.cs, không gọi UseMiddleware()."}
{"issueId": "ISS-014", "severity": "MEDIUM", "artifact": "ATTENDANCE.md", "location": "Section 5 (Business Rules)", "category": "Business Rule", "description": "Chưa ghi nhận việc action AttendanceController.Statistics đang gán cứng IsEligibleForSacrament = false (comment công thức), khác với action ExportStatisticsExcel vẫn tính (sunRate >= 80.0 && classRate >= 80.0).", "evidence": "Controllers/AttendanceController.cs trong hàm Statistics gán IsEligibleForSacrament = false, còn trong ExportStatisticsExcel gán var isEligible = (sunRate >= 80.0 && classRate >= 80.0)."}
{"issueId": "ISS-015", "severity": "MEDIUM", "artifact": "PROJECT-DEPENDENCY-DIAGRAM.md", "location": "Section 3 (Sequence Diagram)", "category": "Consistency", "description": "Sơ đồ Sequence Diagram của luồng điểm danh QR không thể hiện bước Write-Through gọi SyncDirtyAttendancesToDatabaseAsync() ngay trong ScanCheckIn mà chỉ vẽ ở luồng Hangfire 10 phút/lần.", "evidence": "Controllers/AttendanceController.cs gọi await _redisAttendance.SyncDirtyAttendancesToDatabaseAsync(targetDate) ngay bên trong ScanCheckIn."}
{"issueId": "ISS-016", "severity": "MEDIUM", "artifact": "TECH-STACK.md & INTEGRATION-OVERVIEW.md", "location": "Security / Configuration", "category": "Security Audit", "description": "Chưa cảnh báo các thông tin nhạy cảm (Secret/Password) đang bị lưu cứng trong appsettings.Development.json và BatchTeacherViewModels.cs cần được gắn nhãn [REDACTED].", "evidence": "appsettings.Development.json chứa mật khẩu DB và JwtSettings:SecretKey; Models/ViewModels/BatchTeacherViewModels.cs chứa DefaultPassword hardcoded."}
{"issueId": "ISS-017", "severity": "LOW", "artifact": "PROJECT-INVENTORY.json, MODULE-INVENTORY.json, FILE-INDEX.jsonl, SYMBOL-INDEX.jsonl, DEPENDENCY-INDEX.jsonl, ROUTE-INDEX.jsonl, TABLE-INDEX.json", "location": "Formatting", "category": "Technical Quality", "description": "Sử dụng Markdown code fence (`json và `jsonl) bao quanh nội dung các file JSON/JSONL, vi phạm quy tắc Output Requirements số 5.", "evidence": "Các khối xuất JSON/JSONL ở Task 01, Task 05, Task 06 đều có thẻ đóng mở ```."}
{"issueId": "ISS-018", "severity": "LOW", "artifact": "SOURCE-INVENTORY.md vs FILE-INDEX.jsonl", "location": "filePath field", "category": "Technical Quality", "description": "Không thống nhất chuẩn đường dẫn filePath giữa các artifact: SOURCE-INVENTORY.md dùng đường dẫn rút gọn chứa dấu '...' (D:\github\...\Controllers\AbsenceController.cs), PROJECT-INVENTORY.json dùng đường dẫn tuyệt đối ổ D:, còn FILE-INDEX.jsonl dùng đường dẫn tương đối.", "evidence": "Đối chiếu cột Original File Path trong SOURCE-INVENTORY.md và trường filePath trong FILE-INDEX.jsonl."}

---

### 3. MISSING-DATA-REPORT.md

# MISSING DATA AND TRUNCATED SOURCE REPORT

Báo cáo này liệt kê chính xác toàn bộ các file bị cắt ngang (truncated) và các file bị thiếu hoàn toàn trong bộ 5 file TXT đã cung cấp, kèm theo bằng chứng xác thực từ mã nguồn.

## 1. Danh sách các File bị cắt ngang (Truncated Files)

| Stt | Original File Path | Vị trí bị cắt | Dòng code cuối cùng trước khi bị cắt | Phần nội dung bị mất |
| --- | --- | --- | --- | --- |
| 1 | `D:\github\ManagerStudentCaltholic\ManagerStudentCaltholic\Controllers\PromotionController.cs`

 | Cuối `full_source_part_1.txt`

 | `_logger.LogInformation("AUDIT: User {User} updated PromotionRuleConfig for YearId={YearId},...`

 | Phần cuối của action `SavePromotionRule`, toàn bộ action `ExecuteRollover` và `CloneClassesToNewYear` (được gọi qua AJAX từ `Views\Promotion\Index.cshtml`).

 |
| 2 | `D:\github\ManagerStudentCaltholic\ManagerStudentCaltholic\Services\DeviceStudentRecognitionService.cs`

 | Cuối `full_source_part_2.txt`

 | `if (!classIds.Any() &&...`

 | Phần truy vấn `Announcements` trong `DetectStudentsAndLoadAnnouncementsAsync`, hàm `DetachStudentFromDevice`, và các hàm đọc/ghi Cookie mã hóa `ReadProtectedCookieList`/`WriteProtectedCookieList`.

 |
| 3 | `D:\github\ManagerStudentCaltholic\ManagerStudentCaltholic\Views\Dashboard\Index.cshtml`

 | Cuối `full_source_part_3.txt`

 | `<td class="te...`

 | Phần cột Liên hệ của bảng `HighAbsenceStudents`, bảng xếp hạng `TopPerformingClasses`, và toàn bộ khối `@section Scripts` vẽ biểu đồ Chart.js (`gradeComparisonBarChart`, `statusDoughnutChart`, `monthlyTrendLineChart`).

 |
| 4 | `D:\github\ManagerStudentCaltholic\ManagerStudentCaltholic\Views\Sacraments\Index.cshtml`

 | Cuối `full_source_part_4.txt`

 | `dotSpan.innerHTML = item.isCompleted ? '<i class="bi bi-check-lg"></i>'...`

 | Phần script JavaScript render timeline 4 chặng Bí tích, hàm `submitSaveSacramentRecord` và `submitDeleteSacramentRecord`.

 |

---

## 2. Danh sách các File Code C# bị mất hoàn toàn giữa các Part TXT

### 2.1. Nhóm Controllers bị mất (Giữa Part 1 và Part 2 — thứ tự chữ cái `P` đến `U`)

1. `Controllers\PublicRegistrationController.cs` — **Bằng chứng tồn tại:** Được gọi từ `Views\PublicRegistration\Index.cshtml` (`/PublicRegistration/Submit`), `Views\PublicRegistration\CheckStatus.cshtml`, `Views\PublicRegistration\Success.cshtml`, `LogsController.cs` và `nginx\default.conf`.


2. `Controllers\PwaController.cs` — **Bằng chứng tồn tại:** Có thư mục view tương ứng `Views\Pwa\Index.cshtml`.


3. `Controllers\SacramentsController.cs` — **Bằng chứng tồn tại:** Được gọi từ `Views\Sacraments\Index.cshtml`, `Views\Sacraments\Evaluation.cshtml` (`/Sacraments/GetRecords/{id}`, `/Sacraments/SaveRecord`, `/Sacraments/DeleteRecord`, `/Sacraments/SaveApprovalBatch`, `/Sacraments/ApproveBatch`, `/Sacraments/ExportApprovalExcel`, `/Sacraments/PrintApprovalSheet`).


4. `Controllers\StudentRegistrationsController.cs` — **Bằng chứng tồn tại:** Được tham chiếu tại `Views\Dashboard\Index.cshtml` (`/StudentRegistrations`), `Views\Home\Guides\_GuideSacrament.cshtml` (`/StudentRegistrations/Index`) và `Models\ViewModels\AdminRegistrationDashboardViewModel.cs`.


5. `Controllers\StudentsController.cs` — **Bằng chứng tồn tại:** Được gọi từ `Views\Students\Index.cshtml` (`/Students/GetStudentAjax/{id}`, `/Students/PreviewNextCode`, `/Students/CheckCodeUnique`, `Create`, `Edit`, `ImportExcel`, `ToggleActive`, `DownloadQr`) và `Views\Students\PrintBadges.cshtml`.


6. *(Lưu ý)* `Controllers\GradesController_copy.cs` — Đã bị loại khỏi bản build theo cấu hình `<Compile Remove="Controllers\GradesController_copy.cs" />` trong `ManagerStudentCaltholic.csproj` nên không cần bổ sung.



### 2.2. Nhóm Services bị mất (Giữa Part 2 và Part 3 — thứ tự chữ cái `D` đến `R`)

Toàn bộ 9 class dưới đây được xác nhận đăng ký Dependency Injection trong `Extensions\ServiceCollectionExtensions.cs` nhưng bị rơi mất giữa cuối Part 2 và đầu Part 3:

1. `Services\FeatureNavigationService.cs` (Thực thi `IFeatureNavigationService`).


2. `Services\GradeCalculationService.cs` (Thực thi `IGradeCalculationService`).


3. `Services\GradePermissionService.cs` (Thực thi `IGradePermissionService`).


4. `Services\LessonPlanImportService.cs` (Thực thi `ILessonPlanImportService`).


5. `Services\LogRetentionBackgroundService.cs` (Thực thi `IHostedService` và `ILogMaintenanceJobService`).


6. `Services\MassCampaignService.cs` (Thực thi `IMassCampaignService`).


7. `Services\PasswordHasherService.cs` (Thực thi `IPasswordHasherService`).


8. `Services\PromotionEvaluationService.cs` (Thực thi `IPromotionEvaluationService`).


9. `Services\QrCodeService.cs` (Thực thi `IQrCodeService`).



---

## 3. Danh sách các File Giao diện (Views), Client Assets và Migrations chưa được cung cấp

1. **Razor Views bị rơi giữa Part 3 & Part 4 (`D` → `G`) và giữa Part 4 & Part 5 (`S` → `S`):**
* `Views\Enrollments\Manage.cshtml` (Được trả về từ `EnrollmentsController.Manage`).


* `Views\Facilities\Index.cshtml` (Được trả về từ `FacilitiesController.Index`).


* `Views\FeaturePermissions\Index.cshtml` (Được trả về từ `FeaturePermissionsController.Index`).


* `Views\Governance\Index.cshtml` (Được trả về từ `GovernanceController.Index`).


* `Views\Sacraments\PrintApprovalSheet.cshtml` (Được liên kết từ `Views\Sacraments\Evaluation.cshtml`).


* `Views\Shared\_Layout.cshtml` và `Views\Shared\_ValidationScriptsPartial.cshtml` (Được tham chiếu trong `Views\_ViewStart.cshtml` và `Views\Account\Login.cshtml`).


* `Views\StudentRegistrations\Index.cshtml` (Màn hình duyệt hồ sơ tuyển sinh của BĐH).




2. **Client-side JavaScript & Service Worker (`wwwroot/`):**
* File chứa đối tượng `ParishOfflineEngine` (được gọi trong `Views\Attendance\Index.cshtml` và `Views\Attendance\ScanQr.cshtml` để lưu IndexedDB).


* File Service Worker `wwwroot\sw.js` (được cấu hình header `no-cache` trong `Program.cs`).




3. **EF Core Migrations (`Migrations/`):**
* Thư mục `Migrations\` được khai báo trong `ManagerStudentCaltholic.csproj` và thực thi tự động qua `dbContext.Database.Migrate()` trong `WebApplicationExtensions.cs`, nhưng chưa có các file migration `.cs` và `ParishDbContextModelSnapshot.cs`.





---

### 4. CORRECTION-PLAN.md

# CORRECTION PLAN (KẾ HOẠCH HIỆU CHỈNH BỘ KNOWLEDGE BASE)

Để bộ tài liệu và chỉ mục AI Codebase Analyzer đạt độ chính xác 100% và sẵn sàng phục vụ việc sửa bug, phát triển tính năng và phân tích impact, quy trình hiệu chỉnh được chia thành 4 giai đoạn theo mức độ ưu tiên:

---

## Giai đoạn 1: Bổ sung các phần Source Code bị thiếu / bị cắt (Yêu cầu Dữ liệu Đầu vào)

Đề xuất cung cấp bổ sung các đoạn source bị khuyết ở điểm nối giữa các Part TXT:

1. **Bổ sung phần cuối của 4 file bị cắt:**
* `Controllers\PromotionController.cs` (từ dòng `_logger.LogInformation("AUDIT: User {User} updated PromotionRuleConfig...` đến hết file).


* `Services\DeviceStudentRecognitionService.cs` (từ dòng `if (!classIds.Any() &&...` đến hết file).


* `Views\Dashboard\Index.cshtml` (từ bảng `HighAbsenceStudents` đến hết file).


* `Views\Sacraments\Index.cshtml` (từ vòng lặp `currentSacramentTimeline.forEach` đến hết file).




2. **Bổ sung 5 Controllers và 9 Services bị mất ở ranh giới các Part:**
* Controllers: `PublicRegistrationController.cs`, `PwaController.cs`, `SacramentsController.cs`, `StudentRegistrationsController.cs`, `StudentsController.cs`.


* Services: `FeatureNavigationService.cs`, `GradeCalculationService.cs`, `GradePermissionService.cs`, `LessonPlanImportService.cs`, `LogRetentionBackgroundService.cs`, `MassCampaignService.cs`, `PasswordHasherService.cs`, `PromotionEvaluationService.cs`, `QrCodeService.cs`.




3. **Bổ sung JS Offline Engine & Shared Layout:**
* File JS định nghĩa `ParishOfflineEngine`, `wwwroot\sw.js`, và `Views\Shared\_Layout.cshtml`.





---

## Giai đoạn 2: Sửa lỗi CRITICAL trong Tài liệu Kiến trúc & Nghiệp vụ (Có thể thực hiện ngay trên Source hiện có)

1. **Hiệu chỉnh `ATTENDANCE.md` và `PROJECT-DEPENDENCY-DIAGRAM.md` (Khắc phục `ISS-002`, `ISS-009`, `ISS-014`, `ISS-015`):**
* Cập nhật lại mô tả và Sequence Diagram của cả `SaveBatch` và `ScanCheckIn` trong `AttendanceController.cs`: Cả hai luồng đều thực hiện **Write-Through tức thì** xuống PostgreSQL thông qua `SyncDirtyAttendancesToDatabaseAsync` ngay trong cùng HTTP Request (P2-LOG-16 FIX).


* Bổ sung quy tắc xử lý quét QR ngoài khung giờ trong `RedisAttendanceService.ProcessQrCheckInViaRedisAsync` (`!isMass && !isClass` mặc định tính vào buổi học Giáo lý `isClass = true`, `status = PRESENT`).


* Ghi nhận rõ `RedisAttendanceService.ProcessQrCheckInViaRedisAsync` hiện chưa tích hợp `IStudentCodeRolloverService.ResolveStudentByAnyCodeAsync` cho mã QR năm cũ (`_2526`), và `AttendanceController.Statistics` đang gán cứng `IsEligibleForSacrament = false`.




2. **Hiệu chỉnh `PROJECT.md` và `ARCHITECTURE.md` (Khắc phục `ISS-004`, `ISS-011`, `ISS-013`):**
* Đánh dấu `[INFERRED - File Missing in Export]` đối với các class `StudentsController`, `SacramentsController`, `GradeCalculationService`, `MassCampaignService`, `FeatureNavigationService` cho đến khi nhận được source bổ sung.


* Ghi chú rõ `RequestLoggingMiddleware.cs` không được đăng ký trong `Program.cs` (hệ thống đang dùng `UseCustomRequestLogging()` trong `LoggingExtensions.cs`).


* Phân tách rõ cụm bảng điểm đang hoạt động (`GradeConfigurations`, `GradeRecords`) và cụm bảng điểm snake_case (`grade_columns`, `student_grades`, `grading_formulas`, `student_grade_summaries`, `attendance_score_rules`, `teacher_classes`).




3. **Hiệu chỉnh `SOURCE-INVENTORY.md` và `MODULE-INVENTORY.json` (Khắc phục `ISS-001`, `ISS-010`, `ISS-018`):**
* Loại bỏ hoàn toàn các nhãn domain giả định từ ví dụ prompt (`Hotel`, `Booking`, `PaymentPaymentModule`). Chuẩn hóa lại tên module theo đúng nghiệp vụ Giáo xứ: `Attendance`, `AcademicYear`, `AuthAndUsers`, `Announcements`, `BackgroundJobs`, `ClassAndEnrollment`, `DashboardAndAnalytics`, `Facilities`, `GovernanceAndPermissions`, `GradesAndEvaluation`, `LessonPlans`, `SystemLogs`, `PublicLookup`, `MassCampaigns`, `ParentMessages`, `PrintReports`, `PromotionAndRollover`, `PublicRegistration`, `Sacraments`, `StudentManagement`.





---

## Giai đoạn 3: Tái tạo Toàn diện Bộ Index JSON/JSONL (Khắc phục `ISS-003`, `ISS-005`, `ISS-006`, `ISS-007`, `ISS-008`, `ISS-012`, `ISS-017`)

1. **`FILE-INDEX.jsonl`:** Xuất đầy đủ toàn bộ **207 file** thực tế có trong 5 file TXT theo chuẩn đường dẫn tương đối nhất quán từ thư mục gốc Solution, đánh dấu `"isComplete": false` cho 4 file bị cắt ngang.


2. **`SYMBOL-INDEX.jsonl`:**
* Bổ sung đầy đủ 14 class Service và toàn bộ các public methods của chúng trong Part 2 & Part 3.


* Bổ sung toàn bộ 94 action methods của 22 Controllers (giữ đầy đủ các overload như `AccountController.Login(string?)` và `AccountController.Login(LoginRequestDto)` bằng `symbolKey` kèm tham số).


* Bổ sung toàn bộ 39 Entities, 20 Interfaces, 2 Enums và các DTOs/ViewModels.




3. **`DEPENDENCY-INDEX.jsonl`:** Đồng bộ 100% `sourceSymbol` và `targetSymbol` khớp với `symbolKey` trong `SYMBOL-INDEX.jsonl`.
4. **`ROUTE-INDEX.jsonl`:** Xuất đầy đủ **94 routes** từ 22 Controllers, bao gồm cả các Attribute Routes của `LookupController`, `LogsController`, `LessonPlansController` và Conventional Routes của 19 Controllers còn lại.


5. **`TABLE-INDEX.json`:** Hợp nhất và xuất đầy đủ **39 bảng** của `ParishDbContext` (bao gồm cả `ClassLessonPlans`) + ghi nhận schema `hangfire`, xóa bỏ index ảo `"StudentCode"` trên bảng `Students`.


6. **Chuẩn hóa Định dạng:** Xuất nội dung JSON/JSONL thuần túy không kèm Markdown fence và ẩn (`[REDACTED]`) mọi mật khẩu/khóa bí mật cấu hình.



---

**Trạng thái chờ lệnh:** Tôi đã hoàn tất việc kiểm định và lập kế hoạch hiệu chỉnh mà không tự ý sửa đổi source code hay tài liệu gốc. Vui lòng cho biết bạn muốn cung cấp bổ sung phần source bị cắt trước, hay yêu cầu tôi thực thi ngay **Giai đoạn 2 & Giai đoạn 3** để xuất lại bộ tài liệu và các file Index chuẩn hóa trên tập 207 file hiện có.