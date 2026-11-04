# GEMINI AI DEVELOPER INSTRUCTIONS & PROJECT PLAYBOOK

> **Project:** `ManagerStudentCaltholic` (Hệ thống Quản lý Thiếu Nhi Thánh Thể - Giáo xứ Phú Trung)  
> **Target Framework:** .NET 8 / ASP.NET Core MVC (C# 12)  
> **Architecture:** Single-Project Monolith with Service Layer  
> **Knowledge Base:** Refer to `AI-Knowledge/` for detailed architectural maps, schemas, and specifications.

---

## 1. Project Overview & Business Domain

**ManagerStudentCaltholic** là ứng dụng web monolithic phục vụ công tác quản lý toàn diện các hoạt động của Xứ Đoàn Thiếu Nhi Thánh Thể thuộc Giáo xứ Phú Trung:
- **Quản lý Hồ sơ & Lớp học:** Quản lý lý lịch thiếu nhi, phân bổ lớp học theo niên khóa, kết chuyển năm học (Promotion rollover), và import/export Excel qua ClosedXML.
- **Điểm danh & Chuyên cần (High-throughput & Offline-first):** Quét mã QR tốc độ cao nạp vào Redis Cache (O(1)), hỗ trợ PWA offline qua IndexedDB và đồng bộ batch sync khi có mạng, tự động chốt vắng cuối ngày qua Hangfire, giải quyết khiếu nại (Dispute).
- **Học vụ & Sổ điểm:** Cấu hình cột điểm linh hoạt, tính điểm trung bình bằng NCalc, xếp loại và đánh giá điều kiện lên lớp.
- **Sổ bộ Bí tích (Sacraments):** Theo dõi tiến trình 4 mốc Bí tích (Rửa tội, Rước lễ lần đầu, Thêm sức, Bao đồng), tạo tờ trình phê duyệt gửi Linh mục Chánh xứ.
- **Chiến dịch Đi Lễ (Mass Campaigns):** Theo dõi chuỗi đi lễ liên tục (Streak) trong các đợt thi đua phụng vụ.
- **Cổng Tra cứu & Tương tác Phụ huynh:** Tra cứu thẻ thiếu nhi và chuyên cần công khai, gửi đơn xin phép nghỉ, xuất mẫu tin nhắn Zalo/Viber.
- **Quản trị & Phân quyền:** Phân quyền theo vai trò (Admin, Ban Điều Hành, Huynh Trưởng / GLV, Phụ huynh), giám sát System Health và Audit Log.

---

## 2. AI-Knowledge Navigation Matrix

Thư mục `AI-Knowledge/` lưu trữ toàn bộ dữ liệu phân tích hệ thống. Khi xử lý công việc, **hãy luôn tra cứu đúng tài liệu tương ứng** trước khi sửa đổi:

| Thư mục / File | Nội dung & Mục đích | Khi nào cần tham khảo? |
|---|---|---|
| `AI-Knowledge/AI-KNOWLEDGE-STRUCTURE.md` | Bản đồ cấu trúc toàn bộ kho tri thức AI | Định hướng vị trí các tài liệu trong `AI-Knowledge` |
| `AI-Knowledge/00-OVERVIEW/PROJECT.md` | Mục đích, entry points, module chính | Nắm bắt tổng quan nghiệp vụ và kiến trúc |
| `AI-Knowledge/00-OVERVIEW/ARCHITECTURE.md` | Luồng xử lý dữ liệu và kiến trúc tầng | Khi thiết kế flow mới hoặc tối ưu hiệu năng |
| `AI-Knowledge/00-OVERVIEW/CODING-RULES.md` | Quy chuẩn đặt tên, query, transaction, bảo mật | Trước khi viết hoặc refactor bất kỳ code C# nào |
| `AI-Knowledge/00-OVERVIEW/TECH-STACK.md` | Danh mục thư viện, framework, phiên bản | Khi thêm package mới hoặc tích hợp thư viện |
| `AI-Knowledge/01-MODULES/ATTENDANCE.md` | Chi tiết phân hệ điểm danh, ca học, sync DB | Khi can thiệp logic điểm danh hoặc QR code |
| `AI-Knowledge/02-DATABASE/DATABASE-OVERVIEW.md` | Danh sách bảng, quan hệ Entity, mapping ORM | Khi viết DbContext, migration, hoặc truy vấn phức tạp |
| `AI-Knowledge/02-DATABASE/TABLE-RELATIONSHIPS.md` | Sơ đồ ERD và ràng buộc Foreign Keys | Khi thêm quan hệ bảng hoặc xử lý Cascade Delete |
| `AI-Knowledge/03-CODE-INDEX/*.jsonl` | Chỉ mục file, symbol, route, dependency | Khi tìm kiếm nhanh file, class, method, endpoint URL |
| `AI-Knowledge/04-INTEGRATIONS/` | Tích hợp Redis, Hangfire, Nginx, IndexedDB | Khi cấu hình background jobs hoặc cache |
| `AI-Knowledge/05-FEATURES/` | Đặc tả luồng xử lý chi tiết từng tính năng | Khi bảo trì tính năng Điểm danh QR & Offline sync |
| `AI-Knowledge/06-UPDATE-LOG/` | Báo cáo kiểm định source code & baseline | Khi đối chiếu tính nhất quán giữa tài liệu và mã nguồn |

---

## 3. Technology Stack & Key Dependencies

- **Runtime & Language:** .NET 8 (`net8.0`), C# 12.
- **Web Framework:** ASP.NET Core MVC (Controllers & Razor Pages `.cshtml`).
- **Database & ORM:** PostgreSQL kết hợp Entity Framework Core 8 (`Npgsql.EntityFrameworkCore.PostgreSQL`).
- **Caching & High Throughput:** Redis (`StackExchange.Redis 2.8.16`).
- **Background Jobs:** Hangfire (`Hangfire.PostgreSql 1.20.9`).
- **Logging:** Serilog (`Serilog.AspNetCore 8.0.3`) ghi console và rolling file logs.
- **Client & PWA:** Bootstrap 5, Javascript Vanilla, `Html5Qrcode` (quét camera QR), IndexedDB (`ParishOfflineEngine.js`), Chart.js.
- **Utility Libraries:**
  - `ClosedXML` (`0.102.3`): Đọc và xuất báo cáo Excel học sinh, bảng điểm.
  - `QRCoder` (`1.6.0`): Tạo mã QR định danh thiếu nhi.
  - `NCalc` (`7.0.0`): Phân tích cú pháp biểu thức tính điểm.
  - `PdfPig` (`0.1.9`): Trích xuất và xử lý file PDF.
- **Deployment:** Docker, Docker Compose, Nginx (Reverse Proxy & Rate Limiter).

---

## 4. Coding Rules & Architectural Standards

Khi viết code trong dự án này, Gemini **bắt buộc tuân thủ** các quy tắc sau:

### 4.1. Naming Conventions
- **Class, Interface, Method, Property, Enum:** Sử dụng `PascalCase` (ví dụ: `RedisAttendanceService`, `CalculateSemesterAverage`, `StudentCode`).
- **Tham số & Biến cục bộ:** Sử dụng `camelCase` (ví dụ: `studentId`, `academicYearId`).
- **Private Dependency Injected Fields:** Bắt đầu bằng gạch dưới `_` + `camelCase` (ví dụ: `_context`, `_logger`, `_cache`, `_attendanceService`).
- **Endpoints / Actions:** Đặt tên rõ ràng, sử dụng Attribute routing chuẩn MVC (ví dụ: `[HttpGet]`, `[HttpPost]`).

### 4.2. Database & Entity Framework Core Guidelines
- **Không áp dụng Generic Repository Pattern:** Sử dụng trực tiếp `ParishDbContext` trong Controllers và Services theo đúng thiết kế hiện tại của dự án.
- **Tối ưu Read Queries:** Luôn sử dụng `.AsNoTracking()` cho các truy vấn chỉ đọc (read-only) để tránh overhead tracking của EF Core:
  ```csharp
  var students = await _context.Students
      .AsNoTracking()
      .Where(s => s.IsActive && !s.IsDeleted)
      .ToListAsync();
  ```
- **Xóa mềm (Soft Delete):**
  - Mọi entity kế thừa `ISoftDelete` (chứa `IsDeleted`, `DeletedAt`).
  - Hệ thống đã cấu hình **Global Query Filter** (`e => !e.IsDeleted`) và ghi đè `SaveChangesAsync()` để chuyển Delete thành Soft-Delete. Không thực hiện xóa cứng (hard delete) dữ liệu trừ khi có yêu cầu đặc biệt.
- **Transaction & Execution Strategy:**
  - Khi thực hiện nhiều thao tác ghi hoặc batch operations liên quan đến nhiều bảng, luôn sử dụng `IExecutionStrategy` kết hợp transaction để đảm bảo an toàn kết nối:
  ```csharp
  var strategy = _context.Database.CreateExecutionStrategy();
  await strategy.ExecuteAsync(async () =>
  {
      await using var transaction = await _context.Database.BeginTransactionAsync();
      // ... thực thi logic ...
      await _context.SaveChangesAsync();
      await transaction.CommitAsync();
  });
  ```

### 4.3. Security & Validation
- **Authentication & Roles:** Phân quyền chặt chẽ bằng `[Authorize(Roles = ...)]` sử dụng hằng số từ `UserRole` (ví dụ: `UserRole.Admin`, `UserRole.ExecutiveBoard`, `UserRole.Teacher`).
- **Anti-CSRF:**
  - Áp dụng `[ValidateAntiForgeryToken]` cho toàn bộ action `[HttpPost]`.
  - Các cuộc gọi AJAX/Fetch phải đính kèm header `X-CSRF-TOKEN` lấy từ thẻ input anti-forgery token trên view.
- **Rate Limiting:** Sử dụng `[EnableRateLimiting("PolicyPublicLookup")]` cho các API tra cứu công khai hoặc xác thực để tránh spam và brute-force.
- **Input Validation:** Kiểm tra `ModelState.IsValid` và validate dữ liệu DTO trước khi xử lý.

### 4.4. Response & UI Conventions
- **AJAX / API JSON Response:** Luôn trả về cấu trúc chuẩn:
  ```json
  {
    "success": true,
    "message": "Thông báo trạng thái",
    "data": { ... }
  }
  ```
- **MVC View Notification:** Sử dụng `TempData["SuccessMessage"]` và `TempData["ErrorMessage"]` khi redirect sau action để hiển thị toast/alert trên giao diện Razor.
- **UI Consistency:** Dùng Bootstrap 5 components, tuân thủ các class giao diện đang dùng (`rounded-4`, `shadow-sm`, `bg-primary-subtle`, v.v.).

---

## 5. Core Architectural Flows

### 5.1. Điểm danh QR tốc độ cao (Fast QR Check-in)
```text
Client (Web/Camera) 
   ──> POST /Attendance/ScanCheckIn (QrScanRequestDto)
   ──> AttendanceController 
   ──> RedisAttendanceService.ProcessQrCheckInViaRedisAsync()
   ──> Kiểm tra Redis Cache O(1) [ATT:QR:DATE:{date}:CODE:{code}]
          ├── Cache Hit: Đánh giá thời gian (AttendanceTimeHelper) -> Cập nhật IsDirty = true
          └── Cache Miss: Đọc PostgreSQL -> Nạp lại Cache (Pre-warm) -> Cập nhật Redis
   ──> Ghi Write-Through xuống Database hoặc xếp lịch đồng bộ
   ──> Trả về QrScanResponseDto (Success/Duplicate/Error) + UI phát âm thanh
```

### 5.2. Điểm danh Ngoại tuyến (Offline-First) & Đồng bộ
```text
Mất mạng: Quét QR ──> Lưu trữ cục bộ IndexedDB (ParishOfflineEngine.js)
Có mạng:  Nhấn "Đồng bộ" ──> POST /Attendance/SyncOfflineQueue (OfflineSyncQueueRequestDto)
          ──> Controller xác thực quyền truy cập lớp
          ──> Hợp nhất trạng thái (Conflict Resolution)
          ──> Batch Upsert vào Database qua Transaction
          ──> Phản hồi syncedCount và làm sạch hàng đợi IndexedDB
```

### 5.3. Đồng bộ dữ liệu nền (Hangfire Background Jobs)
- **Đồng bộ Redis về Database:** Định kỳ mỗi 10 phút, worker quét các lớp có cờ `IsDirty = true` trên Redis để ghi hàng loạt xuống PostgreSQL.
- **Chốt vắng cuối ngày:** Tự động đánh vắng không phép cho học sinh không điểm danh vào chiều Thứ 5 và Chúa Nhật.
- **Xuất báo cáo ngầm:** Tác vụ xuất file Excel danh sách học sinh / điểm số lớn chạy trong Hangfire để tránh timeout request.

---

## 6. Project Directory Layout

```text
ManagerStudentCaltholic/
├── Controllers/          # MVC Controllers (chứa routing, auth, business logic & CRUD)
├── Data/                 # ParishDbContext và cấu hình Entity Framework Fluent API
├── Extensions/           # Extension methods cấu hình DI, Security, Logging, RateLimit
├── Interface/            # Interfaces cho Services (IRedisAttendanceService, ITokenService...)
├── Middlewares/          # Custom Middlewares (ExceptionHandling, Auth, Logging)
├── Migrations/           # EF Core Database Migrations
├── Models/
│   ├── DTOs/             # Data Transfer Objects cho AJAX requests và API
│   ├── Entities/         # Entity Classes tương ứng các bảng PostgreSQL
│   ├── Enum/             # Enums hệ thống (UserRole, AttendanceStatus...)
│   └── ViewModels/       # ViewModels phục vụ hiển thị dữ liệu lên Razor Views
├── Security/             # Cấu hình Token, Password Hashing, Policy
├── Services/             # Business Logic Layer (Redis, Grades, Sacraments, Excel...)
├── Views/                # Razor Views (.cshtml) phân nhóm theo chức năng
└── wwwroot/              # Static files (CSS, JS, PWA manifest, local vendor libs)
```

---

## 7. Development & Deployment Workflows

### 7.1. Quản lý Migration (EF Core)
```powershell
# Tạo migration mới khi thay đổi Entity
dotnet ef migrations add <TenMigration> --project ManagerStudentCaltholic

# Cập nhật migration vào cơ sở dữ liệu (sử dụng chuỗi kết nối từ appsettings.Development.json)
dotnet ef database update --project ManagerStudentCaltholic

# Hoặc chỉ định rõ connection string:
dotnet ef database update --project ManagerStudentCaltholic --connection "<connection_string>"
```

### 7.2. Build và Run Local
```powershell
# Khôi phục packages và build
dotnet restore
dotnet build

# Chạy ứng dụng ở môi trường Development
dotnet run --project ManagerStudentCaltholic
```

### 7.3. Triển khai Docker & Docker Compose
```bash
# Build lại image ứng dụng không dùng cache
docker compose build --no-cache app

# Khởi chạy container ngầm
docker compose up -d app

# Xem logs thời gian thực
docker compose logs -f app

# Rebuild và khởi động lại toàn bộ services
docker compose up -d --build
```

---

## 8. Guidelines for Gemini When Modifying This Codebase

1. **Tuân thủ hiện trạng Monolith:** Không tự ý tách solution thành nhiều project `.csproj` con hoặc đưa vào các pattern phức tạp không có sẵn (như CQRS, Clean Architecture, Repository pattern). Hãy giữ kiến trúc nhất quán với `ParishDbContext` và `Services/`.
2. **Kiểm tra Soft-Delete & Multi-tenant Rules:** Bất kỳ thao tác query/update nào cũng cần lưu ý cờ `IsDeleted` và niên khóa hiện hành (`AcademicYear`).
3. **Giữ an toàn cho Luồng Điểm danh:** Khi sửa đổi logic điểm danh, luôn đảm bảo tương thích giữa Redis key format (`ATT:*`), DTOs của `Html5Qrcode`, và bảng `Attendances` trong PostgreSQL.
4. **Cập nhật AI-Knowledge:** Nếu thêm mới Entity, Service, Controller hoặc thay đổi luồng nghiệp vụ lớn, hãy cập nhật các tài liệu tương ứng trong `AI-Knowledge/` (đặc biệt là `TABLE-RELATIONSHIPS.md`, `FILE-INDEX.jsonl`, và `AI-Knowledge/00-OVERVIEW/`).
5. **Code Quality & Bảo mật:** Luôn áp dụng kiểm tra quyền `[Authorize]`, CSRF Token, và mã hóa/bảo vệ dữ liệu nhạy cảm của thiếu nhi và phụ huynh.
