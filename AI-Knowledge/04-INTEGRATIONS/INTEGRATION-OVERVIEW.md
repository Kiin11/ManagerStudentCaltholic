# EXTERNAL INTEGRATIONS OVERVIEW

Dựa trên việc kiểm tra mã nguồn hiện hành (`Phase-F5.8`, commit `4589783`), hệ thống **ManagerStudentCaltholic** kết nối với **Google Drive API v3** cho lưu trữ tệp đám mây và vận hành cùng các hạ tầng lưu trữ/tác vụ nền sau:

## 1. Hệ quản trị CSDL & Lưu trữ Cục bộ / Bộ đệm
- **PostgreSQL Database:** Lưu trữ dữ liệu hệ thống (Entities, Logs, Configurations, bảng metadata `CloudMediaFiles`). Cấu hình chuỗi kết nối qua `ConnectionStrings__DefaultConnection`.
- **Redis Cache (High-Throughput & Fallback):** Được sử dụng để phân tán tải khi điểm danh bằng QR Code, nạp sổ điểm danh O(1) và cache dữ liệu hệ thống qua `ICacheService` / `RedisCacheService.cs`. Tích hợp cơ chế **Circuit Breaker `30 giây`** (`IsRedisCircuitOpen` / `TripRedisCircuit`) tự động chuyển sang **`IMemoryCache` fallback** khi Redis ngoại tuyến.
  - *Provider:* `StackExchange.Redis`, `IMemoryCache`.
  - *Configuration:* `ConnectionStrings__RedisConnection` (`appsettings.json`, `docker-compose.yml`).

## 2. Lưu trữ Đám mây & Tối ưu Hình ảnh (`FEAT-P5-08`)
- **Google Drive API v3 (`GoogleDriveStorageService`):**
  - *SDK:* `Google.Apis.Drive.v3` (`1.69.0.3769`), `Google.Apis.Auth` (`1.78.0`).
  - *Xác thực:* Service Account JSON đọc từ đường dẫn cấu hình `GoogleDrive:ServiceAccountKeyPath` (mặc định `/app/secrets/google-service-account.json`, không lưu secret trong repository), tự nạp lại `DriveService` theo `LastWriteTimeUtc` của file key.
  - *Truyền tải & Chịu lỗi:* Sử dụng Resumable Upload (chunk `256 KB`..`2 MB`), hỗ trợ My Drive và Shared Drive (`SupportsAllDrives = true`, `IncludeItemsFromAllDrives = true`), cache thư mục 30 phút trong `IMemoryCache`, fallback an toàn xuống `PrivateUploads/cloud_fallback` (`LOCAL_FALLBACK`) khi Drive lỗi hoặc chưa cấu hình, và stream bảo mật qua Auth Proxy `/CloudMedia/File/{id}` (`CloudMediaController`).
- **Tối ưu Hình ảnh (`ImageOptimizationService`):**
  - *Thư viện:* `SixLabors.ImageSharp` (`3.1.12` thuần managed, tương thích Alpine Docker).
  - *Chức năng:* Kiểm tra magic bytes, giới hạn kích thước trước giải mã (`10000x10000` / `50,000,000` px), `AutoOrient()`, xóa metadata EXIF/IPTC/XMP và nén sang WebP lossy.

## 3. Hệ thống Background Jobs & Hosted Services
- **Hangfire:** Quản lý và thực thi các tác vụ nền định kỳ (Recurring Jobs) và hàng đợi (Enqueued Jobs).
  - *Storage:* Sử dụng `Hangfire.PostgreSql` gắn với Database PostgreSQL mặc định.
  - *Sử dụng:* Job dự phòng đồng bộ Redis còn `IsDirty` về DB (mỗi 2 phút, bổ trợ cho luồng Write-Behind), chốt sổ đánh vắng cuối ngày (Thứ 5 & Chúa Nhật), xuất file Excel ngầm.
- **ASP.NET Core `BackgroundService` (`DriveLogUploadBackgroundService` & `AttendanceWriteBehindBackgroundService`):**
  - `DriveLogUploadBackgroundService`: Nén GZip streaming các file `.log` Serilog quá thời hạn `LocalRetentionDays` theo lịch Cron 5 trường múi giờ Việt Nam (`0 2 * * *` mặc định) hoặc `IntervalMinutes`, tải lên Google Drive (`System_Logs/{yyyy-MM}`, `AllowLocalFallback = false`) và chỉ xóa file `.log` gốc sau khi đã xác thực `ExternalFileId`.

## 4. Web Client Side Integrations
- **Html5Qrcode:** Thư viện Javascript được tải từ Client-side (Local file trong `~/lib/html5-qrcode/`) để mở Camera quét thẻ QR trực tiếp trên trình duyệt Web/Mobile (kèm điều khiển Torch/Flip/Zoom tại `Views/Attendance/ScanQr.cshtml`).
- **IndexedDB:** Ứng dụng PWA (Progressive Web App) lưu trữ cục bộ dữ liệu thiếu nhi (bao gồm `HistoricalCodes`), lưới điểm danh và hàng đợi quét QR ngoại tuyến thông qua module `ParishOfflineEngine` trong `wwwroot/js/offline-attendance.js` (hỗ trợ `undoOfflineQrScan` và đồng bộ khi có mạng).