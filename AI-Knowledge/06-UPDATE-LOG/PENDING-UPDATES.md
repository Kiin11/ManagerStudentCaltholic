# PENDING UPDATES - AI-KNOWLEDGE

> **Trạng thái cập nhật gần nhất:** `2026-11-24` (Đồng bộ `PR #99` / `Fix-Sec-Inf` tại commit `e147f88` trên nhánh `main`)  
> **Tiến độ xử lý Issue kiểm định:** `9/9` issues (`ISS-AUD-001` ➔ `ISS-AUD-009`) đã **khắc phục (`RESOLVED`)**; `13/17` hạng mục bảng `Ke_Hoach_Va_Loi_Bao_Mat_Logic_ManagerStudentCaltholic` đã vá trong mã nguồn, `4/17` hạng mục hạ tầng (`SEC-INF-01..04`) đang xử lý phía Ops.

## 1. Trạng thái Đồng bộ Đợt `Fix-Sec-Inf` (`e183ccd..e147f88`)
* **Đã cập nhật trong `AI-Knowledge/`:** `00-OVERVIEW/` (`PROJECT.md`, `ARCHITECTURE.md`, `CODING-RULES.md`, `TECH-STACK.md`, `SOURCE-INVENTORY.md`, `PROJECT-INVENTORY.json`), `01-MODULES/ATTENDANCE.md`, `03-CODE-INDEX/*.jsonl`, `04-INTEGRATIONS/` (`INTEGRATION-OVERVIEW.md`, `EXTERNAL-APIS.md`), `05-FEATURES/` (`FAST-QR`, `FEAT-P5-01`, `04`, `07`, `08`, `FEAT-P6-01`), và `06-UPDATE-LOG/` (`CHANGE-ANALYSIS.md`, `IMPACT-ANALYSIS.md`, `KNOWLEDGE-UPDATE-PLAN.md`, `AFFECTED-FILES.json`, `KNOWLEDGE-BASELINE.json`, `UPDATE-LOG.md`, `PENDING-UPDATES.md`, `UPDATE-REPORT.md`).
* **Không thay đổi (`02-DATABASE/`):** Không có thay đổi Entity/Schema/Migration trong `e183ccd..e147f88` (chỉ xóa tệp cũ `script/fix-db-1.txt`).

## 2. Hạng Mục Đang Chờ / Mở Rộng Tiếp Theo (`NEEDS_VERIFICATION` & Backlog)
1. **Tái quét đầy đủ `03-CODE-INDEX/*.jsonl` cho khoảng trung gian `2a3263d..4589783` (`Phase 6/7`):**
   * Các commit `e183ccd` và `e147f88` cập nhật tăng dần phần `FEAT-P5-08` và `Fix-Sec-Inf`, trong khi `lastIndexedCommit` vẫn giữ `2a3263d` chờ một đợt quét đầy đủ toàn bộ symbol/route mới của `GradeLevel`, `ProfileHistoryService`, `SacramentsController`, `GradesController`, `ClassesController`, `UsersController`, `GovernanceController`, v.v.
2. **Bổ sung đặc tả chuyên đề Học vụ, Bí tích & Báo cáo Hiệu năng (Tùy chọn):**
   * Hiện chưa có tài liệu riêng trong `01-MODULES/` hoặc `05-FEATURES/` cho `GradesController` / `GradeCalculationService`, `SacramentsController` / `SacramentEvaluationService`, `BackgroundExportJobService`, và `LogsController`.
3. **Xác minh Hạ tầng Ngoài Repository (`parish-infra-config` & Production VM):**
   * Xác nhận `SEC-INF-01` (quét `gitleaks`, xoay vòng secret và `git filter-repo` hoặc chuyển Private), `SEC-INF-03` (`SSL Mode=VerifyCA`/`VerifyFull`), `SEC-INF-04` (Redis `--requirepass`), và `SEC-APP-05` (dải IP `parish_nginx` khớp `ReverseProxy:KnownNetworks`).
4. **Dọn Service Chưa có Caller Trực tiếp (`IGradePermissionService`, `ILessonPlanImportService`):**
   * Xác nhận xem `IGradePermissionService` (chỉ đăng ký DI) và `ILessonPlanImportService` (chỉ inject vào `FacilitiesController` nhưng chưa gọi method) có tiếp tục giữ dự phòng hay cần tích hợp/gỡ bỏ.
