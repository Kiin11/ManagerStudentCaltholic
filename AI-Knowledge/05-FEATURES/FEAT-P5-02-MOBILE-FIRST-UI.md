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
| **Stylesheet** | `wwwroot/css/mobile-layout.css` | Mobile-First CSS Rules | Safe Area Insets, 44x44px touch target, GPU scroll optimization |
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
- **Chiều cao cố định:** `54px` (`--mobile-header-height: 54px`), cộng thêm `env(safe-area-inset-top)` cho màn hình tai thỏ / Dynamic Island.
- **Thành phần Header:** Logo Giáo xứ Phú Trung + Tên phân khu đang mở, nút tắt Quét QR (dành cho nhân sự nội bộ), và nút mở Offcanvas Menu (`44x44px`).
- **Offcanvas Menu (`#mobileUserOffcanvas`):**
  - Đặt `z-index: 1085` (cao hơn Bottom Nav và nút nổi) để không bị che khuất.
  - Danh sách lối tắt bên trong sử dụng `flex-grow-1 overflow-y-auto` (`-webkit-overflow-scrolling: touch`) giúp kéo mượt trên màn hình nhỏ.
  - Nút **Đăng xuất** (hoặc **Đăng nhập**) được ghim cố định ở đáy Offcanvas (`flex-shrink-0`), đảm bảo luôn nhìn thấy và bấm được ngay cả với tài khoản Admin có nhiều menu.

### 4.2. Thanh Điều hướng Đáy 5 Phân khu (`_LayoutMobileBottomNav.cshtml`)
- **Dải Sub-module Pills (`.mobile-subnav-strip`):** Nằm phía trên nội dung chính, cho phép vuốt ngang chọn nhanh các chức năng con thuộc phân khu hiện tại.
- **Bottom Navigation Bar (`.mobile-bottom-nav`):**
  - Cố định tại đáy màn hình (`position: fixed; bottom: 0; height: calc(64px + env(safe-area-inset-bottom))`).
  - 5 nút điều hướng chính:
    1. **Chung** (`COMMON` - `/Home/Index?master=COMMON`)
    2. **Học Vụ** (`ACADEMIC` - `/Home/Index?master=ACADEMIC`)
    3. **Nút trung tâm nổi (FAB):**
       - Nếu đã đăng nhập (`isAuthenticated`): **Quét QR** (`/Attendance/ScanQr`).
       - Nếu là phụ huynh/khách: **Tra Cứu** (`/TraCuu`).
    4. **Bí Tích** (`SACRAMENT` - `/Home/Index?master=SACRAMENT`)
    5. **Quản Trị** (`GOVERNANCE` - `/Home/Index?master=GOVERNANCE`, hoặc `ATTENDANCE` nếu phân khu Quản trị bị khóa quyền).

### 4.3. Tối ưu Hiệu năng Di động (Performance Optimizations)
1. **Loại bỏ `backdrop-filter: blur(...)` trên Mobile:** Tránh hiện tượng giật khung hình (scroll jank) trên chip đồ họa di động khi cuộn qua các phần tử cố định.
2. **Chỉ nạp script lắng nghe cuộn (`scrollToTopBtn`) trên nhánh Desktop (`!isMobile`):** Tránh gắn sự kiện `window.scroll` và ẩn nút cuộn nổi trên màn hình điện thoại để không đè lên tab thứ 5 của Bottom Navigation Bar.
3. **Kiểm soát vòng đời PWA Service Worker (`pwa-installer.js`):** Chỉ gọi `reg.update()` trên trang chủ thay vì mọi lần chuyển trang, và chỉ tự động `window.location.reload()` khi người dùng chủ động nhấn nút cập nhật (`window.__pwaUserRequestedUpdate = true`).
