# TECHNOLOGY STACK

## 1. Backend Stack
- **Framework:** .NET 8 / ASP.NET Core MVC (Xác minh qua `<TargetFramework>net8.0</TargetFramework>` trong `ManagerStudentCaltholic.csproj` và cú pháp C# 12).
- **Authentication/Authorization:** Hybrid (Cookie Authentication cho MVC Views + JWT Bearer Token cho Ajax/API requests). (Xác minh: `ServiceCollectionExtensions.cs`).
- **Background Processing:** Hangfire (phiên bản `1.8.14`) với Hangfire.PostgreSql (`1.20.9`). (Xác minh: `csproj`).
- **Logging:** Serilog (`Serilog.AspNetCore 8.0.3`) ghi log ra file và console. (Xác minh: `LoggingExtensions.cs`).
- **Server-side Device Detection:** `HttpRequestExtensions.IsMobileDevice()` sử dụng Compiled Regex (`RegexOptions.IgnoreCase | RegexOptions.Compiled`) kết hợp HTTP Client Hints (`Sec-CH-UA-Mobile`) để phân tách nhánh render Mobile/Desktop tại tầng Razor Layout. (Xác minh: `Extensions/HttpRequestExtensions.cs`).

## 2. Data & Storage
- **Relational Database:** PostgreSQL.
- **ORM:** Entity Framework Core 8 (`Microsoft.EntityFrameworkCore 8.0.11`, `Npgsql.EntityFrameworkCore.PostgreSQL`).
- **Caching / High-throughput Storage:** Redis (`StackExchange.Redis 2.8.16`). (Xác minh: `CacheSettings` trong `appsettings.json`, `RedisAttendanceService.cs`, `RedisCacheService.cs`).
- **Local Storage (Client-side):** IndexedDB (Dùng cho Offline-first Attendance). (Xác minh qua logic comment/JS call trong `Views/Attendance/Index.cshtml` nhắc đến `ParishOfflineEngine`).

## 3. Thư viện tiện ích (C#)
- **Xử lý Excel:** ClosedXML (`0.102.3`). (Xác minh: `StudentExcelService.cs`).
- **Tạo mã QR:** QRCoder (`1.6.0`). (Xác minh: `LookupController.cs`, `QrCodeService`).
- **Phân tích toán học/Công thức:** NCalc (`7.0.0`). (Xác minh: `csproj`).
- **Xử lý PDF:** PdfPig (`0.1.9`). (Xác minh: `csproj`).

## 4. Frontend Stack
- **UI Framework:** Bootstrap 5 (Xác minh qua class CSS: `rounded-4`, `shadow-sm`, `bg-primary-subtle`).
- **Mobile-First Responsive UI:** Custom CSS (`wwwroot/css/mobile-layout.css`) hỗ trợ vùng an toàn `env(safe-area-inset-top)` / `env(safe-area-inset-bottom)` trên iOS/Android, kích thước nút chạm tối thiểu `44x44px`, Bottom Navigation Bar 5 phân khu, và tối ưu GPU compositing/cuộn mượt trên thiết bị di động. (Xác minh: `mobile-layout.css`, `_LayoutMobileHeader.cshtml`, `_LayoutMobileBottomNav.cshtml`).
- **View Engine:** Razor Pages (`.cshtml`).
- **Mã vạch / QR (Client-side):** Html5Qrcode (Xác minh: `<script src="~/lib/html5-qrcode/html5-qrcode.min.js">` trong `ScanQr.cshtml`).
- **Biểu đồ:** Chart.js (Xác minh: `<script src="https://cdn.jsdelivr.net/npm/chart.js@4.4.3/...` trong `Logs/Index.cshtml`).
- **PWA (Progressive Web App):** Sử dụng `manifest.json` và `wwwroot/js/pwa-installer.js` (cấu hình hiển thị độc lập `standalone`, kiểm soát vòng đời Service Worker qua cờ `window.__pwaUserRequestedUpdate`).
