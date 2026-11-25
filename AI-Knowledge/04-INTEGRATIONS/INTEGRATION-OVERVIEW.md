# EXTERNAL INTEGRATIONS OVERVIEW

Dựa trên việc kiểm tra mã nguồn hiện hành (`HEAD` `37a30f3` trên nhánh `main` — hợp nhất `Phase-F5.8`, `Fix-Sec-Inf` và OAuth2 Refresh Token Drive), hệ thống **ManagerStudentCaltholic** kết nối với **Google Drive API v3** cho lưu trữ tệp đám mây và vận hành cùng các hạ tầng lưu trữ/tác vụ nền sau:

## 1. Hệ quản trị CSDL & Lưu trữ Cục bộ / Bộ đệm
- **PostgreSQL Database:** Lưu trữ dữ liệu hệ thống (Entities, Logs, Configurations, bảng metadata `CloudMediaFiles`). Cấu hình chuỗi kết nối qua `ConnectionStrings__DefaultConnection` (khuyến nghị `SSL Mode=VerifyCA`/`VerifyFull`; `Program.cs` cảnh báo non-Development nếu còn `Trust Server Certificate=true` hoặc thiếu `SSL Mode`).
- **Redis Cache (High-Throughput & Fallback):** Được sử dụng để phân tán tải khi điểm danh bằng QR Code, khóa idempotency đồng bộ offline (`ATT:OFFLINE:IDEMP:*`), khóa chống brute-force công khai (`lookup:fail:*`, `absence:fail:*`, `pubreg:fail:*`), nạp sổ điểm danh O(1) và cache dữ liệu hệ thống qua `ICacheService` / `RedisCacheService.cs`. Tích hợp cơ chế **Circuit Breaker `30 giây`** (`IsRedisCircuitOpen` / `TripRedisCircuit`) tự động chuyển sang **`IMemoryCache` fallback** khi Redis ngoại tuyến.
  - *Provider:* `StackExchange.Redis`, `IMemoryCache`.
  - *Configuration:* `ConnectionStrings__RedisConnection` (mặc định `""` trong `appsettings.json`, chỉ bật Redis khi truyền biến môi trường; `Program.cs` cảnh báo non-Development nếu thiếu `password=`).

## 2. Lưu trữ Đám mây & Tối ưu Hình ảnh (`FEAT-P5-08`)
- **Google Drive API v3 (`GoogleDriveStorageService`):**
  - *SDK:* `Google.Apis.Drive.v3` (`1.69.0.3769`), `Google.Apis.Auth` (`1.78.0`).
  - *Xác thực:* Hỗ trợ 2 chế độ (mặc định tắt `GoogleDrive:Enabled = false` và rỗng credential trong repo app; nạp qua biến môi trường từ `parish-infra-config`): ưu tiên **OAuth2 Refresh Token** (`OAuthClientId`, `OAuthClientSecret`, `OAuthRefreshToken` cho quota Google Drive cá nhân) qua `GoogleAuthorizationCodeFlow` + `UserCredential`, dự phòng **Service Account JSON** (`ServiceAccountKeyPath` cho Shared Drive) tự nạp lại theo `LastWriteTimeUtc` của file key.
  - *Truyền tải & Chịu lỗi:* Sử dụng Resumable Upload (chunk `256 KB`..`2 MB`, giới hạn `MaxUploadBytes = 30 MB`, khóa thư mục cố định 64-stripe `FolderLockStripes`), hỗ trợ My Drive và Shared Drive (`SupportsAllDrives = true`, `IncludeItemsFromAllDrives = true`), cache thư mục 30 phút trong `IMemoryCache`, fallback an toàn xuống `PrivateUploads/cloud_fallback` (`LOCAL_FALLBACK`) khi Drive lỗi hoặc chưa cấu hình, và stream bảo mật qua Auth Proxy `/CloudMedia/File/{id}` (`CloudMediaController` gắn `CSP: sandbox` cho PDF và `attachment` cho `ABSENCE_DOC`).
- **Tối ưu Hình ảnh (`ImageOptimizationService`):**
  - *Thư viện:* `SixLabors.ImageSharp` (`3.1.12` thuần managed, tương thích Alpine Docker).
  - *Chức năng:* Kiểm tra magic bytes, giới hạn kích thước trước giải mã (`10000x10000` / `50,000,000` px), `AutoOrient()`, xóa metadata EXIF/IPTC/XMP và nén sang WebP lossy (áp dụng cả cho ảnh chứng nhận tuyển sinh công khai).

## 3. Hệ thống Background Jobs & Hosted Services
- **Hangfire:** Quản lý và thực thi các tác vụ nền định kỳ (Recurring Jobs, dùng `VietnamTimeHelper.VietnamTimeZone`) và hàng đợi (Enqueued Jobs).
  - *Storage:* Sử dụng `Hangfire.PostgreSql` gắn với Database PostgreSQL mặc định.
  - *Sử dụng:* Job dự phòng đồng bộ Redis còn `IsDirty` về DB (mỗi 2 phút, bổ trợ cho luồng Write-Behind), chốt sổ đánh vắng cuối ngày (Thứ 5 & Chúa Nhật), xuất file Excel ngầm (`BackgroundExportJobService` gom nhóm bằng SQL projection và ghi thẳng `FileStream`).
- **ASP.NET Core `BackgroundService` (`DriveLogUploadBackgroundService`, `AttendanceWriteBehindBackgroundService`, `LogRetentionBackgroundService`):**
  - `DriveLogUploadBackgroundService`: Nén GZip streaming các file `.log` Serilog quá thời hạn `LocalRetentionDays` (bỏ qua file `> 200 MB`, áp dụng `RetryDelaySeconds`, xóa bù bản đã verified) theo lịch Cron 5 trường múi giờ Việt Nam (`0 2 * * *` mặc định) hoặc `IntervalMinutes`, tải lên Google Drive (`System_Logs/{yyyy-MM}`, `AllowLocalFallback = false`, mặc định `Enabled = false`) và chỉ xóa file `.log` gốc sau khi đã xác thực `ExternalFileId`.
  - `LogRetentionBackgroundService` (đăng ký 1 singleton dùng chung với `ILogMaintenanceJobService`): Dọn log `> 90 ngày`, file export `> 24 giờ` và file tạm mồ côi `> 2 giờ` trong `PrivateUploads/tmp_logs` & `PrivateUploads/cloud_tmp`.

## 4. Web Client Side Integrations
- **Html5Qrcode:** Thư viện Javascript được tải từ Client-side (Local file trong `~/lib/html5-qrcode/`) để mở Camera quét thẻ QR trực tiếp trên trình duyệt Web/Mobile (kèm điều khiển Torch/Flip/Zoom tại `Views/Attendance/ScanQr.cshtml`).
- **IndexedDB:** Ứng dụng PWA (Progressive Web App) nâng cấp schema `DB_VERSION = 2` (chuyển index `by_enrollmentId` sang non-unique để hỗ trợ `HistoricalCodes`), tính ngày theo giờ local (`formatLocalDateYyyyMmDd`), đồng bộ idempotent chỉ xóa các `syncedClientIds` được server xác nhận, và hỗ trợ `undoOfflineQrScan` kèm fallback gọi server undo trong `wwwroot/js/offline-attendance.js`.