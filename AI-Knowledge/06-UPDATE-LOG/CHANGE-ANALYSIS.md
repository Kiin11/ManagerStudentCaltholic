# BÁO CÁO PHÂN TÍCH THAY ĐỔI MÃ NGUỒN (CHANGE ANALYSIS REPORT)

- **Dự án:** `ManagerStudentCaltholic`
- **Thời gian phân tích:** 2026-11-04 22:05:00 (+07:00)
- **Vai trò:** Senior Software Architect & Codebase Analyst
- **Commit Baseline (Đã phân tích gần nhất):** `7c61591` (*create file gemini.md*)
- **Commit Hiện tại (HEAD):** `c0e828a` (*hotfix*) trên nhánh `main`
- **Tổng số tệp thay đổi:** 19 tệp (+3,026 dòng, -41 dòng)

---

## 1. Phân loại tệp (File Classification)

### 1.1. Added (11 tệp mới tạo)
1. `ManagerStudentCaltholic/Extensions/HttpRequestExtensions.cs` (Layer: Extension)
2. `ManagerStudentCaltholic/Interface/Services/IStaffStudentLookupService.cs` (Layer: Interface/Service)
3. `ManagerStudentCaltholic/Models/DTOs/StaffStudentLookupDtos.cs` (Layer: Model/DTO)
4. `ManagerStudentCaltholic/Models/Entities/LookupViewOption.cs` (Layer: Model/Entity)
5. `ManagerStudentCaltholic/Models/Entities/StaffQuickLookupLog.cs` (Layer: Model/Entity)
6. `ManagerStudentCaltholic/Services/StaffStudentLookupService.cs` (Layer: Service)
7. `ManagerStudentCaltholic/Views/Home/_StaffStudentLookupWidget.cshtml` (Layer: Razor Partial View)
8. `ManagerStudentCaltholic/Views/Shared/_LayoutMobileBottomNav.cshtml` (Layer: Razor Partial View)
9. `ManagerStudentCaltholic/Views/Shared/_LayoutMobileHeader.cshtml` (Layer: Razor Partial View)
10. `ManagerStudentCaltholic/wwwroot/css/mobile-layout.css` (Layer: Static Asset/CSS)
11. `ManagerStudentCaltholic/wwwroot/js/staff-student-lookup.js` (Layer: Static Asset/JavaScript)

### 1.2. Modified (8 tệp bị sửa đổi)
1. `ManagerStudentCaltholic/Controllers/HomeController.cs` (Layer: Controller)
2. `ManagerStudentCaltholic/Data/ParishDbContext.cs` (Layer: Data/ORM)
3. `ManagerStudentCaltholic/Extensions/ServiceCollectionExtensions.cs` (Layer: Extension/DI)
4. `ManagerStudentCaltholic/Models/ViewModels/MasterNavigationViewModels.cs` (Layer: Model/ViewModel)
5. `ManagerStudentCaltholic/Views/Home/Index.cshtml` (Layer: Razor View)
6. `ManagerStudentCaltholic/Views/Shared/_Layout.cshtml` (Layer: Razor Layout)
7. `ManagerStudentCaltholic/Views/_ViewImports.cshtml` (Layer: Razor ViewImports)
8. `ManagerStudentCaltholic/wwwroot/js/pwa-installer.js` (Layer: Static Asset/JavaScript)

### 1.3. Deleted (0 tệp)
*(Không có tệp nào bị xóa trong commit range này)*

### 1.4. Renamed (0 tệp)
*(Không có tệp nào đổi tên hoặc đổi đường dẫn)*

---

## 2. Chi tiết phân tích thay đổi mã nguồn

### 2.1. Class, Method, Property mới
- **`ManagerStudentCaltholic.Models.Entities.LookupViewOption`**:
  - Class Entity đại diện bảng `LookupViewOptions`.
  - Properties: `Id (int, PK)`, `OptionCode (string, Required, Max(50))`, `DisplayName (string, Required, Max(100))`, `Description (string?, Max(255))`, `IconClass (string, Max(50))`, `BadgeColor (string, Max(30))`, `SortOrder (int)`, `IsActive (bool)`.
