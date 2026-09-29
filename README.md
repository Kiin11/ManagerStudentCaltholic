# ManagerStudentCaltholic

##1. Lệnh Tạo Bản Migration Mới (Add Migration)
Mở Developer PowerShell tại thư mục dự án
dotnet ef migrations add <TenMigration>

##2. 2. Lệnh Cập Nhật / Đẩy Migration Vào Database (Database Update)
dotnet ef database update --connection "{connectring}"
```[cite: 11]

#### Cách 2: Chạy theo cấu hình trong `appsettings.Development.json`
```powershell
dotnet ef database update

