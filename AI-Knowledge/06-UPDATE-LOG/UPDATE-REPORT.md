# BÁO CÁO CẬP NHẬT TÀI LIỆU TRI THỨC HỆ THỐNG (UPDATE-REPORT)

> **Dự án:** `ManagerStudentCaltholic`  
> **Phạm vi cập nhật:** Commit `7c61591` ➔ `c0e828a` (Nhánh `main`)  
> **Thời gian thực hiện:** `2026-11-04T22:14:00+07:00`  
> **Vai trò thực hiện:** Senior Software Architect

---

## 1. Danh sách File Knowledge Đã Cập nhật (Modified)

Chỉ những tài liệu thực sự bị ảnh hưởng bởi 19 file mã nguồn thay đổi mới được cập nhật (các tài liệu không bị ảnh hưởng như `CODING-RULES.md`, `ATTENDANCE.md`, `REDIS-CACHE-SPEC.md`, `OFFLINE-SYNC-SPEC.md` được giữ nguyên 100%):

| STT | Đường dẫn File Knowledge | Nội dung cập nhật chính |
|---|---|---|
| 1 | `AI-Knowledge/00-OVERVIEW/PROJECT.md` | Bổ sung tính năng Cổng tra cứu 360 độ thiếu nhi nội bộ (`FEAT-P5-01`), Giao diện Mobile-First Web UI (`FEAT-P5-02`), và vai trò cache Redis cho tra cứu nội bộ. |
| 2 | `AI-Knowledge/00-OVERVIEW/ARCHITECTURE.md` | Bổ sung `StaffStudentLookupService.cs` vào tầng Service; thêm **Flow 4** (Luồng tra cứu 360 độ thiếu nhi nội bộ & Redis cache 5 phút) và **Flow 5** (Luồng chuyển đổi giao diện Mobile-First / Desktop qua `HttpRequestExtensions.IsMobileDevice()`). |
| 3 | `AI-Knowledge/00-OVERVIEW/TECH-STACK.md` | Bổ sung cơ chế Server-side Device Detection (`HttpRequestExtensions.cs`), Mobile-First CSS (`mobile-layout.css`, Safe Area Insets, 44x44px touch targets) và kiểm soát vòng đời PWA (`pwa-installer.js`). |
| 4 | `AI-Knowledge/02-DATABASE/DATABASE-OVERVIEW.md` | Bổ sung ánh xạ ORM cho 2 Entity mới: `StaffQuickLookupLog` (`StaffQuickLookupLogs`) và `LookupViewOption` (`LookupViewOptions`). |
| 5 | `AI-Knowledge/02-DATABASE/TABLE-INDEX.json` | Bổ sung định nghĩa bảng `StaffQuickLookupLogs` và `LookupViewOptions`; cập nhật danh sách index của bảng `Students` (`LastName, FirstName`, `ChristianName`) và `Enrollments` (`StudentId, AcademicYearId`). |
| 6 | `AI-Knowledge/02-DATABASE/TABLE-RELATIONSHIPS.md` | Bổ sung mục **6. Internal Staff Quick Lookup** (`StaffQuickLookupLogs ↔ Users`, `StaffQuickLookupLogs ↔ Students`, `LookupViewOptions`), cập nhật sơ đồ `erDiagram` Mermaid và khối JSON index đi kèm. |
| 7 | `AI-Knowledge/03-CODE-INDEX/FILE-INDEX.jsonl` | Bổ sung 11 dòng định nghĩa cho 11 file mã nguồn mới (`HttpRequestExtensions.cs`, `IStaffStudentLookupService.cs`, `StaffStudentLookupDtos.cs`, `LookupViewOption.cs`, `StaffQuickLookupLog.cs`, `StaffStudentLookupService.cs`, 3 Razor Partial Views, `mobile-layout.css`, `staff-student-lookup.js`). |
| 8 | `AI-Knowledge/03-CODE-INDEX/SYMBOL-INDEX.jsonl` | Bổ sung 18 dòng symbol mới gồm `HttpRequestExtensions`, `IsMobileDevice`, `IStaffStudentLookupService`, `StaffStudentLookupService` (cùng 5 methods), `LookupViewOption`, `StaffQuickLookupLog`, các DTO tra cứu, và `HomeController` (cùng 4 action methods mới). |
| 9 | `AI-Knowledge/03-CODE-INDEX/ROUTE-INDEX.jsonl` | Bổ sung 5 route của `HomeController`: `/Home/Index`, `/Home/StaffSearchSuggestions`, `/Home/StaffStudentLookup`, `/Home/StaffRecentLookups`, `/Home/StaffLookupOptions`. |
| 10 | `AI-Knowledge/03-CODE-INDEX/DEPENDENCY-INDEX.jsonl` | Bổ sung 9 quan hệ phụ thuộc (`IMPLEMENTS`, `INJECTS`, `CALLS`) đã xác minh từ cú pháp C# cho `StaffStudentLookupService` và `HomeController`. |
| 11 | `AI-Knowledge/06-UPDATE-LOG/KNOWLEDGE-BASELINE.json` | Nâng `lastIndexedCommit` từ `7c61591` lên `c0e828a`, cập nhật `lastUpdatedAt` và trạng thái `completed`. |
| 12 | `AI-Knowledge/06-UPDATE-LOG/UPDATE-LOG.md` | Ghi nhận chi tiết biên bản cập nhật phiên `7c61591` ➔ `c0e828a`. |
| 13 | `AI-Knowledge/06-UPDATE-LOG/PENDING-UPDATES.md` | Cập nhật trạng thái hoàn tất đồng bộ đến `c0e828a` và liệt kê các hạng mục chờ ở sprint tiếp theo. |

