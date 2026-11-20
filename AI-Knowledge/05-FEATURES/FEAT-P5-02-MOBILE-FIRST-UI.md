# FEATURE SPECIFICATION: GIAO DIỆN MOBILE WEB (MOBILE-FIRST UI - `FEAT-P5-02`)

> **Trạng thái:** NEW FILE (Bổ sung từ commit `c0e828a` — Hoàn thành `TASK-P5-02.1`)  
> **Module:** `Common` (Layout, Navigation & Responsive UI)  
> **Phạm vi ảnh hưởng:** Toàn bộ các trang sử dụng `Views/Shared/_Layout.cshtml` khi truy cập từ thiết bị di động

---

## 1. Mục đích Nghiệp vụ & Trải nghiệm Người dùng
Thiết kế trải nghiệm chuyên biệt cho trình duyệt di động và PWA (`Mobile-First`) mà **không làm thay đổi giao diện Desktop hiện hữu**:
- Phân tách nhánh render ngay tại `Views/Shared/_Layout.cshtml` thông qua `@if (isMobile)`.
- Cung cấp **Thanh điều hướng đáy cố định (Bottom Navigation Bar)** gồm 5 phân khu chính với nút trung tâm **Quét QR** nổi bật để thao tác một tay.
- Tối ưu **Header thu gọn (Compact Header)**, vùng an toàn **Safe Area Inset** trên iOS/Android, kích thước vùng chạm tối thiểu `44x44px`, và hiệu năng cuộn mượt trên thiết bị di động.

---

## 2. Thành phần Source Code Tham gia

| Tầng | File Path | Symbol / Thành phần | Vai trò |
|---|---|---|---|
| **Extension** | `Extensions/HttpRequestExtensions.cs` | `HttpRequestExtensions.IsMobileDevice(this HttpRequest)` | Nhận diện thiết bị Mobile từ HTTP Request |
| **Layout** | `Views/Shared/_Layout.cshtml` | `@if (isMobile)` / `else` | Nhánh chuyển đổi giao diện Mobile-First vs Desktop |
| **Partial View** | `Views/Shared/_LayoutMobileHeader.cshtml` | Mobile Compact Header & Offcanvas | Header thu gọn (54px) và ngăn kéo menu phải tối ưu cuộn |
| **Partial View** | `Views/Shared/_LayoutMobileBottomNav.cshtml` | Mobile Bottom Navigation Bar | Thanh điều hướng đáy 5 phân khu & dải Sub-module Pills cuộn ngang |
| **Stylesheet** | `wwwroot/css/mobile-layout.css` | Mobile-First CSS Rules | Safe Area Insets, 44x44px touch target, GPU scroll optimization, hệ thống thẻ/lưới dữ liệu Mobile (`.mobile-card-list`, `.mobile-data-card`, `.mobile-data-grid-*`) |
| **Data Views** | `Views/Dashboard/Index.cshtml`, `Views/Sacraments/Index.cshtml`, `Views/AcademicYears/Index.cshtml`, `Views/Grades/Index.cshtml`, `Views/Promotion/Index.cshtml`, `Views/Governance/Index.cshtml`, `Views/Students/Index.cshtml` | Nhánh `@if (isMobile)` | Hiển thị danh sách nhiều cột dạng thẻ/lưới chuyên biệt trên Mobile thay cho bảng rộng, giữ nguyên bảng Desktop tại nhánh `else` |
| **PWA Script** | `wwwroot/js/pwa-installer.js` | `window.__pwaUserRequestedUpdate` | Ngăn tự động reload trang ngoài ý muốn khi Service Worker kiểm tra cập nhật |

---

## 3. Cơ chế Nhận diện Thiết bị (`HttpRequestExtensions.IsMobileDevice`)

