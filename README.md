# ManagerStudentCaltholic

##1. Lệnh Tạo Bản Migration Mới (Add Migration)
Mở Developer PowerShell tại thư mục dự án
dotnet ef migrations add <TenMigration>

##2. Lệnh Cập Nhật / Đẩy Migration Vào Database (Database Update)
dotnet ef database update --connection "{connectring}"
```[cite: 11]

#### Cách 2: Chạy theo cấu hình trong `appsettings.Development.json`
```powershell
dotnet ef database update

## 3. Build lên Docker
# a. Build lại app image để đóng gói mã nguồn mới nhất (bỏ qua cache cũ)
docker compose build --no-cache app

# b. Khởi chạy lại container app ở chế độ nền
docker compose up -d app

# c. Theo dõi log xem ứng dụng đã nạp code mới thành công chưa
docker compose logs -f app