---

## 2. Danh sách File Knowledge Tạo Mới (`NEW FILE`)

| STT | Đường dẫn File Mới | Mục đích & Nội dung |
|---|---|---|
| 1 | `AI-Knowledge/05-FEATURES/FEAT-P5-01-STAFF-QUICK-LOOKUP.md` | Đặc tả chi tiết tính năng Cổng tra cứu 360 độ thiếu nhi nội bộ tại Trang chủ: kiến trúc file, luồng autocomplete đa khóa (Họ tên, Tên Thánh, Mã hiện hành, Mã 2 năm cũ), tổng hợp 360 độ có cache Redis 5 phút (`staff_lookup:student_360:{studentId}`), ghi log `StaffQuickLookupLogs` và bảng 4 chế độ xem `LookupViewOptions`. |
| 2 | `AI-Knowledge/05-FEATURES/FEAT-P5-02-MOBILE-FIRST-UI.md` | Đặc tả chi tiết kiến trúc Giao diện Mobile-First (`TASK-P5-02.1`): cơ chế nhận diện `IsMobileDevice()`, phân nhánh `@if (isMobile)` trong `_Layout.cshtml`, `_LayoutMobileHeader.cshtml` (Offcanvas cuộn mượt, ghim nút Đăng xuất), `_LayoutMobileBottomNav.cshtml` (5 phân khu + nút Quét QR nổi), Safe Area Inset và tối ưu hiệu năng cuộn/PWA. |
| 3 | `UPDATE-REPORT.md` & `AI-Knowledge/06-UPDATE-LOG/UPDATE-REPORT.md` | Báo cáo tổng kết đợt cập nhật tài liệu `AI-Knowledge`. |

---

## 3. Danh sách File Knowledge Đã Xóa (`DELETE FILE`)

- **Không có (`0` file bị xóa).** Không có class, controller, hay feature nào bị gỡ bỏ khỏi mã nguồn trong khoảng commit `7c61591..c0e828a`.

---

## 4. Các Module và Feature Bị Ảnh Hưởng

| Module / Feature | Mức độ ảnh hưởng | Tóm tắt thay đổi |
|---|---|---|
| **Module `Common` & `StaffLookup` (`FEAT-P5-01`)** | **MEDIUM - HIGH** | Thêm mới hoàn toàn dịch vụ `StaffStudentLookupService`, 2 bảng `StaffQuickLookupLogs` và `LookupViewOptions`, 4 API endpoints trên `HomeController`, và widget tra cứu tức thời tại Trang chủ. |
| **Module `Common` / Layout & UI (`FEAT-P5-02` / `TASK-P5-02.1`)** | **HIGH** | Thay đổi `Views/Shared/_Layout.cshtml` (layout gốc của toàn hệ thống) để tự động rẽ nhánh giao diện Mobile-First (`_LayoutMobileHeader.cshtml`, `_LayoutMobileBottomNav.cshtml`, `mobile-layout.css`) khi truy cập từ thiết bị di động, giữ nguyên giao diện Desktop. |
| **Module `Database` (`ParishDbContext`)** | **HIGH** | Thêm 2 `DbSet`, 7 cấu hình Index mới (trên `Students`, `Enrollments`, `StudentCodeHistories`, `StaffQuickLookupLogs`, `LookupViewOptions`), và Seed Data 4 dòng cho `LookupViewOptions`. |
| **Module `Student`, `Enrollment`, `Attendance`, `MassCampaigns`, `Grades`, `Promotion`, `Sacraments`, `Notification`, `PublicRegistration`** | **LOW (Read-Only)** | Được `StaffStudentLookupService` truy vấn chỉ đọc (`.AsNoTracking()`) để tổng hợp hồ sơ 360 độ. Không thay đổi logic ghi hay hợp đồng dữ liệu của các module này. |

---

## 5. Các Dependency Thay Đổi (Đã Xác Minh Từ Source Code)

1. **Đăng ký Dependency Injection mới (`Extensions/ServiceCollectionExtensions.cs`):**
   - `services.AddScoped<IStaffStudentLookupService, StaffStudentLookupService>();`
