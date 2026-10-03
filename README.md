# ManagerStudentCatholic

Tài liệu hướng dẫn quản lý Database Migration với Entity Framework Core và quy trình triển khai ứng dụng bằng Docker.

---

## 1. Quản lý Migration (EF Core)

> **Lưu ý:** Mở **Developer PowerShell** (hoặc Terminal) tại thư mục chứa dự án trước khi chạy các lệnh bên dưới.

### 1.1. Tạo bản migration mới (Add Migration)

Lệnh này dùng để tạo bản ghi nhận thay đổi mới của model/database schema:

```powershell
dotnet ef migrations add <TenMigration>
```

### 1.2. Cập nhật vào Database (Database Update)

Có hai cách để áp dụng migration vào cơ sở dữ liệu:

* **Cách 1: Chỉ định trực tiếp chuỗi kết nối (Connection String)**
  ```powershell
  dotnet ef database update --connection "<connection_string>"
  ```

* **Cách 2: Sử dụng chuỗi kết nối mặc định trong cấu hình (`appsettings.Development.json`)**
  ```powershell
  dotnet ef database update
  ```

---

## 2. Triển khai với Docker (Build & Run)

Quy trình cập nhật mã nguồn và khởi chạy dịch vụ qua Docker Compose:

### a. Build lại Docker image
Đóng gói mã nguồn mới nhất và bỏ qua cache cũ để đảm bảo nhận toàn bộ thay đổi:
```bash
docker compose build --no-cache app
```

### b. Khởi chạy lại container
Khởi chạy container `app` ở chế độ chạy ngầm (detached mode):
```bash
docker compose up -d app
```

Kiểm tra container đã nạp code mới và chạy ổn định hay chưa:
```bash
docker compose logs --tail 30 <server_name>
```


### c. Theo dõi log ứng dụng
Kiểm tra log thời gian thực để xác nhận ứng dụng đã khởi động và nhận code mới thành công:
```bash
docker compose logs -f app
```

### d. Build lại image và khởi động lại container
Chỉ định Docker Compose build lại riêng service ứng dụng (hoặc toàn bộ) mà không sử dụng cache cũ để nạp các thay đổi mới:
```bash
docker compose up -d --build <server_name>
```
