# KẾ HOẠCH CẬP NHẬT KHO TRI THỨC AI (KNOWLEDGE UPDATE PLAN)

- **Dự án:** `ManagerStudentCaltholic`
- **Phiên bản baseline hiện tại (`KNOWLEDGE-BASELINE.json`):** `c0e828a`
- **Phiên bản mục tiêu phân tích:** `2a3263d` (+ cập nhật cấu hình cục bộ 2026-11-15)
- **Quy tắc bước phân tích:** **KHÔNG cập nhật các tài liệu Knowledge chính ở bước này**; tài liệu này lập lộ trình cập nhật chính xác cho đợt đồng bộ kế tiếp.

---

## 1. Danh sách Tài liệu `AI-Knowledge/` Cần Cập nhật

| STT | Đường dẫn tài liệu | Phân loại | Ưu tiên | Nội dung cần cập nhật |
|---|---|---|---|---|
| 1 | `AI-Knowledge/AI-KNOWLEDGE-STRUCTURE.md` | Modified | Medium | Cập nhật cây thư mục Mục 2 phản ánh đúng các file thực tế (`ATTENDANCE.md`, các file `FEAT-P5-*`, `06-UPDATE-LOG/*`). |
| 2 | `AI-Knowledge/00-OVERVIEW/PROJECT.md` | Modified | High | Bổ sung `FEAT-P5-04` (QR HUD & Undo 5s), `FEAT-P5-07` (Quản lý kế hoạch lớp theo version), `PublicRegistration/Resubmit`, thời hạn phiên/Refresh Token 14 ngày, `PwaController.Offline`, Circuit Breaker Redis. |
| 3 | `AI-Knowledge/00-OVERVIEW/ARCHITECTURE.md` | Modified | High | Bổ sung `ClassCurriculumService` vào Service Layer, cập nhật `Flow 2` (Undo 5s & mã cũ `StudentCodeHistories`), thêm `Flow 6` cho `FEAT-P5-07`. |
| 4 | `AI-Knowledge/00-OVERVIEW/TECH-STACK.md` | Modified | High | Ghi nhận cấu hình Hybrid Auth thời hạn **14 ngày** (`ParishSessionCookie` + `RefreshTokenExpirationDays`), `DataProtection` persist keys, và `RedisCacheService` Circuit Breaker 30s + `IMemoryCache` fallback. |
| 5 | `AI-Knowledge/01-MODULES/ATTENDANCE.md` | Modified | High | Bổ sung luồng `UndoScanCheckIn` / `UndoScanCheckInAsync` (5s), tra cứu `StudentCodeHistories`, `undoOfflineQrScan`, liên kết tới `FEAT-P5-04-QR-SCANNER-UPGRADE.md`. |
| 6 | `AI-Knowledge/05-FEATURES/FAST-QR-CHECKIN-AND-OFFLINE-SYNC.md` | Modified | High | Bổ sung Flow hoàn tác 5 giây (`UndoScanRequestDto`), `HistoricalCodes` cho cache offline, tham chiếu `FEAT-P5-04-QR-SCANNER-UPGRADE.md`. |
| 7 | `AI-Knowledge/02-DATABASE/DATABASE-OVERVIEW.md` | Modified | Medium | Đảm bảo ghi nhận trạng thái ánh xạ ORM của `ClassCurriculumDocuments`, `ClassCurriculumVersions`, `ClassLessonPlans` và lưu ý thiếu file EF Migration trong `Migrations/`. |
| 8 | `AI-Knowledge/02-DATABASE/TABLE-INDEX.json` | Modified | High | Bổ sung schema chi tiết (columns, FKs, indexes) của 3 bảng `ClassLessonPlans`, `ClassCurriculumDocuments`, `ClassCurriculumVersions`. |
| 9 | `AI-Knowledge/02-DATABASE/TABLE-RELATIONSHIPS.md` | Modified | High | Thêm mục quan hệ bảng `Lesson Plans & Class Curriculum Versioning (FEAT-P5-07)`, cập nhật sơ đồ `erDiagram` Mermaid và khối JSON index. |
| 10 | `AI-Knowledge/03-CODE-INDEX/FILE-INDEX.jsonl` | Modified | High | Bổ sung `PublicRegistrationController.cs`, `PwaController.cs`, `StudentsController.cs`, `PublicRegistrationSubmitDto.cs`, `Resubmit.cshtml`, `ScanQr.cshtml`, `LessonPlans/Index.cshtml`, `offline-attendance.js`. |
| 11 | `AI-Knowledge/03-CODE-INDEX/SYMBOL-INDEX.jsonl` | Modified | High | Thêm symbols cho `FEAT-P5-07` (`ClassLessonPlan`, `ClassCurriculumDocument`, `ClassCurriculumVersion`, `IClassCurriculumService`, `ClassCurriculumService`, `LessonPlansController` actions), `UndoScanCheckIn*`, `UndoScanRequestDto`, `PublicRegistrationController` (`Resubmit`, `SubmitResubmit`), `PublicRegistrationResubmitDto`, `PwaController`, `AccountController.Profile(long? id = null)`. |
| 12 | `AI-Knowledge/03-CODE-INDEX/ROUTE-INDEX.jsonl` | Modified | High | Cập nhật `startLine` chính xác cho các action `LessonPlansController` và bổ sung routes `/Attendance/UndoScanCheckIn`, `/PublicRegistration/Resubmit`, `/PublicRegistration/SubmitResubmit`, `/Offline`, `/icons/{fileName}`. |
| 13 | `AI-Knowledge/03-CODE-INDEX/DEPENDENCY-INDEX.jsonl` | Modified | High | Chỉnh `sourceLine` của `ClassCurriculumService` (`17`, `23`) và bổ sung cạnh `CALLS`/`INJECTS` cho `AttendanceController.UndoScanCheckIn`, `LessonPlansController` actions và `PublicRegistrationController`. |
| 14 | `AI-Knowledge/06-UPDATE-LOG/KNOWLEDGE-BASELINE.json` | Modified | Final | Sau khi hoàn tất cập nhật và kiểm tra cú pháp JSON/JSONL, nâng `lastIndexedCommit` từ `c0e828a` lên `2a3263d`. |
| 15 | `AI-Knowledge/06-UPDATE-LOG/UPDATE-LOG.md` | Modified | Final | Ghi biên bản hoàn thành cho phiên đồng bộ `c0e828a` ➔ `2a3263d` (+ cấu hình Token/Cookie 14 ngày). |
| 16 | `AI-Knowledge/06-UPDATE-LOG/PENDING-UPDATES.md` | Modified | Final | Cập nhật trạng thái đồng bộ và ghi rõ mục `NEEDS_VERIFICATION` về Migration/DDL của `ClassCurriculum*`. |

