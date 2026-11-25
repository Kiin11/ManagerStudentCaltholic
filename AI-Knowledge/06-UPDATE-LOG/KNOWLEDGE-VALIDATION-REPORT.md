# BÁO CÁO KIỂM ĐỊNH ĐỘC LẬP KHO TRI THỨC (KNOWLEDGE VALIDATION REPORT)

> **Vai trò thực hiện:** Independent Codebase Knowledge Auditor  
> **Đối tượng kiểm định:** Thư mục `AI-Knowledge/` sau đợt cập nhật ngày `2026-11-24` (đối chiếu với mã nguồn thực tế trên nhánh `main` tại commit `37a30f3` — bao gồm `PR #99` `Fix-Sec-Inf` tại `e147f88` và OAuth2 Refresh Token Google Drive tại `37a30f3`)  
> **Quy tắc kiểm định:** Chỉ kiểm chứng từ source code và cấu hình hiện có trong repository; không tự sửa source code hay tài liệu kiến trúc trong bước audit; mọi hạng mục thiếu source ngoài repository hoặc thiếu bằng chứng runtime được đánh dấu `NEEDS_VERIFICATION`.

---

## 1. Kết Luận Tổng Thể (Overall Audit Verdict)

> ### ⚠️ **TRẠNG THÁI KIỂM ĐỊNH: `PASS WITH WARNINGS` (Cần chỉnh sửa đồng bộ chỉ mục & 2 sơ đồ luồng trước khi nâng baseline)**

- **Điểm đạt chuẩn cao:** Tài liệu kiến trúc cốt lõi (`ARCHITECTURE.md`, `CODING-RULES.md`, `ATTENDANCE.md`, `INTEGRATION-OVERVIEW.md`, `EXTERNAL-APIS.md`, `FAST-QR-CHECKIN-AND-OFFLINE-SYNC.md`, `FEAT-P5-01`, `FEAT-P5-02`, `FEAT-P5-08` phần chi tiết, `FEAT-P6-01`, `02-DATABASE/*`) phản ánh chính xác hành vi bảo mật, Write-Behind/Write-Through, OAuth2 Refresh Token Google Drive, và 44 `DbSet<T>` trong source code hiện hành; toàn bộ file JSON/JSONL (`100%`) hợp lệ cú pháp và không còn bất kỳ tham chiếu giả lập/tệp đã xóa nào (`GradesController_copy.cs`, `fix-db-1.txt`, domain giả lập).
- **Lý do chưa đạt `PASS` tuyệt đối:**
  1. Phát hiện **`3` lỗi `HIGH`**, **`5` lỗi `MEDIUM`** (`2` lỗi tài liệu/index + `2` hạng mục `NEEDS_VERIFICATION` ngoài repository/DB runtime), và **`2` lỗi `LOW`**:
     - `Request Flow Diagram.md` và biểu đồ Mermaid trong `FEAT-P5-04` vẫn ghi luồng quét QR dùng **Write-Through** thay vì **Write-Behind** (`ISS-AUD-010`).
     - Bộ chỉ mục `03-CODE-INDEX/` vẫn giữ `lastIndexedCommit = "2a3263d"` nên tồn tại độ trôi vị trí dòng (`56` dòng trong `SYMBOL-INDEX.jsonl`, `43` dòng trong `ROUTE-INDEX.jsonl`, `17` dòng trong `DEPENDENCY-INDEX.jsonl`), `11` target symbol bị thiếu khai báo, `3` tệp Service/Interface thiếu trong `FILE-INDEX.jsonl`, và `16` tệp lõi thiếu trong `SOURCE-INVENTORY.md` / `PROJECT-INVENTORY.json` (`ISS-AUD-012`..`ISS-AUD-015`).
     - Chưa có source hạ tầng deploy (`parish-infra-config`: `docker-compose.yml`, `nginx/default.conf`) và file EF Migration trong repo (`Migrations/` có `0` file) nên Tiêu chí #5 và #6 ghi nhận trạng thái `NEEDS_VERIFICATION` (`ISS-AUD-017`, `ISS-AUD-018`).

---

## 2. Bảng Tổng Hợp 10 Tiêu Chí Kiểm Định (Validation Scorecard)