- **`ManagerStudentCaltholic.Models.Entities.StaffQuickLookupLog`**:
  - Class Entity đại diện bảng `StaffQuickLookupLogs` (Ghi log kiểm toán thao tác tra cứu của GLV).
  - Properties: `Id (long, PK)`, `UserId (long, FK -> Users)`, `User (User?)`, `StudentId (long, FK -> Students)`, `Student (Student?)`, `SearchTerm (string, Max(150))`, `SelectedOption (string, Max(50))`, `IpAddress (string?, Max(50))`, `LookedUpAt (DateTime, Default NOW())`.
- **`ManagerStudentCaltholic.Interface.Services.IStaffStudentLookupService`**:
  - Interface dịch vụ tra cứu nội bộ:
    - `Task<List<StaffStudentQuickSearchResultDto>> SearchStudentsQuickAsync(string keyword, int limit = 8)`
    - `Task<StaffStudent360DetailDto?> GetStudent360DetailAsync(long studentId, string selectedOption = "OVERVIEW")`
    - `Task RecordLookupLogAsync(long userId, long studentId, string searchTerm, string selectedOption, string? ipAddress)`
    - `Task<List<RecentLookupItemDto>> GetRecentLookupsAsync(long userId, int limit = 5)`
    - `Task<List<LookupViewOption>> GetViewOptionsAsync()`
- **`ManagerStudentCaltholic.Services.StaffStudentLookupService`**:
  - Triển khai toàn bộ logic của `IStaffStudentLookupService` trên nền EF Core (`ParishDbContext`), hỗ trợ tìm kiếm đa khóa (Tên, Tên Thánh, Mã hiện tại, Mã năm cũ qua `StudentCodeHistories`), tính toán tỷ lệ chuyên cần Lễ Chúa Nhật/Thứ Năm, chuỗi Streak Chiến dịch Đi Lễ, lịch sử điểm số 2 năm trước và tổng hợp mốc 4 Bí tích.
- **`ManagerStudentCaltholic.Extensions.HttpRequestExtensions`**:
  - Method mở rộng: `bool IsMobileDevice(this HttpRequest request)`.
  - Hỗ trợ nhận diện thiết bị thông qua: Query string `?device=mobile|desktop`, HTTP header Client Hints `Sec-CH-UA-Mobile: ?1`, và Regex matching trên `User-Agent`.
- **`ManagerStudentCaltholic.Models.DTOs`** (13 DTOs mới):
  - `StaffStudentQuickSearchResultDto`
  - `StaffStudent360DetailDto`
  - `StudentBasicInfoDto`
  - `AttendanceOverviewDto`
  - `RecentAttendanceItemDto`
  - `MassCampaignSummaryDto`
  - `AcademicHistoryDto`
  - `CurrentGradeRecordDto`
  - `PastYearHistoryDto`
  - `SacramentHistoryDto`
  - `SacramentMilestoneItemDto`
  - `ParentContactDto`
  - `ParentMessageQuickTemplateDto`
  - `RecentLookupItemDto`

### 2.2. Class, Method, Property bị sửa đổi
- **`ManagerStudentCaltholic.Models.ViewModels.MasterWorkspaceViewModel`**:
  - Thêm 2 properties mới:
    - `public List<DTOs.RecentLookupItemDto> RecentStaffLookups { get; set; } = new();`
    - `public List<LookupViewOption> LookupOptions { get; set; } = new();`
- **`ManagerStudentCaltholic.Controllers.HomeController`**:
  - Constructor: Bổ sung 2 Dependency Injection parameters: `IStaffStudentLookupService lookupService`, `ICacheService cacheService`.
  - Action `Index`: Bổ sung nạp dữ liệu `RecentStaffLookups` và `LookupOptions` khi người dùng đã đăng nhập (`User.Identity.IsAuthenticated`).

