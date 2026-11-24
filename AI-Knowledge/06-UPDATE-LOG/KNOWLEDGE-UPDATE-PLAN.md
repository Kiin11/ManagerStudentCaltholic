# KẾ HOẠCH CẬP NHẬT KHO TRI THỨC AI (KNOWLEDGE UPDATE PLAN)

- **Dự án:** `ManagerStudentCaltholic`
- **Baseline hiện tại (`KNOWLEDGE-BASELINE.json`):**
  - `lastFeatureSync`: `FEAT-P5-08` (`lastFeatureSyncCommit = 4589783`, kèm commit tài liệu `e183ccd`)
  - `lastIndexedCommit`: `2a3263d` *(cần bắt kịp cả khoảng `2a3263d..4589783` khi làm mới `03-CODE-INDEX/`)*
- **Phiên bản mục tiêu phân tích (`HEAD` trên `main`):** `e147f88` (`Merge pull request #99 from Kiin11/Fix-Sec-Inf`)
- **Quy tắc bước phân tích:** **KHÔNG cập nhật các tài liệu Knowledge chính ở bước này**; tài liệu này lập lộ trình cập nhật chính xác cho đợt đồng bộ kế tiếp.

---

## 1. Danh sách Tài liệu `AI-Knowledge/` Cần Cập nhật

