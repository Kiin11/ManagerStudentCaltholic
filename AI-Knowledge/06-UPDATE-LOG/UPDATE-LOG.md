# AI-KNOWLEDGE UPDATE LOG

## Lịch sử Cập nhật (Changelog)

### Phiên cập nhật: `7c61591` ➔ `c0e828a`
- **Thời gian cập nhật:** `2026-11-04T22:14:00+07:00`
- **Commit trước đó (`Baseline`):** `7c61591` (`feat: Implement attendance dispute submission and management...`)
- **Commit hiện tại (`HEAD`):** `c0e828a` (`perf(mobile): optimize mobile layout rendering, remove scroll listener & prevent SW reload loop`)
- **Tổng số file source thay đổi:** `19` files (`11 Added`, `8 Modified`, `0 Deleted`, `0 Renamed`).
- **Các tính năng / Module được cập nhật:**
  1. **`FEAT-P5-01` (Module `Common` / `StaffLookup`):** Cổng tra cứu 360 độ thiếu nhi nội bộ tại Trang chủ cho GLV/BĐH đã đăng nhập (`StaffStudentLookupService`, `IStaffStudentLookupService`, `LookupViewOption`, `StaffQuickLookupLog`, `StaffStudentLookupDtos`, `HomeController`, `_StaffStudentLookupWidget.cshtml`, `staff-student-lookup.js`).
  2. **`FEAT-P5-02` / `TASK-P5-02.1` (Module `Common` / Responsive UI):** Thiết kế khung giao diện Mobile-First với nhánh `@if (isMobile)` trong `_Layout.cshtml`, `HttpRequestExtensions.IsMobileDevice`, Header thu gọn `_LayoutMobileHeader.cshtml`, Thanh điều hướng đáy 5 phân khu `_LayoutMobileBottomNav.cshtml`, `mobile-layout.css`, và tối ưu vòng đời PWA trong `pwa-installer.js`.
- **Danh sách tài liệu `AI-Knowledge` đã cập nhật (Modified):**
  - `AI-Knowledge/00-OVERVIEW/PROJECT.md`
  - `AI-Knowledge/00-OVERVIEW/ARCHITECTURE.md`
  - `AI-Knowledge/00-OVERVIEW/TECH-STACK.md`
  - `AI-Knowledge/02-DATABASE/DATABASE-OVERVIEW.md`
  - `AI-Knowledge/02-DATABASE/TABLE-INDEX.json`
  - `AI-Knowledge/02-DATABASE/TABLE-RELATIONSHIPS.md`
  - `AI-Knowledge/03-CODE-INDEX/FILE-INDEX.jsonl`
  - `AI-Knowledge/03-CODE-INDEX/SYMBOL-INDEX.jsonl`
  - `AI-Knowledge/03-CODE-INDEX/ROUTE-INDEX.jsonl`
  - `AI-Knowledge/03-CODE-INDEX/DEPENDENCY-INDEX.jsonl`
  - `AI-Knowledge/06-UPDATE-LOG/KNOWLEDGE-BASELINE.json`
  - `AI-Knowledge/06-UPDATE-LOG/UPDATE-LOG.md`
  - `AI-Knowledge/06-UPDATE-LOG/PENDING-UPDATES.md`
- **Danh sách tài liệu `AI-Knowledge` tạo mới (NEW FILE):**
  - `AI-Knowledge/05-FEATURES/FEAT-P5-01-STAFF-QUICK-LOOKUP.md`
  - `AI-Knowledge/05-FEATURES/FEAT-P5-02-MOBILE-FIRST-UI.md`
  - `AI-Knowledge/06-UPDATE-LOG/UPDATE-REPORT.md` (đồng bộ với `UPDATE-REPORT.md` tại thư mục gốc)
- **Kết quả kiểm tra (Validation):** Hoàn tất đồng bộ 100% các thay đổi từ commit `7c61591` đến `c0e828a`.