2. **Quan hệ `IMPLEMENTS` & `INJECTS` mới:**
   - `StaffStudentLookupService` implements `IStaffStudentLookupService`.
   - `StaffStudentLookupService` injects `ParishDbContext` và `ILogger<StaffStudentLookupService>`.
   - `HomeController` injects thêm `IStaffStudentLookupService` (`_lookupService`) và `ICacheService` (`_cacheService`).
3. **Quan hệ `CALLS` & `USES` mới ở tầng View/Layout:**
   - `Views/_ViewImports.cshtml` thêm `@using ManagerStudentCaltholic.Extensions`.
   - `Views/Shared/_Layout.cshtml` gọi `Context.Request.IsMobileDevice()` và nạp `_LayoutMobileHeader`, `_LayoutMobileBottomNav`.
   - `Views/Home/Index.cshtml` nạp partial `_StaffStudentLookupWidget` và script `~/js/staff-student-lookup.js`.

---

## 6. Những Điểm Chưa Xác Minh (`NEEDS_VERIFICATION`)

1. **Trạng thái thực thi Schema `StaffQuickLookupLogs` & `LookupViewOptions` trên Database thực tế (`NEEDS_VERIFICATION`):**
   - *Bằng chứng:* Trong commit `201db10`, file migration `20260410164552_AddStaffQuickLookupTablesAndIndexes.cs` và script `FEAT-P5-01_StaffQuickLookup_Setup_And_Data.sql` được thêm vào, nhưng đến commit `0f5331a` đã bị xóa khỏi Git repository trong khi `ParishDbContext.cs` vẫn giữ nguyên khai báo `DbSet`, `HasIndex`, và `HasData`.
   - *Hệ quả:* Chưa thể xác minh từ Git history xem môi trường Database hiện tại đã được chạy script SQL tạo 2 bảng này hay chưa.
2. **Bảng `UserHomepageWidgetConfigs` trong đặc tả `FEAT-P5-02` (`NEEDS_VERIFICATION`):**
   - *Bằng chứng:* Mô tả ban đầu của `FEAT-P5-02` có nhắc tới `UserHomepageWidgetConfigs`, nhưng trong source code đến commit `c0e828a` chưa có Entity hoặc `DbSet` này.
3. **Các Sub-task tiếp theo của `FEAT-P5-02` (`TASK-P5-02.2`, `TASK-P5-02.3`, `TASK-P5-02.4`) (`NEEDS_VERIFICATION`):**
   - *Bằng chứng:* Source code đến commit `c0e828a` mới hoàn tất `TASK-P5-02.1` (Khung Mobile-First, Header thu gọn, Bottom Nav 5 phân khu, Safe Area, tối ưu hiệu năng). Các View bảng dữ liệu cụ thể (Sổ điểm danh, Sổ điểm, Danh sách thiếu nhi) chưa có commit thay đổi trong dải `7c61591..c0e828a`.

---

## 7. Những Nội Dung Cần Developer Kiểm Tra Thủ Công

1. **Kiểm tra Database Schema & Seed Data cho `FEAT-P5-01`:**
   - Đảm bảo 2 bảng `"StaffQuickLookupLogs"` và `"LookupViewOptions"` cùng 4 dòng dữ liệu cấu hình (`OVERVIEW`, `ATTENDANCE_DETAIL`, `ACADEMIC_2Y_SACRAMENT`, `PARENT_CONTACT`) và các Index mới trên `"Students"`, `"Enrollments"`, `"StudentCodeHistories"` đã tồn tại trong PostgreSQL trước khi sử dụng Cổng tra cứu nội bộ tại `/Home/Index`.
   - Nếu triển khai sang môi trường mới bằng `dotnet ef database update`, cần tạo lại EF Core Migration cho `ParishDbContext` để tránh lỗi `42P01: relation "StaffQuickLookupLogs" does not exist`.
2. **Kiểm tra Trải nghiệm Giao diện Mobile (`FEAT-P5-02`):**
   - Kiểm tra thực tế trên trình duyệt di động (iOS Safari / Android Chrome) hoặc qua tham số `/?device=mobile`:
     - Thanh điều hướng đáy 5 phân khu hiển thị đúng và không che khuất nội dung cuối trang.
     - Menu Offcanvas bên phải cuộn mượt và nút **Đăng xuất** luôn hiển thị cố định ở đáy menu đối với tài khoản Quản trị viên.
3. **Kiểm tra Đồng bộ Cache Redis khi Thay đổi Điểm danh / Điểm số:**
   - Hồ sơ tra cứu 360 độ tại `/Home/StaffStudentLookup` được cache trên Redis với khóa `staff_lookup:student_360:{studentId}` trong **5 phút**. Nếu GLV vừa cập nhật điểm danh hoặc điểm số và tra cứu lại ngay lập tức trên Trang chủ, dữ liệu mới sẽ phản ánh sau tối đa 5 phút (hoặc developer có thể bổ sung lệnh xóa key `staff_lookup:student_360:{studentId}` khi lưu điểm danh/điểm số nếu cần tức thời 100%).
