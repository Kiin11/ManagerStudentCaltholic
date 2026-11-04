# EXTERNAL INTEGRATIONS OVERVIEW

Dựa trên việc kiểm tra mã nguồn, hệ thống **ManagerStudentCaltholic** không có sự phụ thuộc vào các API gọi ra bên ngoài (External 3rd-party REST/SOAP APIs). Hệ thống giao tiếp nội bộ qua các hạ tầng và thư viện sau:

## 1. Hệ quản trị CSDL & Lưu trữ
- **PostgreSQL Database:** Lưu trữ dữ liệu hệ thống (Entities, Logs, Configurations). Cấu hình chuỗi kết nối qua `ConnectionStrings__DefaultConnection`.
- **Redis Cache (High-throughput Storage):** Được sử dụng để phân tán tải khi điểm danh bằng QR Code và nạp sổ điểm danh O(1).
  - *Provider:* `StackExchange.Redis`.
  - *Configuration:* `ConnectionStrings__RedisConnection`.

## 2. Hệ thống Background Jobs
- **Hangfire:** Quản lý và thực thi các tác vụ nền định kỳ (Recurring Jobs) và hàng đợi (Enqueued Jobs).
  - *Storage:* Sử dụng `Hangfire.PostgreSql` gắn với Database PostgreSQL mặc định.
  - *Sử dụng:* Đồng bộ Redis về DB (mỗi 10 phút), chốt sổ đánh vắng cuối ngày (Thứ 5 & Chúa Nhật), xuất file Excel ngầm.

## 3. Web Client Side Integrations
- **Html5Qrcode:** Thư viện Javascript được tải từ Client-side (Local file trong `~/lib/html5-qrcode/`) để mở Camera quét thẻ QR trực tiếp trên trình duyệt Web/Mobile.
- **IndexedDB:** Ứng dụng PWA (Progressive Web App) lưu trữ cục bộ dữ liệu lưới điểm danh và lệnh quét QR ngoại tuyến (Offline) thông qua `ParishOfflineEngine.js` (Custom Code).