| STT | Tiêu chí kiểm định | Kết quả | Mức độ vấn đề | Ghi chú kiểm chứng |
|---|---|---|---|---|
| 1 | **Class, method, namespace & file path** | ⚠️ **WARNING** | `HIGH` | `FILE-INDEX` `0` path hỏng nhưng thiếu `3` Service/Interface; `SYMBOL-INDEX` lệch `56/264` dòng/chữ ký & thiếu `11` symbol (`ISS-AUD-012`, `ISS-AUD-015`). |
| 2 | **Route & Controller Action** | ⚠️ **WARNING** | `HIGH` | Toàn bộ 69 route template hợp lệ, nhưng `43/69` `startLine` bị lệch và thiếu `POST /Attendance/FlushRedisToDatabase` (`ISS-AUD-013`). |
| 3 | **Dependency có bằng chứng** | ⚠️ **WARNING** | `MEDIUM` | Không có dependency bịa đặt; nhưng `17/116` cạnh lệch `sourceLine`, thiếu cạnh Write-Behind/ProfileHistory, `2` DI chưa có caller (`ISS-AUD-014`, `ISS-AUD-019`). |
| 4 | **Feature document đúng luồng xử lý** | ⚠️ **WARNING** | `HIGH` | `ATTENDANCE.md`, `FAST-QR`, `FEAT-P5-01/02/08/P6-01` đúng source; nhưng `Request Flow Diagram.md` & `FEAT-P5-04:46` vẫn ghi sai là Write-Through (`ISS-AUD-010`). |
| 5 | **Database table, column & relationship** | 🔍 **NEEDS_VERIFICATION** | `MEDIUM` | `44 DbSet<T>` trong `ParishDbContext.cs` khớp 100% `DATABASE-OVERVIEW.md` (44) & `TABLE-INDEX.json` (44); nhưng `Migrations/` có `0` file nên chưa xác minh trạng thái DB runtime (`ISS-AUD-017`). |
| 6 | **API & Integration** | 🔍 **NEEDS_VERIFICATION** | `MEDIUM` | Code Google Drive/Redis/Hangfire/Nginx headers khớp source; `TECH-STACK.md:22` & `FEAT-P5-08:15` thiếu OAuth2 ở tóm tắt (`ISS-AUD-011`); `parish-infra-config` ngoài repo (`ISS-AUD-018`). |
| 7 | **Tham chiếu tới class/method đã xóa** | ✅ **PASS** | — | `0` path hỏng trong `FILE-INDEX.jsonl`; `0` tham chiếu sót tới `GradesController_copy.cs`, `fix-db-1`, hoặc domain giả lập trong tài liệu lõi. *(Lưu ý `11` orphan ref trong `DEPENDENCY-INDEX` là symbol còn sống nhưng thiếu entry bên `SYMBOL-INDEX`, thuộc `ISS-AUD-012`.)* |
| 8 | **Tính hợp lệ JSON / JSONL** | ✅ **PASS** | — | `100%` file `.json` (`KNOWLEDGE-BASELINE.json`, `AFFECTED-FILES.json`, `MODULE-INVENTORY.json`, `PROJECT-INVENTORY.json`, `TABLE-INDEX.json`) và `.jsonl` (`FILE/SYMBOL/ROUTE/DEPENDENCY-INDEX.jsonl`, `KNOWLEDGE-ISSUES.jsonl`) parse thành công với `0` lỗi cú pháp. |
| 9 | **Tài liệu bị ảnh hưởng nhưng chưa cập nhật** | ⚠️ **WARNING** | `MEDIUM` | `SOURCE-INVENTORY.md` & `PROJECT-INVENTORY.json` thiếu `16` tệp active (`FEAT-P5-08`, Write-Behind, `ProfileHistoryService`, `GradeLevel`); `PROJECT.md` & `FEAT-P5-07` cần bổ sung liên kết ngắn (`ISS-AUD-015`, `ISS-AUD-016`). |
| 10 | **Đánh dấu rõ thông tin suy luận / thiếu bằng chứng** | ✅ **PASS** | — | Các vùng thiếu dữ liệu ngoài repo (`parish-infra-config`, `Migrations/`, DI unused) đều được gắn nhãn `NEEDS_VERIFICATION` rõ ràng; tiếp tục theo dõi qua `ISS-AUD-017`..`019`. |

