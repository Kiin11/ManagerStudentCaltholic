# EXTERNAL INTEGRATIONS OVERVIEW

Dựa trên việc kiểm tra mã nguồn, hệ thống **ManagerStudentCaltholic** không có sự phụ thuộc vào các API gọi ra bên ngoài (External 3rd-party REST/SOAP APIs). Hệ thống giao tiếp nội bộ qua các hạ tầng và thư viện sau:

## 1. Hệ quản trị CSDL & Lưu trữ
- **PostgreSQL Database:** Lưu trữ dữ liệu hệ thống (Entities, Logs, Configurations). Cấu hình chuỗi kết nối qua `ConnectionStrings__DefaultConnection`.
- **Redis Cache (High-Throughput & Fallback):** Được sử dụng để phân tán tải khi điểm danh bằng QR Code, nạp sổ điểm danh O(1) và cache dữ liệu hệ thống qua `ICacheService` / `RedisCacheService.cs`. Tích hợp cơ chế **Circuit Breaker `30 giây`** (`IsRedisCircuitOpen` / `TripRedisCircuit`) tự động chuyển sang **`IMemoryCache` fallback** khi Redis ngoại tuyến.
  - *Provider:* `StackExchange.Redis`, `IMemoryCache`.
  - *Configuration:* `ConnectionStrings__RedisConnection` (`appsettings.json`, `docker-compose.yml`).

## 2. Hệ thống Background Jobs
- **Hangfire:** Quản lý và thực thi các tác vụ nền định kỳ (Recurring Jobs) và hàng đợi (Enqueued Jobs).
  - *Storage:* Sử dụng `Hangfire.PostgreSql` gắn với Database PostgreSQL mặc định.
  - *Sử dụng:* Job dự phòng đồng bộ Redis còn `IsDirty` về DB (mỗi 10 phút, bổ trợ cho luồng Write-Through trực tiếp ở `ScanCheckIn`/`SaveBatch`), chốt sổ đánh vắng cuối ngày (Thứ 5 & Chúa Nhật), xuất file Excel ngầm.

## 3. Web Client Side Integrations
- **Html5Qrcode:** Thư viện Javascript được tải từ Client-side (Local file trong `~/lib/html5-qrcode/`) để mở Camera quét thẻ QR trực tiếp trên trình duyệt Web/Mobile (kèm điều khiển Torch/Flip/Zoom tại `Views/Attendance/ScanQr.cshtml`).
- **IndexedDB:** Ứng dụng PWA (Progressive Web App) lưu trữ cục bộ dữ liệu thiếu nhi (bao gồm `HistoricalCodes`), lưới điểm danh và hàng đợi quét QR ngoại tuyến thông qua module `ParishOfflineEngine` trong `wwwroot/js/offline-attendance.js` (hỗ trợ `undoOfflineQrScan` và đồng bộ khi có mạng).