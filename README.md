# ManagerStudentCatholic

Tài liệu hướng dẫn quản lý Database Migration với Entity Framework Core và quy trình triển khai ứng dụng bằng Docker.

---
## A. Sơ đồ Cấu trúc Thư mục & Hạ tầng Triển khai
Kiin11/ManagerStudentCaltholic (branch: main)
├── .github/                        # Cấu hình GitHub Workflows / CI-CD
├── nginx/                          # Cấu hình Reverse Proxy (TASK-704)
├── ManagerStudentCaltholic/        # Source code chính (ASP.NET Core MVC - C# & Razor HTML)
│   ├── Controllers/                # Điều hướng request & kiểm tra phân quyền (GradesController,...)
│   ├── Data/                       # ApplicationDbContext & EF Core Migrations
│   ├── Models/                     # Entities ánh xạ bảng PostgreSQL
│   ├── Services/                   # Nghiệp vụ tính điểm (NCalc) & Phân quyền GLV
│   ├── ViewModels/                 # DTOs & ViewModels truyền dữ liệu ra View
│   └── Views/                      # Giao diện người dùng Razor (.cshtml)
├── ManagerStudentCaltholic.sln     # Solution file
├── docker-compose.yml              # Định nghĩa multi-container (Nginx, App, PostgreSQL)
├── .dockerignore & .gitignore      # Cấu hình loại trừ file khi build/commit
└── README.md                       # Tài liệu hướng dẫn Migration & Docker

## B. Sơ đồ Luồng Kiến trúc Hệ thống (System & Layered Architecture)

graph TD
    subgraph Client ["Trình duyệt (Giáo lý viên / Ban Điều Hành / Admin)"]
        UI["Giao diện Razor Views + Bootstrap + Fetch API (Inline Grading)"]
    end

    subgraph DockerHost ["Hạ tầng Docker Compose"]
        Nginx["Nginx Reverse Proxy (Thư mục /nginx)"]
        
        subgraph DotNetApp ["Container: app (ASP.NET Core MVC)"]
            Auth["Middleware Xác thực & Phân quyền (ASP.NET Identity / Roles)"]
            Ctrl["Controllers (GradesController, ...)"]
            PermSvc["GradePermissionService (Kiểm tra lớp phụ trách & Kế thừa cấu hình)"]
            CalcSvc["GradeCalculationService (NCalc Formula Engine & Điểm chuyên cần)"]
            EFCore["ApplicationDbContext (Entity Framework Core)"]
        end

        Postgres[("PostgreSQL Database")]
    end

    UI -->|HTTP / HTTPS + JSON| Nginx
    Nginx -->|Proxy Pass| Auth
    Auth --> Ctrl
    Ctrl --> PermSvc
    Ctrl --> CalcSvc
    PermSvc --> EFCore
    Ctrl --> EFCore
    EFCore -->|TCP/IP (Connection String)| Postgres
	

## C. Sơ đồ Quan hệ Thực thể Cơ sở dữ liệu
erDiagram
    CLASSES ||--o{ TEACHER_CLASSES : "phân công phụ trách"
    CLASSES ||--o{ GRADE_COLUMNS : "cấu hình cột riêng (nullable)"
    CLASSES ||--o{ ATTENDANCE_SCORE_RULES : "quy tắc chuyên cần (nullable)"
    CLASSES ||--o{ GRADING_FORMULAS : "công thức tính (nullable)"
    STUDENTS ||--o{ STUDENT_GRADES : "có điểm chi tiết"
    STUDENTS ||--o{ STUDENT_GRADE_SUMMARIES : "có tổng kết kỳ/năm"
    GRADE_COLUMNS ||--o{ STUDENT_GRADES : "thuộc cột điểm"

    TEACHER_CLASSES {
        int id PK
        varchar user_id "ID tài khoản GLV"
        int class_id FK
        varchar role_in_class "GLV_TRUONG, GLV_PHU, TRO_TA"
        varchar academic_year
        boolean is_active
    }

    GRADE_COLUMNS {
        int id PK
        varchar academic_year
        int semester "1: HK1, 2: HK2, 0: Cả năm"
        int class_id FK "NULL = Mặc định toàn xứ"
        varchar column_code "CC, KT_15P, THI..."
        varchar column_name
        numeric coefficient "Hệ số điểm"
        boolean is_attendance_based
        int sort_order
    }

    ATTENDANCE_SCORE_RULES {
        int id PK
        varchar academic_year
        int semester
        int class_id FK "NULL = Mặc định toàn xứ"
        numeric base_score
        numeric penalty_mass_unexcused
        numeric penalty_mass_excused
        numeric penalty_mass_late
        numeric penalty_class_unexcused
        numeric penalty_class_excused
        numeric penalty_class_late
        numeric min_score
    }

    GRADING_FORMULAS {
        int id PK
        varchar academic_year
        int semester
        int class_id FK "NULL = Mặc định toàn xứ"
        text formula_expression "Biểu thức NCalc"
        jsonb evaluation_rules "Ngưỡng Giỏi, Khá, Đạt"
    }

    STUDENT_GRADES {
        int id PK
        int student_id FK
        int grade_column_id FK
        int semester
        numeric score "0.00 - 10.00"
        text note
        varchar updated_by "User ID của GLV"
        timestamp updated_at
    }

    STUDENT_GRADE_SUMMARIES {
        int id PK
        int student_id FK
        varchar academic_year
        int semester
        numeric average_score
        varchar classification
        boolean is_passed
        timestamp updated_at
    }

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