---

## 3. Chi Tiết Kết Quả Kiểm Chứng Từng Tiêu Chí

### Tiêu chí 1 & 7: Class, Method, Namespace, File Path & Stale References
- `[Confirmed]` Toàn bộ `138` đường dẫn trong `AI-Knowledge/03-CODE-INDEX/FILE-INDEX.jsonl` đều tồn tại trên ổ đĩa (`0` file bị xóa hoặc đổi tên sót).
- `[Confirmed]` Không còn tệp `GradesController_copy.cs` trên repository và không có bất kỳ tham chiếu sai sang domain ngoài dự án trong các thư mục `00-OVERVIEW/`..`05-FEATURES/`.
- `[Gap - ISS-AUD-015]` Thiếu `3` tệp Service/Interface đang hoạt động trong `FILE-INDEX.jsonl`: `Services/AttendanceWriteBehindBackgroundService.cs`, `Services/ProfileHistoryService.cs`, `Interface/Services/IProfileHistoryService.cs`.
- `[Gap - ISS-AUD-012]` Quét đối chiếu `SYMBOL-INDEX.jsonl` (`264` dòng) với AST thực tế tại `HEAD` (`37a30f3`) ghi nhận **`56` dòng lệch `startLine` / chữ ký** trên `20` file nguồn (ví dụ `IRedisAttendanceService.ProcessQrCheckInViaRedisAsync` tại `:31`, `UndoScanCheckInAsync` tại `:36`, `ClassCurriculumService.SaveCurriculumAsync` tại `:298`, `ExportCurriculumToExcelAsync` tại `:546`), và **`11` symbolKey trong `DEPENDENCY-INDEX.jsonl` chưa có dòng khai báo trong `SYMBOL-INDEX.jsonl`** (`AbsenceController.Review`, `BackgroundJobsController.DownloadExport`, `GovernanceController.ChangeExecutiveRole`, `IDriveLogUploadService.RunUploadCycleAsync`, `StudentViewModel.CalculateAgeAtDate`, `AttendanceDisputeService.ReviewDisputeByTeacherAsync`, `BackgroundExportJobService.GenerateClassExcelReportJobAsync`, `ClassEvaluationService.SubmitClassEvaluationAsync`, `LogRetentionBackgroundService.CleanUpOldLogsAndTempExports`, `MassCampaignService.UpdateSingleCellAsync`, `VietnamTimeHelper.Today`).

### Tiêu chí 2: Route & Controller Action
- `[Confirmed]` Các route mới quan trọng đều đã hiện diện và khớp hành động thực tế: `GET /health` (`Program.cs:216`), `GET /CloudMedia/File/{id:long}` (`CloudMediaController.cs:39`), `POST /LessonPlans/UploadDocument` (`:403`), `GET /LessonPlans/DownloadDocument/{id:long}` (`:521-522`), `POST /LessonPlans/DeleteDocument` (`:583`), `GET /Attendance/GetOfflineStudentsCache` (`:1547`), `POST /Attendance/SyncOfflineQueue` (`:1655`).
- `[Gap - ISS-AUD-013]` `43 / 69` dòng trong `ROUTE-INDEX.jsonl` có `startLine` cũ trước đợt chỉnh sửa `Fix-Sec-Inf` trên 10 Controller; đồng thời thiếu route `POST /Attendance/FlushRedisToDatabase` (`AttendanceController.cs:2475`).

### Tiêu chí 3: Dependency Có Bằng Chứng
- `[Confirmed]` Mọi cạnh phụ thuộc mới bổ sung cho 4 helper (`VietnamTimeHelper`, `AgeValidationHelper`, `FileUploadSecurityHelper`, `StaffSelfActionGuard`), `LogRetentionBackgroundService` singleton, và `ClassCurriculumService` đều khớp 100% lời gọi và constructor DI trong source code.
- `[Gap - ISS-AUD-014]` `17 / 116` dòng trong `DEPENDENCY-INDEX.jsonl` bị lệch `sourceLine` trên 6 file nguồn, đồng thời thiếu cạnh phụ thuộc cho `AttendanceWriteBehindBackgroundService` (`IAttendanceFlushSignal`, `IRedisAttendanceService`) và `ProfileHistoryService`.

