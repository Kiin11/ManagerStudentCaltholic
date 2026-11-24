# ManagerStudentCatholic

Tài liệu hướng dẫn quản lý Database Migration với Entity Framework Core và quy trình đóng gói / triển khai Docker Image qua GitHub Container Registry (GHCR).

---

## 1. Quản lý Migration (EF Core)

> **Lưu ý:** Mở **Developer PowerShell** (hoặc Terminal) tại thư mục chứa dự án trước khi chạy các lệnh bên dưới.

### 1.1. Tạo bản migration mới (Add Migration)

Lệnh này dùng để tạo bản ghi nhận thay đổi mới của model/database schema:

```powershell
dotnet ef migrations add <TenMigration> --project ManagerStudentCaltholic
```

### 1.2. Cập nhật vào Database (Database Update)

Có hai cách để áp dụng migration vào cơ sở dữ liệu:

* **Cách 1: Chỉ định trực tiếp chuỗi kết nối (Connection String)**
  ```powershell
  dotnet ef database update --project ManagerStudentCaltholic --connection "<connection_string>"
  ```

* **Cách 2: Sử dụng chuỗi kết nối mặc định trong cấu hình cục bộ (`appsettings.Development.json`, không commit lên Git)**
  ```powershell
  dotnet ef database update --project ManagerStudentCaltholic
  ```

---

## 2. Đóng gói Docker Image & Triển khai (GHCR Immutable Image Workflow)

Kho mã nguồn này chỉ chứa **source code ứng dụng & Dockerfile**. Toàn bộ cấu hình triển khai Production (`docker-compose.yml`, Nginx, `.env`, script deploy/rollback) được tách sang kho riêng tư `parish-infra-config`.

- **Chuẩn đặt tên Tag Image:**
  - Bản phát hành thường: `{name}-v1.{yy}.{phase}.{number}` (ví dụ: `parish-app-v1.26.inf.1`)
  - Bản vá lỗi (Hotfix): `{name}-v1.{yy}.{phase}-{hotfix}.{number}` (ví dụ: `parish-app-v1.26.5-hf1.2`)
  - Full OCI Ref: `ghcr.io/kiin11/parish-app:<TAG>`

### Cách 1: Build & Push tự động bằng GitHub Actions (Không cần cài Docker trên máy)

Mở tab **Actions** trên GitHub -> chọn workflow **Build and Push Docker Image to GHCR** (`.github/workflows/build-push-ghcr.yml`) -> **Run workflow** và nhập `phase`, `hotfix` (nếu có), `number`.

### Cách 2: Build & Push từ máy cục bộ (Local Script)

```powershell
# Kiểm tra tag và lệnh build (Dry-Run, không cần Docker)
powershell -File scripts/build-push.ps1 -Phase inf -Number 1 -DryRun

# Build linux/amd64 và đẩy lên GHCR
powershell -File scripts/build-push.ps1 -Phase inf -Number 1 -Push
```

```bash
# Linux / macOS / WSL
./scripts/build-push.sh --phase inf --number 1 --push
```

### Triển khai lên máy chủ Azure VM

Chạy tại thư mục `parish-infra-config` trên máy chủ Azure (tên service Docker Compose chuẩn hóa là `dotnet_app`):

```bash
./scripts/deploy.sh parish-app-v1.26.inf.1
# Hoặc thủ công sau khi cập nhật APP_IMAGE_TAG trong .env:
# docker compose pull dotnet_app && docker compose up -d --no-deps dotnet_app
```

### Yêu cầu Bảo mật & Lưu trữ Bền vững Production (`parish-infra-config`)

- **Tên service Compose chuẩn:** Luôn sử dụng `dotnet_app` trong mọi lệnh `docker compose` (không dùng `app`).
- **Volume lưu trữ bền vững bắt buộc:** Mount `/app/PrivateUploads` (cùng `/app/dataprotection-keys`, `/app/logs`, `/app/secrets`, `/app/wwwroot/uploads/lesson_plans`) để không mất file export, file fallback và tài liệu giáo án khi recreate container.
- **Bảo mật PostgreSQL TLS:** Chuỗi kết nối Production bắt buộc dùng `SSL Mode=VerifyCA` hoặc `SSL Mode=VerifyFull` kèm chứng chỉ CA hợp lệ (tuyệt đối **không** dùng `Trust Server Certificate=true` trên Production).
- **Bảo mật Redis:** Khởi chạy Redis với `--requirepass ${REDIS_PASSWORD}`, truyền `password=${REDIS_PASSWORD}` trong `ConnectionStrings__RedisConnection`, và **không** publish/expose cổng `6379` ra ngoài public host (chỉ giao tiếp qua mạng nội bộ Docker).

Xem chi tiết quy trình đăng nhập GHCR, quy chuẩn tag và triển khai tại [docs/DOCKER-BUILD-DEPLOY.md](docs/DOCKER-BUILD-DEPLOY.md).