| STT | Đường dẫn tài liệu | Phân loại | Ưu tiên | Nội dung cần cập nhật (đã xác minh từ source) |
|---|---|---|---|---|
| 1 | `AI-Knowledge/AI-KNOWLEDGE-STRUCTURE.md` | Modified | Medium | Rà soát phản ánh các file mới trong `06-UPDATE-LOG/` và việc gỡ 3 file trùng lặp ở root (`KNOWLEDGE-ISSUES.jsonl`, `KNOWLEDGE-VALIDATION-REPORT.md`, `REQUIRED-CORRECTIONS.md`). |
| 2 | `AI-Knowledge/00-OVERVIEW/PROJECT.md` | Modified | High | Bổ sung 4 helper chuẩn (`VietnamTimeHelper`, `AgeValidationHelper`, `FileUploadSecurityHelper`, `StaffSelfActionGuard`), endpoint `/health`, bộ unit test `tests/ManagerStudentCaltholic.Tests`, và lớp phòng thủ IDOR/XSS/Upload/Anti-Abuse. |
| 3 | `AI-Knowledge/00-OVERVIEW/ARCHITECTURE.md` | Modified | High | Cập nhật kiến trúc pipeline (`AddReverseProxyConfiguration(IConfiguration)`, `GlobalLimiter` `200 req/min`, `/health`), luồng khóa nguyên tử Hoàn tác QR (`ClassFlushLock -> ClassSheetLock`), idempotency `ATT:OFFLINE:IDEMP:*` và `PhysicalFile` / `Stream` cho xuất/tải tệp tránh OOM. |
| 4 | `AI-Knowledge/00-OVERVIEW/CODING-RULES.md` | Modified | High | Bổ sung quy chuẩn bắt buộc dùng `VietnamTimeHelper` (cấm `DateTime.UtcNow.Date`/`toISOString().split('T')[0]` cho ngày nghiệp vụ VN), `AgeValidationHelper` (năm nhuận 29/02), `FileUploadSecurityHelper` (magic bytes + safe MIME + `TryResolveContainedPath`), `StaffSelfActionGuard`, và cấm `@Html.Raw`/unescaped `innerHTML`. |
| 5 | `AI-Knowledge/00-OVERVIEW/TECH-STACK.md` | Modified | High | Ghi nhận cấu hình `ReverseProxy` trong `appsettings.json`, `RedisConnection = ""` mặc định, `GoogleDrive:Enabled = false` / `DriveLogUpload:Enabled = false` mặc định (`997507f`), Docker `HEALTHCHECK` (`http://127.0.0.1:8080/health`), và bộ test `xUnit 2.9.3` + `EF Core InMemory 8.0.11`. |
| 6 | `AI-Knowledge/00-OVERVIEW/SOURCE-INVENTORY.md` & `PROJECT-INVENTORY.json` & `MODULE-INVENTORY.json` | Modified | Medium | Bổ sung 4 helper mới under `Services/`, test project `tests/ManagerStudentCaltholic.Tests/`, và loại bỏ tham chiếu tới `script/fix-db-1.txt`. |
| 7 | `AI-Knowledge/01-MODULES/ATTENDANCE.md` | Modified | High | Cập nhật `IRedisAttendanceService` overloads (`ClaimsPrincipal? caller`, `ExecuteWithPartitionsFlushLockAsync`, `FinalizePartitionsAfterExternalSyncAsync`), khóa phân dải `ClassFlushLocks`, idempotency `ATT:OFFLINE:IDEMP:*`, xử lý `23505`, grace 15s + kiểm tra người quét gốc khi Hoàn tác, `StaffSelfActionGuard`, fail-closed `BranchHead`, và `AttendanceDisputeService` thông báo lỗi chung. |
| 8 | `AI-Knowledge/05-FEATURES/FAST-QR-CHECKIN-AND-OFFLINE-SYNC.md` | Modified | High | Cập nhật `QrScanResponseDto.IsForbidden`, `OfflineSyncItemDto.SessionMode`, đánh giá trạng thái phía Server theo `VietnamTimeHelper.ToVietnamTime`, `offline-attendance.js` (`DB_VERSION = 2`, `by_enrollmentId` non-unique, xóa đúng `syncedClientIds`, `formatLocalDateYyyyMmDd`), và fallback `/Attendance/UndoScanCheckIn` từ `ScanQr.cshtml`. |
| 9 | `AI-Knowledge/05-FEATURES/FEAT-P5-04-QR-SCANNER-UPGRADE.md` | Modified | Medium | Ghi nhận tắt nút Hoàn tác khi `isDuplicate === true`, xử lý HTTP `403`, DOM `textContent` chống XSS, và fallback server undo khi offline đã đồng bộ. |
| 10 | `AI-Knowledge/05-FEATURES/FEAT-P5-01-STAFF-QUICK-LOOKUP.md` | Modified | High | Cập nhật `[Authorize(Policy = "RequireStaff")]` trên các API tra cứu, overload nhận `ClaimsPrincipal` trên `IStaffStudentLookupService` (`SearchStudentsQuickAsync`, `CanCallerAccessStudentAsync`, `GetRecentLookupsAsync`), chặn truy cập cache `staff_lookup:student_360:{studentId}` nếu ngoài phạm vi (`403`), và ủy quyền sự kiện thay inline `onclick` trong `staff-student-lookup.js`. |
| 11 | `AI-Knowledge/05-FEATURES/FEAT-P5-07-CLASS-CURRICULUM-MANAGEMENT.md` | Modified | High | Cập nhật đổi chữ ký `IClassCurriculumService.GetVersionFileAsync` từ `byte[]` sang `Stream`, giới hạn 15 MB + kiểm tra magic bytes, kiểm tra `ver.ClassCurriculumDocumentId == documentId`, fail-closed khi `ClassRoom == null`, và escape HTML trong modal xem chi tiết phiên bản. |
| 12 | `AI-Knowledge/05-FEATURES/FEAT-P5-08-GOOGLE-DRIVE-STORAGE-AND-LOG-BACKJOB.md` | Modified | High | Cập nhật kiểm tra quyền `AVATAR` trong `CloudMediaController`, `Content-Security-Policy: sandbox` cho PDF, ép `attachment` cho `ABSENCE_DOC`, giới hạn `MaxUploadBytes = 30 MB` & 64-stripe `FolderLockStripes` trong `GoogleDriveStorageService`, xóa bù log verified / giới hạn `200 MB` / `RetryDelaySeconds` trong `DriveLogUploadService`, gộp singleton `LogRetentionBackgroundService` & dọn `tmp_logs`/`cloud_tmp` >2h. |
| 13 | `AI-Knowledge/05-FEATURES/FEAT-P6-01-PROBATIONARY-TEACHER-AND-AFTERNOON-SESSION.md` | Modified | High | Cập nhật chuẩn hóa `AgeValidationHelper` (năm nhuận `29/02`) trên `StudentViewModel`, `StudentsController`, `AccountController`, `UsersController`, `GovernanceController`, `ProfileHistoryService`; đồng bộ `LinkedUser`; khóa `ProbationaryTeacher` ca chiều & `StaffSelfActionGuard` tại mọi module. |
| 14 | `AI-Knowledge/04-INTEGRATIONS/INTEGRATION-OVERVIEW.md` & `EXTERNAL-APIS.md` | Modified | Medium | Cập nhật cấu hình mặc định `GoogleDriveOptions` / `DriveLogUploadOptions` (`Enabled = false`), endpoint `/health`, `GlobalLimiter` (`200 req/min`), và `ReverseProxy` config-driven trong `ServiceCollectionExtensions`. |
| 15 | `AI-Knowledge/03-CODE-INDEX/FILE-INDEX.jsonl` | Modified | High | Thêm 4 helper mới, 5 file trong `tests/ManagerStudentCaltholic.Tests/`, xóa mục cũ nếu có của `script/fix-db-1.txt`, và bắt kịp các file giữa `2a3263d..e183ccd` còn thiếu. |
| 16 | `AI-Knowledge/03-CODE-INDEX/SYMBOL-INDEX.jsonl` | Modified | High | Bổ sung/cập nhật symbols cho 4 helper mới, overloads mới của `IRedisAttendanceService`/`IStaffStudentLookupService`/`IGradeCalculationService`, chữ ký `Stream` của `IClassCurriculumService.GetVersionFileAsync`, `Options` defaults, `DTO` properties (`WebsiteUrlHoneypot`, `IsForbidden`, `SessionMode`), `SecurityExtensions.GetClientIpKey`. |
| 17 | `AI-Knowledge/03-CODE-INDEX/ROUTE-INDEX.jsonl` | Modified | High | Bổ sung route `GET /health` (`Program.cs:216`), cập nhật `startLine` & `[Authorize]` attribute mới trên 21 controllers (cùng các route Phase 6/7 còn thiếu từ mốc `2a3263d`). |
| 18 | `AI-Knowledge/03-CODE-INDEX/DEPENDENCY-INDEX.jsonl` | Modified | High | Bổ sung cạnh `CALLS` / `INJECTS` cho 4 helper mới (`VietnamTimeHelper`, `AgeValidationHelper`, `FileUploadSecurityHelper`, `StaffSelfActionGuard`), `ICacheService` lockout trong `LookupController`/`AbsenceController`/`PublicRegistrationController`, `IImageOptimizationService` trong `PublicRegistrationController`, sửa cạnh singleton `LogRetentionBackgroundService`. |
| 19 | `AI-Knowledge/06-UPDATE-LOG/KNOWLEDGE-BASELINE.json`, `UPDATE-LOG.md`, `PENDING-UPDATES.md` | Modified | Final | Sau khi hoàn tất cập nhật và kiểm tra cú pháp JSON/JSONL, nâng `lastFeatureSync` / `lastFeatureSyncCommit` lên `e147f88` (và `lastIndexedCommit` nếu đã quét đủ `03-CODE-INDEX/` từ `2a3263d`), ghi biên bản vào `UPDATE-LOG.md`. |