Phương thức mở rộng `Context.Request.IsMobileDevice()` trong `Extensions/HttpRequestExtensions.cs` đánh giá theo thứ tự ưu tiên:
1. **Query String Override (`?device=mobile` hoặc `?device=desktop`):** Cho phép kiểm thử nhanh chế độ giao diện qua URL.
2. **Client Hints Header (`Sec-CH-UA-Mobile`):** Nếu trình duyệt gửi header `Sec-CH-UA-Mobile: ?1`, trả về `true` ngay lập tức (O(1)).
3. **Compiled User-Agent Regex (`MobileRegex` & `MobilePrefixRegex`):** Kiểm tra chuỗi `User-Agent` cho các dòng thiết bị Android, iPhone, iPod, Windows Phone, BlackBerry, Opera Mini, IEMobile, Mobile Safari.

---

## 4. Kiến trúc Khung Giao diện Mobile (`TASK-P5-02.1`)

### 4.1. Header Thu gọn & Offcanvas Menu (`_LayoutMobileHeader.cshtml`)
- **Chiều cao cố định:** `56px` (`--mobile-header-height: 56px`), cộng thêm `env(safe-area-inset-top)` cho màn hình tai thỏ / Dynamic Island.
- **Thành phần Header:** Logo Giáo xứ Phú Trung + Tên phân khu đang mở, nút tắt Quét QR (dành cho nhân sự nội bộ), và nút mở Offcanvas Menu (`44x44px`).
- **Offcanvas Menu (`#mobileMenuOffcanvas`):**
  - Đặt `z-index: 1070` (với `.offcanvas-footer` `z-index: 1075` và `.offcanvas-backdrop` `z-index: 1065`) để không bị che khuất.
  - Danh sách lối tắt bên trong sử dụng `flex-grow-1 overflow-y-auto` (`-webkit-overflow-scrolling: touch`) giúp kéo mượt trên màn hình nhỏ.
  - Nút **Đăng xuất** (hoặc **Đăng nhập**) được ghim cố định ở đáy Offcanvas (`flex-shrink-0`), đảm bảo luôn nhìn thấy và bấm được ngay cả với tài khoản Admin có nhiều menu.

### 4.2. Thanh Điều hướng Đáy 5 Phân khu (`_LayoutMobileBottomNav.cshtml`)
- **Dải Sub-module Pills (`.mobile-subnav-strip`):** Nằm phía trên nội dung chính, cho phép vuốt ngang chọn nhanh các chức năng con thuộc phân khu hiện tại.
- **Bottom Navigation Bar — YouTube Floating Pill Dock (`.mobile-bottom-nav.yt-floating-nav`):**
  - Thiết kế nổi dạng viên thuốc bo tròn (`position: fixed; bottom: max(12px, calc(env(safe-area-inset-bottom) + 8px)); border-radius: 9999px; background: rgba(18, 18, 18, 0.94)`).
  - Trạng thái tab đang chọn hiển thị khung viên thuốc xám bo tròn (`.yt-nav-pill` với `background: rgba(255, 255, 255, 0.2)`) giống giao diện YouTube Mobile.
  - 5 nút điều hướng chính (biểu tượng tối giản chuẩn vùng chạm `44x44px`):
    1. **Chung** (`COMMON` - `/Home/Index?master=COMMON`) — Biểu tượng ngôi nhà.
    2. **Học Vụ** (`ACADEMIC` - `/Home/Index?master=ACADEMIC`) — Biểu tượng mũ tốt nghiệp.
    3. **Nút trung tâm vòng tròn (`.yt-center-circle`):**
       - Nếu là nhân sự nội bộ (`isStaff`): **Quét QR** (`/Attendance/ScanQr`).
       - Nếu là phụ huynh/khách: **Điểm Danh** (`/Home/Index?master=ATTENDANCE`).
    4. **Bí Tích** (`SACRAMENT` - `/Home/Index?master=SACRAMENT`) — Biểu tượng huy chương.
    5. **Quản Trị / Cá Nhân (`.yt-avatar-badge`):** Huy hiệu Avatar tròn màu xanh cổ vịt (`#00897b`) hiển thị chữ cái đầu tên người dùng (hoặc biểu tượng khách), điều hướng tới `GOVERNANCE`, `Account/Profile`, hoặc `Lookup/Index`.