### Tiêu chí 4: Tính Chính Xác Của Tài Liệu Feature & Luồng Xử Lý
- `[Confirmed]` Các tài liệu `ATTENDANCE.md`, `FAST-QR-CHECKIN-AND-OFFLINE-SYNC.md`, `FEAT-P5-01`, `FEAT-P5-02`, `FEAT-P5-07`, `FEAT-P5-08` (§2-§7), `FEAT-P6-01` phản ánh chính xác hành vi thực thi (bao gồm cookie expiration 14d/8d, JWT 11520 phút = 8 ngày, `GenerateExcelToMemoryStream` `:1039`, `MaxUploadBytes = 30 MB`, 64-stripe `FolderLockStripes`, `MaxLogFileBytes = 200 MB`, `RetryDelaySeconds = 10`, Redis-First + Write-Behind ~5s/25 scans).
- `[Gap - ISS-AUD-010]` `AI-Knowledge/00-OVERVIEW/Request Flow Diagram.md` (`lines 22-36`) và biểu đồ Mermaid tại `AI-Knowledge/05-FEATURES/FEAT-P5-04-QR-SCANNER-UPGRADE.md` (`line 46`) vẫn mô tả luồng `ScanCheckIn` online dùng **Write-Through** thay vì **Write-Behind**, gây mâu thuẫn nội bộ giữa các tài liệu kiến trúc.

### Tiêu chí 5: Database Table, Column & Relationship
- `[Confirmed]` `ManagerStudentCaltholic/Data/ParishDbContext.cs` khai báo chính xác **`44` `DbSet<T>`** (bao gồm `PromotionRuleConfigs` ở dòng 51 và `CloudMediaFiles` ở dòng 71+). Số lượng bảng trong `DATABASE-OVERVIEW.md` (`44` dòng) và `TABLE-INDEX.json` (`44` bảng trong schema `public`) khớp nhau hoàn toàn.
- `[NEEDS_VERIFICATION - ISS-AUD-017]` Thư mục `ManagerStudentCaltholic/Migrations/` hiện trống (`0` file). Việc thực thi `script/20261123_CloudMediaFiles_GoogleDrive_FeatP5_08.sql` và các script SQL trước đó trên cơ sở dữ liệu PostgreSQL thực tế chưa thể kiểm chứng từ source code.

### Tiêu chí 6: API & Integration
- `[Confirmed]` Chi tiết `GoogleDriveOptions`, cơ chế ưu tiên **OAuth2 Refresh Token** (`HasOAuthCredentials` trong `GoogleDriveStorageService.cs:766-836`) và dự phòng **Service Account JSON**, giới hạn `30 MB` spool, chunking `256 KB..2 MB`, cùng cấu hình `Dockerfile` (`HEALTHCHECK` gọi `/health`) được mô tả đúng trong `INTEGRATION-OVERVIEW.md` và `EXTERNAL-APIS.md`.
- `[Gap - ISS-AUD-011]` Phần tóm tắt tại `TECH-STACK.md:22` và `FEAT-P5-08:15` vẫn chỉ nhắc Service Account mà chưa cập nhật OAuth2 Refresh Token từ commit `37a30f3`.
- `[NEEDS_VERIFICATION - ISS-AUD-018]` Cấu hình `docker-compose.yml` và `nginx/default.conf` nằm ở repo ngoài (`parish-infra-config`).

### Tiêu chí 8, 9 & 10: JSON/JSONL, Tài Liệu Sót Cập Nhật & Nhãn `NEEDS_VERIFICATION`
- `[Confirmed]` `100%` tệp JSON và JSONL trong `AI-Knowledge/` hợp lệ cú pháp (`0` lỗi parse).
- `[Gap - ISS-AUD-015, ISS-AUD-016]` `SOURCE-INVENTORY.md` và `PROJECT-INVENTORY.json` còn thiếu `16` tệp C# lõi của `FEAT-P5-08`, Write-Behind, `ProfileHistoryService` và `GradeLevel`; `PROJECT.md` §3 và `FEAT-P5-07` §5 cần thêm chú thích dẫn chiếu ngắn tới cụm Cloud Media / Document endpoints.