---

## 2. Lộ trình Thực thi Đề xuất (Step-by-Step Roadmap)

1. **Giai đoạn 1 — Kiểm tra Tồn đọng `2a3263d..e183ccd` & Cập nhật `03-CODE-INDEX/*.jsonl`:**
   - Quét đối chiếu bổ sung các tệp Phase 6/7 giữa `2a3263d..4589783` để `FILE-INDEX.jsonl`, `SYMBOL-INDEX.jsonl`, `ROUTE-INDEX.jsonl`, `DEPENDENCY-INDEX.jsonl` phản ánh trọn vẹn trạng thái `HEAD` (`e147f88`).
   - Thêm 4 helper mới, test project, breaking signature `GetVersionFileAsync` và route `/health`.
2. **Giai đoạn 2 — Cập nhật Kiến trúc & Quy chuẩn (`00-OVERVIEW/`, `04-INTEGRATIONS/`):**
   - Cập nhật `PROJECT.md`, `ARCHITECTURE.md`, `CODING-RULES.md`, `TECH-STACK.md`, `SOURCE-INVENTORY.md`, `INTEGRATION-OVERVIEW.md`, `EXTERNAL-APIS.md`, `AI-KNOWLEDGE-STRUCTURE.md`.
3. **Giai đoạn 3 — Cập nhật Module & Đặc tả Feature (`01-MODULES/`, `05-FEATURES/`):**
   - Cập nhật `ATTENDANCE.md`, `FAST-QR-CHECKIN-AND-OFFLINE-SYNC.md`, `FEAT-P5-04-QR-SCANNER-UPGRADE.md`, `FEAT-P5-01-STAFF-QUICK-LOOKUP.md`, `FEAT-P5-07-CLASS-CURRICULUM-MANAGEMENT.md`, `FEAT-P5-08-GOOGLE-DRIVE-STORAGE-AND-LOG-BACKJOB.md`, `FEAT-P6-01-PROBATIONARY-TEACHER-AND-AFTERNOON-SESSION.md`.
   - Cân nhắc bổ sung mục tài liệu chuyên đề cho nhóm Học vụ/Bí tích/Hiệu năng (`GradesController`, `SacramentEvaluationService`, `BackgroundExportJobService`, `LogsController`) vốn hiện chưa có file riêng trong `01-MODULES/` hoặc `05-FEATURES/`.
4. **Giai đoạn 4 — Kiểm định Cú pháp & Chốt Baseline (`06-UPDATE-LOG/`):**
   - Kiểm tra hợp lệ toàn bộ `.json` và `.jsonl`.
   - Cập nhật `KNOWLEDGE-BASELINE.json`, `UPDATE-LOG.md`, `PENDING-UPDATES.md` (giữ rõ phần `NEEDS_VERIFICATION` cho hạ tầng `parish-infra-config`).

---

## 3. Kiểm tra Bằng chứng & Dữ liệu Thiếu

- [x] Đã đối chiếu trực tiếp toàn bộ `git diff --numstat -M e183ccd..HEAD` (`93` tệp trong git diff, gồm `91` tệp ngoài `AI-Knowledge/`).
- [x] Đã kiểm tra và ghi nhận khoảng chênh `2a3263d..e183ccd` còn tồn tại trong `KNOWLEDGE-BASELINE.json`.
- [x] Mọi symbol, route, dòng code và quan hệ trong `IMPACT-ANALYSIS.md` đều phân biệt rõ `[Xác nhận được]` và `[Suy luận kiến trúc]`.
- [ ] **Cần xác minh bổ sung (`NEEDS_VERIFICATION`):** Xác nhận cấu hình thực tế tại repo ngoài `parish-infra-config` (`docker-compose.yml`, `nginx/default.conf`, chuỗi kết nối PostgreSQL/Redis trên Production) và quyết định sử dụng hay gỡ bỏ `IGradePermissionService` / `ILessonPlanImportService` chưa có caller thực thi.
