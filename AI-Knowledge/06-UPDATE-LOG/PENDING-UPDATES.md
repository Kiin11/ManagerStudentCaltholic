# PENDING UPDATES - AI-KNOWLEDGE

> **Trạng thái cập nhật gần nhất:** `2026-11-20` (Bổ sung đặc tả thiết kế Phase 6 `FEAT-P6-01` — `PLANNED`)  
> **Tiến độ xử lý Issue kiểm định:** `9/9` issues (`ISS-AUD-001` ➔ `ISS-AUD-009`) đã được **khắc phục (`RESOLVED`)**.

## 1. Lộ trình Triển khai Phase 6 (`FEAT-P6-01` — `PLANNED`)
* **Tài liệu đặc tả:** `AI-Knowledge/05-FEATURES/FEAT-P6-01-PROBATIONARY-TEACHER-AND-AFTERNOON-SESSION.md`
* **Nguồn thiết kế:** Google Spreadsheet Phase 6 (`1q04ZBxPKqJu8r2XFFdQwm2NAHkOPiEUSjbw4catSIUo`, V3 Final — `14` sub-tasks, `10.50` ngày công, trạng thái hiện tại trên Sheet: `Chưa làm`).
* **Các nhóm công việc chờ triển khai mã nguồn C# & Migration:**
  * `TASK-01` (`TASK-01.1` ➔ `TASK-01.4`): Database & Migration (`UserRole.ProbationaryTeacher`, `User.LinkedStudentId`, khối `DU_BI` & `ClassRoom.Session`, `Student.FirstCommunionDate` / `BaoDongPromiseDate` / `CatechistPromiseDate`, EF Core Migration).
  * `TASK-02` (`TASK-02.1` ➔ `TASK-02.3`): Authorization & Policy (`RequireStaff`, `RequireTeachingStaff`, `FeaturePermissionRules`, khóa phạm vi Ca Sáng trong `GradePermissionService` chống IDOR).
  * `TASK-03` (`TASK-03.1` ➔ `TASK-03.4`): Quản lý Lớp Chiều & Tuyển hứa (khởi tạo 4 lớp `Dự Bị 1..4`, chặn Dự Bị dạy ca Chiều, cấp tài khoản Dự Bị từ hồ sơ thiếu nhi, form Tuyên hứa GLV `>= 18` tuổi & nâng Role).
  * `TASK-04` (`TASK-04.1` ➔ `TASK-04.3`): Giao diện Profile Lịch sử Kép (lịch sử học ca Chiều + lịch sử dạy ca Sáng song song theo niên khóa, timeline 5 cột mốc Đức tin & Tuyên hứa).
* **Danh sách tài liệu `AI-Knowledge/` cần đồng bộ sau khi code Phase 6 được thực thi:**
  * `AI-Knowledge/02-DATABASE/` (`DATABASE-OVERVIEW.md`, `TABLE-INDEX.json`, `TABLE-RELATIONSHIPS.md`)
  * `AI-Knowledge/03-CODE-INDEX/` (`FILE-INDEX.jsonl`, `SYMBOL-INDEX.jsonl`, `ROUTE-INDEX.jsonl`, `DEPENDENCY-INDEX.jsonl`)
  * `AI-Knowledge/00-OVERVIEW/` (`PROJECT.md`, `ARCHITECTURE.md`, `TECH-STACK.md`, `MODULE-INVENTORY.json`, `SOURCE-INVENTORY.md`)
  * `AI-Knowledge/05-FEATURES/FEAT-P6-01-PROBATIONARY-TEACHER-AND-AFTERNOON-SESSION.md` (chuyển trạng thái sang `COMPLETED`) và `AI-Knowledge/06-UPDATE-LOG/KNOWLEDGE-BASELINE.json`

## 2. Hạng Mục Đang Chờ / Mở Rộng Tiếp Theo (Non-blocking Backlog)
* Không còn cảnh báo hay sai lệch mức `HIGH` / `MEDIUM` / `LOW` nào từ đợt kiểm định độc lập (`KNOWLEDGE-VALIDATION-REPORT.md`).
* **Ghi chú xác nhận từ Developer (`2026-11-15`):**
  * **`ISS-AUD-002`:** Developer đã xác nhận chạy và kiểm chứng đầy đủ migrations/DDL trên PostgreSQL thực tế (không cần gắn cờ chờ kiểm tra thêm).
  * **`ISS-AUD-005`:** Luồng đồng bộ điểm danh trực tiếp (**Write-Through** ở cả `ScanCheckIn` và `SaveBatch`) đã được đồng bộ hóa giữa mã nguồn, XML comments và toàn bộ tài liệu Markdown (`ATTENDANCE.md`, `ARCHITECTURE.md`, `Request Flow Diagram.md`, `INTEGRATION-OVERVIEW.md`).
  * **Mở rộng tùy chọn trong tương lai:** Bổ sung chi tiết symbol cấp phương thức cho nhóm Controller phụ trợ còn lại hoặc thêm vào `ROUTE-INDEX.jsonl` khi phát sinh endpoint mới.