### 2.3. Thay đổi Controller, Route, Action
Trong `HomeController.cs`:
1. **`GET /Home/StaffSearchSuggestions?term={term}`**:
   - Action: `Task<IActionResult> StaffSearchSuggestions(string? term)`
   - Xác thực: Yêu cầu đăng nhập (`User.Identity.IsAuthenticated`).
   - Chức năng: Trả về danh sách JSON gợi ý tìm kiếm tức thời theo họ tên, tên thánh, mã số hiện hành, mã năm cũ (tối đa 8 kết quả).
2. **`GET /Home/StaffStudentLookup?studentId={id}&option={opt}&searchTerm={term}`**:
   - Action: `Task<IActionResult> StaffStudentLookup(long studentId, string? option = "OVERVIEW", string? searchTerm = null)`
   - Xác thực: Yêu cầu đăng nhập.
   - Chức năng: Trả về chi tiết 360 độ của học sinh. Tích hợp bộ đệm Redis Cache TTL 5 phút (`staff_lookup:student_360:{studentId}`). Tự động ghi nhật ký vào bảng `StaffQuickLookupLogs`.
3. **`GET /Home/StaffRecentLookups`**:
   - Action: `Task<IActionResult> StaffRecentLookups()`
   - Xác thực: Yêu cầu đăng nhập.
   - Chức năng: Trả về danh sách 6 học sinh vừa tra cứu gần đây của GLV hiện tại.
4. **`GET /Home/StaffLookupOptions`**:
   - Action: `Task<IActionResult> StaffLookupOptions()`
   - Xác thực: Công khai/Nội bộ.
   - Chức năng: Trả về danh mục các chế độ xem khả dụng.

### 2.4. Thay đổi Database Schema & ORM Mapping (`ParishDbContext.cs`)
1. **DbSet mới**:
   - `public DbSet<StaffQuickLookupLog> StaffQuickLookupLogs => Set<StaffQuickLookupLog>();`
   - `public DbSet<LookupViewOption> LookupViewOptions => Set<LookupViewOption>();`
2. **Indexes mới bổ sung**:
   - `Students`: Index trên `StudentCode`, Index tổng hợp `{ LastName, FirstName }`, Index trên `ChristianName`.
   - `Enrollments`: Index tổng hợp `{ StudentId, AcademicYearId }`.
   - `StudentCodeHistories`: Bổ sung Index trên `NewCode`.
   - `StaffQuickLookupLogs`: Index tổng hợp `{ UserId, LookedUpAt }`, Index trên `StudentId`.
   - `LookupViewOptions`: Index Unique trên `OptionCode`.
3. **Seed Data mới**:
   - Nạp sẵn 4 bản ghi chế độ xem mặc định vào bảng `LookupViewOptions`:
     - `OVERVIEW` (Tổng quan)
     - `ATTENDANCE_DETAIL` (Chuyên cần & Chiến dịch)
     - `ACADEMIC_2Y_SACRAMENT` (Lịch sử 2 năm & Bí tích)
     - `PARENT_CONTACT` (Liên hệ phụ huynh)

### 2.5. Thay đổi Razor Views & UI Layout
1. **`_ViewImports.cshtml`**:
   - Thêm `@using ManagerStudentCaltholic.Extensions;` để toàn bộ Views sử dụng được `Context.Request.IsMobileDevice()`.
2. **`_Layout.cshtml`**:
   - Bổ sung logic rẽ nhánh thiết bị: `bool isMobile = Context.Request.IsMobileDevice();`.
   - Khung giao diện `@if (isMobile)`: Nhúng Header thu gọn `_LayoutMobileHeader.cshtml`, vùng nội dung `mobile-main-wrapper`, và thanh điều hướng đáy `_LayoutMobileBottomNav.cshtml`.
   - Khung giao diện `else` (Desktop): Giữ nguyên 100% cấu trúc `#app-sidebar`, `#main-wrapper`, và `.topbar-navbar`.
   - Tối ưu hiệu năng: Thêm `viewport-fit=cover` cho mobile; chỉ tải Google Fonts trên Desktop; bọc cụm nút cuộn `.scroll-floating-controls` bằng `@if (!isMobile)` để tránh che giao diện mobile; tối ưu listener cuộn bằng `requestAnimationFrame` + `{ passive: true }`; loại bỏ đăng ký trùng lặp Service Worker trong layout.