---

## 2. Lộ trình Thực thi Đề xuất (Step-by-Step Roadmap)

1. **Giai đoạn 1 — Lớp Cơ sở dữ liệu (`02-DATABASE/`):**
   - Cập nhật `TABLE-INDEX.json` và `TABLE-RELATIONSHIPS.md` cho `ClassLessonPlans`, `ClassCurriculumDocuments`, `ClassCurriculumVersions`.
2. **Giai đoạn 2 — Chỉ mục Mã nguồn (`03-CODE-INDEX/*.jsonl`):**
   - Cập nhật `FILE-INDEX.jsonl`, `SYMBOL-INDEX.jsonl`, `ROUTE-INDEX.jsonl`, `DEPENDENCY-INDEX.jsonl` theo đúng line numbers đã kiểm chứng.
3. **Giai đoạn 3 — Tài liệu Tổng quan, Module & Feature (`00-OVERVIEW/`, `01-MODULES/`, `05-FEATURES/`):**
   - Cập nhật `AI-KNOWLEDGE-STRUCTURE.md`, `PROJECT.md`, `ARCHITECTURE.md`, `TECH-STACK.md`, `ATTENDANCE.md`, `FAST-QR-CHECKIN-AND-OFFLINE-SYNC.md`.
4. **Giai đoạn 4 — Kiểm tra & Chốt Baseline (`06-UPDATE-LOG/`):**
   - Kiểm tra hợp lệ cú pháp toàn bộ `.json` và `.jsonl`.
   - Sau khi xác nhận đạt 100%, cập nhật `KNOWLEDGE-BASELINE.json`, `UPDATE-LOG.md`, `PENDING-UPDATES.md`.

---

## 3. Kiểm tra Bằng chứng & Dữ liệu Thiếu

- [x] Đã đối chiếu trực tiếp toàn bộ git diff `c0e828a..HEAD` và working tree hiện hành.
- [x] Mọi symbol, endpoint và quan hệ trong `IMPACT-ANALYSIS.md` đều gắn rõ bằng chứng `[Xác nhận được]` hoặc `[Suy luận kiến trúc]`.
- [ ] **Cần xác minh bổ sung (`NEEDS_VERIFICATION`):** Xác nhận trạng thái DDL/bảng thực tế trên PostgreSQL cho `ClassCurriculumDocuments` và `ClassCurriculumVersions` do `Migrations/` hiện chưa có migration tương ứng.