---

## 4. Danh Sách Tổng Hợp Các Vấn Đề Phát Hiện (`OPEN` = 10)

| Mã vấn đề | Mức độ | Tiêu chí | File Knowledge bị ảnh hưởng | Tóm tắt ngắn |
|---|---|---|---|---|
| `ISS-AUD-010` | **HIGH** | #4 | `Request Flow Diagram.md`, `FEAT-P5-04` | Sơ đồ vẫn ghi `ScanCheckIn` là Write-Through thay vì Write-Behind. |
| `ISS-AUD-012` | **HIGH** | #1, #7 | `SYMBOL-INDEX.jsonl`, `DEPENDENCY-INDEX.jsonl` | `56` dòng/chữ ký bị lệch dòng và `11` target symbol thiếu khai báo. |
| `ISS-AUD-013` | **HIGH** | #2 | `ROUTE-INDEX.jsonl` | `43` `startLine` route bị lệch và thiếu `POST /Attendance/FlushRedisToDatabase`. |
| `ISS-AUD-011` | **MEDIUM** | #6, #9 | `TECH-STACK.md`, `FEAT-P5-08` | Phần tóm tắt chưa bổ sung chế độ xác thực OAuth2 Refresh Token (`37a30f3`). |
| `ISS-AUD-014` | **MEDIUM** | #3 | `DEPENDENCY-INDEX.jsonl` | `17` `sourceLine` bị lệch và thiếu cạnh cho Write-Behind / ProfileHistory. |
| `ISS-AUD-015` | **MEDIUM** | #1, #9 | `SOURCE-INVENTORY.md`, `PROJECT-INVENTORY.json`, `FILE-INDEX.jsonl` | Thiếu `16` file C# active trong Inventory và `3` Service/Interface trong `FILE-INDEX`. |
| `ISS-AUD-017` | **MEDIUM** | #5, #10 | `DATABASE-OVERVIEW.md` | `Migrations/` rỗng (`0` file), DDL PostgreSQL thực tế chưa xác minh được từ source (`NEEDS_VERIFICATION`). |
| `ISS-AUD-018` | **MEDIUM** | #6, #10 | `PROJECT.md`, `TECH-STACK.md`, `04-INTEGRATIONS/*` | `docker-compose.yml` & `nginx/default.conf` ở repo ngoài `parish-infra-config` (`NEEDS_VERIFICATION`). |
| `ISS-AUD-016` | **LOW** | #9 | `PROJECT.md`, `FEAT-P5-07` | Bổ sung cụm Cloud Media vào `PROJECT.md` §3 và dẫn chiếu 3 route tab tài liệu ở `FEAT-P5-07` §5. |
| `ISS-AUD-019` | **LOW** | #3, #10 | `DEPENDENCY-INDEX.jsonl`, `UPDATE-REPORT.md` | `IGradePermissionService` & `ILessonPlanImportService` chưa có caller thực thi (`NEEDS_VERIFICATION`). |

---

## 5. Điểm Chưa Đủ Căn Cứ Xác Minh Từ Source Code (`NEEDS_VERIFICATION`)

1. **Thực thi SQL Schema trên PostgreSQL đích (`ISS-AUD-017`):** `ManagerStudentCaltholic/Migrations/` có `0` file; không thể tự xác nhận trực tiếp từ Git xem `script/20261123_CloudMediaFiles_GoogleDrive_FeatP5_08.sql` đã chạy thành công trên môi trường Staging/Production hay chưa.
2. **Hạ tầng triển khai ngoài Repository `parish-infra-config` (`ISS-AUD-018`):** Chưa có `docker-compose.yml` và `nginx/default.conf` trong workspace để kiểm chứng `SSL Mode=VerifyCA`/`VerifyFull`, Redis `--requirepass`/`maxmemory-policy`, biến môi trường `GoogleDrive__OAuth*`, và cấu hình mạng `ReverseProxy:KnownNetworks`.
3. **Hai Service DI chưa được gọi (`ISS-AUD-019`):** `IGradePermissionService` (`ServiceCollectionExtensions.cs:188`) và `ILessonPlanImportService` (`FacilitiesController.cs:20`) hiện chưa thấy lời gọi method thực tế.