### 4.3. Tối ưu Hiệu năng Di động (Performance Optimizations)
1. **Loại bỏ `backdrop-filter: blur(...)` trên Mobile:** Tránh hiện tượng giật khung hình (scroll jank) trên chip đồ họa di động khi cuộn qua các phần tử cố định.
2. **Chỉ nạp script lắng nghe cuộn (`scrollToTopBtn`) trên nhánh Desktop (`!isMobile`):** Tránh gắn sự kiện `window.scroll` và ẩn nút cuộn nổi trên màn hình điện thoại để không đè lên tab thứ 5 của Bottom Navigation Bar.
3. **Kiểm soát vòng đời PWA Service Worker (`pwa-installer.js`):** Chỉ gọi `reg.update()` trên trang chủ thay vì mọi lần chuyển trang, và chỉ tự động `window.location.reload()` khi người dùng chủ động nhấn nút cập nhật (`window.__pwaUserRequestedUpdate = true`).

### 4.4. Hệ thống Thẻ Dữ liệu & Lưới Thông tin trên Mobile (`.mobile-card-list`, `.mobile-data-card`)
- **Chống bẻ chữ dọc trong bảng và badge (`mobile-layout.css`):**
  - Không áp dụng `word-break: break-word` lên `.card .badge`; thay vào đó bảo vệ `.table .badge`, `.table .btn`, `.mobile-data-card .badge` với `white-space: nowrap !important; word-break: normal !important; overflow-wrap: normal !important;` và giữ `white-space: nowrap` cho các ô bảng còn lại trong `.table-responsive`.
- **Các lớp khung thẻ dữ liệu (`.mobile-card-list`, `.mobile-data-card`, `.mobile-data-grid-*`):**
  - `.mobile-card-list`: Container flex dọc (`gap: 12px`, `padding: 12px`, nền `#f8fafc`).
  - `.mobile-data-card`: Thẻ trắng bo góc (`border: 1.5px solid #cbd5e1; border-radius: 14px; padding: 12px 14px;`) gồm `.mobile-data-card-header` và `.mobile-data-card-title`.
  - `.mobile-data-grid-2`, `.mobile-data-grid-3`, `.mobile-data-grid-4`: Lưới CSS Grid `repeat(2|3|4, minmax(0, 1fr))` chứa các ô chỉ số `.mobile-metric-box` (`.mobile-metric-label`, `.mobile-metric-value`).
- **Phạm vi áp dụng nhánh `@if (isMobile)` (giữ nguyên bảng Desktop ở nhánh `else`):**
  - `Views/Dashboard/Index.cshtml`: Danh sách thiếu nhi cần quan tâm mục vụ & Xếp hạng chuyên cần các lớp.
  - `Views/Sacraments/Index.cshtml`: Sổ bộ Bí tích điện tử (lưới 2x2 hiển thị 4 mốc Bí tích).
  - `Views/AcademicYears/Index.cshtml`: Danh sách các năm học & nút kích hoạt.
  - `Views/Grades/Index.cshtml`: Thẻ nhập điểm ma trận theo cột điểm (`.mobile-grade-card`, cập nhật `.avg-cell` qua `input.closest('tr, .mobile-grade-card')`).
  - `Views/Promotion/Index.cshtml`: Thẻ xét duyệt lên lớp & kết chuyển niên khóa (`.student-rollover-row`, `.decision-select`, `.target-class-select`).
  - `Views/Governance/Index.cshtml`: Thẻ phân công Trưởng khối theo niên khóa.
  - `Views/Students/Index.cshtml`: Thẻ hồ sơ thiếu nhi & cụm nút thao tác nhanh.