3. **`_LayoutMobileHeader.cshtml` (New)**:
   - Header thu gọn $\approx 56\text{px}$, nút bấm đạt kích thước chạm tối thiểu $44\times 44\text{px}$.
   - Tích hợp Offcanvas drawer chứa lối tắt hệ thống và chân trang cố định chứa nút Đăng Xuất (`z-index: 1075`).
4. **`_LayoutMobileBottomNav.cshtml` (New)**:
   - Thanh điều hướng cố định phía dưới màn hình với 5 phân khu chính: Chung, Điểm Danh (nổi bật với nút tròn quét QR 1-chạm), Học Vụ, Bí Tích, Quản Trị / Cá Nhân (`z-index: 1035`).
5. **`_StaffStudentLookupWidget.cshtml` (New)**:
   - Widget tra cứu nội bộ tại `HomePage` cho GLV: Thanh tìm kiếm thông minh, dropdown gợi ý realtime, danh sách tra cứu gần đây, bảng kết quả 360 độ gồm 4 tab chuyển đổi Option, nút gọi/nhắn tin Zalo/SMS nhanh cho phụ huynh.
6. **`Views/Home/Index.cshtml`**:
   - Nhúng widget tra cứu: `@if (User.Identity?.IsAuthenticated == true) { @await Html.PartialAsync("_StaffStudentLookupWidget", Model) }`.

### 2.6. Thay đổi Assets & Scripts
1. **`wwwroot/css/mobile-layout.css` (New)**:
   - Khai báo biến Safe Area (`env(safe-area-inset-*)`).
   - Định nghĩa quy chuẩn `.touch-target-44`, layout cho `.mobile-topbar`, `.mobile-bottom-nav`, `.mobile-main-wrapper`, `.mobile-bottom-sheet`.
   - Tối ưu 60fps scrolling (loại bỏ `backdrop-filter: blur()`, áp dụng `contain: layout paint`).
2. **`wwwroot/js/staff-student-lookup.js` (New)**:
   - Bộ điều khiển client-side: Debounce input (300ms), gọi API `/Home/StaffSearchSuggestions`, nạp và render động dữ liệu 360 qua `/Home/StaffStudentLookup`, xử lý copy clipboard mẫu tin nhắn phụ huynh.
3. **`wwwroot/js/pwa-installer.js`**:
   - Ngăn chặn vòng lặp tự động reload trang khi Service Worker cập nhật hoặc cài đặt lần đầu (`controllerchange` chỉ kích hoạt reload khi cờ `window.__pwaUserRequestedUpdate = true`).

---

## 3. Tổng kết theo Phân hệ & Feature

| Mã Feature | Tên Feature | File chính can thiệp | Loại thay đổi |
|---|---|---|---|
| **FEAT-P5-01** | Cổng tra cứu thiếu nhi nội bộ tại HomePage (Đã đăng nhập) | `HomeController.cs`, `IStaffStudentLookupService.cs`, `StaffStudentLookupService.cs`, `ParishDbContext.cs`, `LookupViewOption.cs`, `StaffQuickLookupLog.cs`, `StaffStudentLookupDtos.cs`, `_StaffStudentLookupWidget.cshtml`, `staff-student-lookup.js` | Tạo mới hoàn chỉnh Service, Entity, DTO, Widget và API |
| **FEAT-P5-02 / TASK-P5-02.1** | Giao diện Mobile Web (Mobile-First): Header, Bottom Nav, Layout Switch | `HttpRequestExtensions.cs`, `_Layout.cshtml`, `_LayoutMobileHeader.cshtml`, `_LayoutMobileBottomNav.cshtml`, `_ViewImports.cshtml`, `mobile-layout.css`, `pwa-installer.js` | Tạo mới khung giao diện di động, tách luồng `@if (isMobile)`, tối ưu hiệu năng |
