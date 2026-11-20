# PENDING UPDATES - AI-KNOWLEDGE

> **Trạng thái cập nhật gần nhất:** `2026-11-20` (Hoàn thành toàn bộ `14/14` Sub-tasks Phase 6 `FEAT-P6-01`: `TASK-01` ➔ `TASK-04`)  
> **Tiến độ xử lý Issue kiểm định:** `9/9` issues (`ISS-AUD-001` ➔ `ISS-AUD-009`) đã được **khắc phục (`RESOLVED`)**.

## 1. Trạng thái Đồng bộ Phase 6 (`FEAT-P6-01` — `14/14 COMPLETED`)
* **Tài liệu đặc tả:** `AI-Knowledge/05-FEATURES/FEAT-P6-01-PROBATIONARY-TEACHER-AND-AFTERNOON-SESSION.md` (`100.0%` hoàn thành trên Google Sheet).
* **SQL Script PostgreSQL:** Người dùng đã xác nhận chạy xong `script/20261120_Users_Classes_Students_FeaturePermissionRules_Phase6.sql`.
* **Đã đồng bộ trong `AI-Knowledge/`:** `02-DATABASE/` (`DATABASE-OVERVIEW.md`, `TABLE-INDEX.json`, `TABLE-RELATIONSHIPS.md`), `00-OVERVIEW/CODING-RULES.md`, `05-FEATURES/FEAT-P6-01-PROBATIONARY-TEACHER-AND-AFTERNOON-SESSION.md`, `06-UPDATE-LOG/` (`PENDING-UPDATES.md`, `UPDATE-LOG.md`).
* **Mở rộng tùy chọn sau commit Phase 6:** Khi chốt commit Phase 6 lên Git, chạy đồng bộ lại `03-CODE-INDEX/` (`FILE-INDEX.jsonl`, `SYMBOL-INDEX.jsonl`, `ROUTE-INDEX.jsonl`, `DEPENDENCY-INDEX.jsonl`), `00-OVERVIEW/` (`PROJECT.md`, `ARCHITECTURE.md`, `SOURCE-INVENTORY.md`) và `KNOWLEDGE-BASELINE.json`.

## 2. Hạng Mục Đang Chờ / Mở Rộng Tiếp Theo (Non-blocking Backlog)
* Không còn cảnh báo hay sai lệch mức `HIGH` / `MEDIUM` / `LOW` nào từ đợt kiểm định độc lập (`KNOWLEDGE-VALIDATION-REPORT.md`).
* **Ghi chú xác nhận từ Developer (`2026-11-15`):**
  * **`ISS-AUD-002`:** Developer đã xác nhận chạy và kiểm chứng đầy đủ migrations/DDL trên PostgreSQL thực tế (không cần gắn cờ chờ kiểm tra thêm).
  * **`ISS-AUD-005`:** Luồng đồng bộ điểm danh trực tiếp (**Write-Through** ở cả `ScanCheckIn` và `SaveBatch`) đã được đồng bộ hóa giữa mã nguồn, XML comments và toàn bộ tài liệu Markdown (`ATTENDANCE.md`, `ARCHITECTURE.md`, `Request Flow Diagram.md`, `INTEGRATION-OVERVIEW.md`).
  * **Mở rộng tùy chọn trong tương lai:** Bổ sung chi tiết symbol cấp phương thức cho nhóm Controller phụ trợ còn lại hoặc thêm vào `ROUTE-INDEX.jsonl` khi phát sinh endpoint mới.
