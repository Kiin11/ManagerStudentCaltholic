# AI-KNOWLEDGE UPDATE LOG

## Lịch sử Cập nhật (Changelog)

### Phiên cập nhật: `FEAT-P5-04`
- **Thời gian cập nhật:** `2026-11-05T09:48:00+07:00`
- **Mã tính năng:** `FEAT-P5-04` (Điểm danh & QR - Nâng cấp giao diện Quét QR Điểm danh)
- **Sub-tasks:**
  - `TASK-P5-04.1`: Thiết kế lại Khung ngắm Camera (Viewfinder HUD) & Điều khiển Phần cứng (Flash Torch, Flip, Zoom slider, rung & âm báo).
  - `TASK-P5-04.2`: Thanh Chuyển đổi Nhanh Buổi Quét (Session Quick-Switcher) & Cảnh báo Thẻ Mã Năm Cũ (`StudentCodeHistories`).
  - `TASK-P5-04.3`: Khay Lịch sử Vừa Quét (Recent Scans Drawer) & Hoàn tác (Undo) 5 Giây (`/Attendance/UndoScanCheckIn` & `ParishOfflineEngine.undoOfflineQrScan`).
  - `TASK-P5-04.4`: Tối ưu Quét Liên tục (Continuous Scan, chống trùng 3s) & Đồng bộ Hàng đợi Offline.
- **Danh sách file source cập nhật:**
  - `ManagerStudentCaltholic/Views/Attendance/ScanQr.cshtml`
  - `ManagerStudentCaltholic/Controllers/AttendanceController.cs`
  - `ManagerStudentCaltholic/Services/RedisAttendanceService.cs`
  - `ManagerStudentCaltholic/Interface/Services/IRedisAttendanceService.cs`
  - `ManagerStudentCaltholic/Models/ViewModels/AttendanceViewModels.cs`
  - `ManagerStudentCaltholic/wwwroot/js/offline-attendance.js`
- **Danh sách tài liệu `AI-Knowledge` tạo mới:**
  - `AI-Knowledge/05-FEATURES/FEAT-P5-04-QR-SCANNER-UPGRADE.md`

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

---

### Cập nhật Ngày 05/11/2026 - FEAT-P5-07: Quản Lý Kế Hoạch Lớp Theo Version Ngày Sửa
- **Phân hệ:** `Học vụ & Giáo án` (`LessonPlans`)
- **Mã tính năng:** `FEAT-P5-07` (Sub-tasks: `TASK-P5-07.1`, `TASK-P5-07.2`, `TASK-P5-07.3`, `TASK-P5-07.4`)
- **Nội dung thực hiện:**
  1. **TASK-P5-07.1 (Database & Sync):**
     - Tạo Entity `ClassCurriculumDocument` (kế hoạch hiện hành, snapshot JSON `ContentJson`, thông tin file version mới nhất).
     - Tạo Entity `ClassCurriculumVersion` (lịch sử phiên bản ngày sửa, bản chụp tuần học, đường dẫn file `.xlsx` tự động sinh).
     - Cập nhật `ClassLessonPlan`: Bổ sung `CurriculumDocumentId`, `WeekNumber`, `Semester` và liên kết với `ClassCurriculumDocument`.
     - Cấu hình Fluent API và tạo EF Migration `AddClassCurriculumDocumentsAndVersions`.
  2. **TASK-P5-07.2 (Parser đa định dạng):**
     - Xây dựng dịch vụ đọc file đa định dạng trong `ClassCurriculumService`: hỗ trợ `.xlsx` (ClosedXML), `.docx` (OpenXML), `.csv` (StreamReader với auto delimiter detection), `.md` (Markdown Table & List), và `.pdf` (PdfPig).
     - Tự động chuẩn hóa số tuần, phát hiện ngày thi/nghỉ lễ, trả về DTOs nạp trực tiếp vào giao diện web mà không cần reload trang.
  3. **TASK-P5-07.3 (Biên tập trực tuyến & Tự động sinh file version):**
     - Trình biên tập bảng tuần tương tác: sửa tuần, ngày học, chủ đề, GLV, công tắc đánh dấu thi/nghỉ lễ, tính chuỗi Chúa Nhật tự động.
     - Tự động sinh file Excel `.xlsx` chuẩn mẫu Giáo xứ gắn hậu tố version và ngày sửa (`KeHoach_{Class}_v{Version}_{YYYYMMDD}.xlsx`), lưu an toàn vào `PrivateUploads/curriculums`.
     - Lưu phiên bản vào `ClassCurriculumVersions` và đồng bộ nguyên tử sang `ClassLessonPlans` qua `IExecutionStrategy`.
  4. **TASK-P5-07.4 (Giao diện tích hợp, Xem lịch sử & Rollback):**
     - Cập nhật `LessonPlansController` và View `Views/LessonPlans/Index.cshtml` với giao diện 3 Tab trực quan.
     - Hỗ trợ xem lại snapshot từng phiên bản cũ, tải file `.xlsx` của ngày sửa bất kỳ, và chức năng Khôi phục (Rollback) an toàn.
