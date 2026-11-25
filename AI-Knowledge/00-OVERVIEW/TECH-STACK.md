# TECHNOLOGY STACK

## 1. Backend Stack
- **Framework:** .NET 8 / ASP.NET Core MVC (Xác minh qua `<TargetFramework>net8.0</TargetFramework>` trong `ManagerStudentCaltholic.csproj` và cú pháp C# 12).
- **Authentication/Authorization & Edge Security:** Hybrid (Cookie Authentication `ParishSessionCookie` cho MVC Views + JWT Bearer Token cho Ajax/API requests). Thời hạn ghi nhớ phiên Cookie (`ExpireTimeSpan = TimeSpan.FromDays(14)`, `SlidingExpiration = true`; mặc định `RememberMe = true` đặt `ExpiresUtc = AddDays(14)`, hoặc `AddDays(8)` nếu bỏ tick) và Refresh Token (`RefreshTokenExpirationDays = 14` ngày; `AccessTokenExpirationMinutes = 11520` phút = 8 ngày); cấu hình Data Protection lưu khóa bền vững xuống thư mục `dataprotection-keys`; cấu hình `ReverseProxy` (`ForwardLimit`, `KnownProxies`, `KnownNetworks` mặc định loopback + `172.16.0.0/12`) qua `ServiceCollectionExtensions.AddReverseProxyConfiguration(IConfiguration?)` và `GlobalLimiter` (`200 req/min/IP`) trong `SecurityExtensions.AddAppRateLimiter`. (Xác minh: `ServiceCollectionExtensions.cs`, `SecurityExtensions.cs`, `AccountController.cs`, `TokenService.cs`, `appsettings.json`).
- **Background Processing & Health:** Hangfire (`1.8.14`) với Hangfire.PostgreSql (`1.20.9`) dùng `VietnamTimeHelper.VietnamTimeZone`, kết hợp ASP.NET Core `BackgroundService` (`AttendanceWriteBehindBackgroundService`, `DriveLogUploadBackgroundService`, `LogRetentionBackgroundService` đăng ký single-instance); endpoint `GET /health` phục vụ Docker `HEALTHCHECK`. (Xác minh: `csproj`, `Program.cs`, `Dockerfile`, `ServiceCollectionExtensions.cs`).
- **Testing:** `xUnit` (`2.9.3`), `Microsoft.NET.Test.Sdk` (`17.12.0`), `Microsoft.EntityFrameworkCore.InMemory` (`8.0.11`) trong `tests/ManagerStudentCaltholic.Tests/ManagerStudentCaltholic.Tests.csproj`.
- **Logging:** Serilog (`Serilog.AspNetCore 8.0.3`) ghi log ra file và console (kèm override `Microsoft.AspNetCore.Antiforgery` ở mức `Fatal`). (Xác minh: `LoggingExtensions.cs`).
- **Server-side Device Detection:** `HttpRequestExtensions.IsMobileDevice()` sử dụng Compiled Regex (`RegexOptions.IgnoreCase | RegexOptions.Compiled`) kết hợp HTTP Client Hints (`Sec-CH-UA-Mobile`) để phân tách nhánh render Mobile/Desktop tại tầng Razor Layout. (Xác minh: `Extensions/HttpRequestExtensions.cs`).

## 2. Data & Storage
- **Relational Database:** PostgreSQL (khuyến nghị `SSL Mode=VerifyCA`/`VerifyFull`; `Program.cs` cảnh báo khi khởi động non-Development nếu còn `Trust Server Certificate=true` hoặc thiếu `SSL Mode`).
- **ORM:** Entity Framework Core 8 (`Microsoft.EntityFrameworkCore 8.0.11`, `Npgsql.EntityFrameworkCore.PostgreSQL`).
- **Caching / High-throughput Storage:** Redis (`StackExchange.Redis 2.8.16`) thông qua `ICacheService` / `RedisCacheService` có cơ chế **Circuit Breaker `30 giây`** (`IsRedisCircuitOpen` / `TripRedisCircuit`) kết hợp **`IMemoryCache` fallback** để tránh treo timeout khi Redis ngoại tuyến. Trong `appsettings.json`, `ConnectionStrings:RedisConnection` đặt mặc định `""` (chỉ bật Redis khi truyền qua biến môi trường; `Program.cs` cảnh báo non-Development nếu thiếu `password=`). (Xác minh: `appsettings.json`, `Program.cs`, `RedisAttendanceService.cs`, `RedisCacheService.cs`).
- **Local Storage (Client-side):** IndexedDB (`DB_VERSION = 2` qua `wwwroot/js/offline-attendance.js` định nghĩa `ParishOfflineEngine`, index `by_enrollmentId` đặt `unique: false` để hỗ trợ alias mã cũ, gọi tại `Views/Attendance/Index.cshtml`, `ScanQr.cshtml`, `Pwa/Index.cshtml`, `AccessDenied.cshtml`).

## 3. Thư viện tiện ích (C#)
- **Xử lý Excel:** ClosedXML (`0.102.3`). (Xác minh: `StudentExcelService.cs`, `ClassCurriculumService.cs`).
- **Tạo mã QR:** QRCoder (`1.6.0`). (Xác minh: `LookupController.cs`, `QrCodeService`).
- **Phân tích toán học/Công thức:** NCalc (`7.0.0`). (Xác minh: `csproj`).
- **Xử lý PDF:** PdfPig (`0.1.9`). (Xác minh: `csproj`, `ClassCurriculumService.cs`).
- **Google Drive API & Xác thực Cloud:** `Google.Apis.Drive.v3` (`1.69.0.3769`) và `Google.Apis.Auth` (`1.78.0`) dùng Service Account JSON (`ServiceAccountCredential` / `DriveService.Scope.Drive`), hỗ trợ Resumable Upload (chunk bội số `256 KB` tối đa `2 MB`) và Shared Drive (`SupportsAllDrives = true`). (Xác minh: `ManagerStudentCaltholic.csproj`, `GoogleDriveStorageService.cs`).
- **Xử lý & Tối ưu Hình ảnh:** `SixLabors.ImageSharp` (`3.1.12`) chuyển đổi sang WebP lossy, `AutoOrient()`, xóa metadata EXIF/IPTC/XMP, kiểm tra magic bytes + giới hạn kích thước ảnh (`Image.IdentifyAsync` tối đa `10000x10000` / `50,000,000` pixels). **Ghi chú tương thích Alpine & Audit:** `SixLabors.ImageSharp` là thư viện thuần managed .NET (không dùng `libgdiplus`, chạy ổn định trên `mcr.microsoft.com/dotnet/aspnet:8.0-alpine`); phiên bản được ghim tại `3.1.12` (do 3.2+/4.x yêu cầu `sixlabors.lic` thương mại) và được giảm thiểu rủi ro qua danh sách trắng magic bytes + kiểm tra `Image.IdentifyAsync` kèm `NuGetAuditSuppress` trong `ManagerStudentCaltholic.csproj`. (Xác minh: `ManagerStudentCaltholic.csproj`, `Dockerfile`, `ImageOptimizationService.cs`).

## 4. Frontend Stack
- **UI Framework:** Bootstrap 5 (Xác minh qua class CSS: `rounded-4`, `shadow-sm`, `bg-primary-subtle`).
- **Mobile-First Responsive UI:** Custom CSS (`wwwroot/css/mobile-layout.css`) hỗ trợ vùng an toàn `env(safe-area-inset-top)` / `env(safe-area-inset-bottom)` trên iOS/Android, kích thước nút chạm tối thiểu `44x44px`, Bottom Navigation Bar 5 phân khu, và tối ưu GPU compositing/cuộn mượt trên thiết bị di động. (Xác minh: `mobile-layout.css`, `_LayoutMobileHeader.cshtml`, `_LayoutMobileBottomNav.cshtml`).
- **View Engine:** Razor Pages (`.cshtml`).
- **Mã vạch / QR (Client-side):** Html5Qrcode (Xác minh: `<script src="~/lib/html5-qrcode/html5-qrcode.min.js">` trong `ScanQr.cshtml`).
- **Biểu đồ:** Chart.js (Xác minh: `<script src="https://cdn.jsdelivr.net/npm/chart.js@4.4.3/...` trong `Logs/Index.cshtml`).
- **PWA (Progressive Web App):** Sử dụng `manifest.json` và `wwwroot/js/pwa-installer.js` (cấu hình hiển thị độc lập `standalone`, kiểm soát vòng đời Service Worker qua cờ `window.__pwaUserRequestedUpdate`).
