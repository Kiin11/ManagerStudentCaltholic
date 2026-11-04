# GEMINI AI DEVELOPER INSTRUCTIONS & PROJECT PLAYBOOK

> **Project:** `ManagerStudentCaltholic` (Hệ thống Quản lý Thiếu Nhi Thánh Thể - Giáo xứ Phú Trung)  
> **Target Framework:** .NET 8 / ASP.NET Core MVC (C# 12)  
> **Architecture:** Single-Project Monolith with Service Layer  
> **Knowledge Base:** Refer to `AI-Knowledge/` for detailed architectural maps, schemas, code indexes, and feature specifications.

---

## 1. Project Overview & Business Domain

**ManagerStudentCaltholic** là ứng dụng web monolithic phục vụ công tác quản lý toàn diện các hoạt động của Xứ Đoàn Thiếu Nhi Thánh Thể thuộc Giáo xứ Phú Trung:
- **Quản lý Hồ sơ & Lớp học:** Quản lý lý lịch thiếu nhi, phân bổ lớp học theo niên khóa, kết chuyển năm học & lưu vết mã cũ (`StudentCodeHistories`), và import/export Excel qua ClosedXML.
- **Điểm danh & Chuyên cần (High-throughput & Offline-first):** Quét mã QR tốc độ cao nạp vào Redis Cache (`O(1)`) kết hợp ghi Write-Through xuống PostgreSQL, hỗ trợ PWA offline qua IndexedDB và đồng bộ batch sync khi có mạng, tự động chốt vắng cuối ngày qua Hangfire, giải quyết khiếu nại (`AttendanceDisputeRequests`).
- **Học vụ & Sổ điểm:** Cấu hình cột điểm linh hoạt, tính điểm trung bình qua `IGradeCalculationService` / NCalc, xếp loại và đánh giá điều kiện lên lớp.
- **Sổ bộ Bí tích (Sacraments):** Theo dõi tiến trình 4 mốc Bí tích (Rửa tội, Rước lễ lần đầu, Thêm sức, Bao đồng), tạo tờ trình phê duyệt gửi Linh mục Chánh xứ.
- **Chiến dịch Đi Lễ (Mass Campaigns):** Theo dõi chuỗi đi lễ liên tục (Streak) trong các đợt thi đua phụng vụ.
- **Cổng Tra cứu Công khai & Tra cứu 360 Độ Nội bộ (`FEAT-P5-01`):**
  - *Công khai (`/TraCuu`):* Phụ huynh tra cứu thẻ thiếu nhi, chuyên cần, gửi đơn xin phép nghỉ và báo sai sót điểm danh.
  - *Nội bộ tại Trang chủ (`/Home/Index`):* GLV/BĐH đã đăng nhập tra cứu tức thời thiếu nhi theo Họ tên (có dấu/không dấu), Tên Thánh, Mã hiện hành hoặc Mã năm cũ mà không cần Ngày sinh/SĐT; chuyển đổi tức thì 4 chế độ xem (`OVERVIEW`, `ATTENDANCE_DETAIL`, `ACADEMIC_2Y_SACRAMENT`, `PARENT_CONTACT`) với bộ đệm Client `Map` + Redis Cache 5 phút (`staff_lookup:student_360:{studentId}`).
- **Giao diện Đa nền tảng & Mobile-First Web UI (`FEAT-P5-02`):** Tự động nhận diện thiết bị di động ở phía Server (`HttpRequestExtensions.IsMobileDevice()`) để rẽ nhánh `@if (isMobile)` tại `_Layout.cshtml`: cung cấp Header thu gọn, Offcanvas menu cuộn mượt ghim nút Đăng xuất, Thanh điều hướng đáy cố định 5 phân khu (`_LayoutMobileBottomNav.cshtml`), hỗ trợ Safe Area Inset iOS/Android, trong khi vẫn giữ nguyên 100% giao diện Desktop trên màn hình lớn.
- **Quản trị & Phân quyền:** Phân quyền theo vai trò (`Admin`, `SpiritualDirector`, `ExecutiveBoard`, `BranchHead`, `Teacher`), cấu hình quyền nút chức năng động (`FeaturePermissionRules`), giám sát System Health và Audit Log.

---

## 2. AI-Knowledge Navigation Matrix

Thư mục `AI-Knowledge/` lưu trữ toàn bộ dữ liệu phân tích hệ thống. Khi xử lý công việc, **hãy luôn đọc `AI-Knowledge/AI-KNOWLEDGE-STRUCTURE.md` và tra cứu đúng tài liệu tương ứng** trước khi sửa đổi:

| Thư mục / File | Nội dung & Mục đích | Khi nào cần tham khảo? |
|---|---|---|
| `AI-Knowledge/AI-KNOWLEDGE-STRUCTURE.md` | Bản đồ cấu trúc, quy tắc cập nhật & kiểm định `AI-Knowledge` | Đọc đầu tiên trước khi tra cứu hoặc cập nhật Knowledge |
| `AI-Knowledge/00-OVERVIEW/PROJECT.md` | Mục đích, entry points, 8 nhóm module chính | Nắm bắt tổng quan nghiệp vụ và kiến trúc |
| `AI-Knowledge/00-OVERVIEW/ARCHITECTURE.md` | Kiến trúc tầng và 5 luồng xử lý cốt lõi (Request Flows) | Khi thiết kế flow mới, tích hợp cache hoặc tối ưu hiệu năng |
| `AI-Knowledge/00-OVERVIEW/CODING-RULES.md` | Quy chuẩn đặt tên, query, transaction, bảo mật | Trước khi viết hoặc refactor bất kỳ code C# nào |
| `AI-Knowledge/00-OVERVIEW/TECH-STACK.md` | Danh mục thư viện, framework, phiên bản, frontend/PWA | Khi thêm package mới hoặc chỉnh sửa hạ tầng UI/PWA |
| `AI-Knowledge/01-MODULES/ATTENDANCE.md` | Chi tiết phân hệ điểm danh, ca học, sync DB | Khi can thiệp logic điểm danh hoặc QR code |
| `AI-Knowledge/02-DATABASE/DATABASE-OVERVIEW.md` | Danh sách bảng, quan hệ Entity, mapping ORM | Khi viết `ParishDbContext`, migration, hoặc truy vấn phức tạp |
| `AI-Knowledge/02-DATABASE/TABLE-RELATIONSHIPS.md` | Sơ đồ ERD và ràng buộc Foreign Keys / Cascade | Khi thêm quan hệ bảng hoặc xử lý Soft-Delete / Cascade |
| `AI-Knowledge/02-DATABASE/TABLE-INDEX.json` | Chỉ mục cấu trúc bảng, cột, PK, FK, Indexes | Khi tra cứu nhanh schema bảng và index hiện có |
| `AI-Knowledge/03-CODE-INDEX/*.jsonl` | Chỉ mục máy đọc: `FILE`, `SYMBOL`, `ROUTE`, `DEPENDENCY` | Khi định vị chính xác file, chữ ký hàm, route, và DI |
| `AI-Knowledge/04-INTEGRATIONS/` | Tích hợp Redis, Hangfire, Nginx, IndexedDB | Khi cấu hình background jobs, cache hoặc offline storage |
| `AI-Knowledge/05-FEATURES/FAST-QR-CHECKIN-AND-OFFLINE-SYNC.md` | Đặc tả Điểm danh QR tốc độ cao & Đồng bộ Offline | Khi bảo trì luồng quét QR và `ParishOfflineEngine` |
| `AI-Knowledge/05-FEATURES/FEAT-P5-01-STAFF-QUICK-LOOKUP.md` | Đặc tả Cổng tra cứu 360 độ thiếu nhi nội bộ tại HomePage | Khi bảo trì `StaffStudentLookupService` hoặc widget tra cứu |
| `AI-Knowledge/05-FEATURES/FEAT-P5-02-MOBILE-FIRST-UI.md` | Đặc tả Giao diện Mobile-First, Bottom Nav & Safe Area | Khi chỉnh sửa `_Layout.cshtml`, `_LayoutMobile*.cshtml`, `mobile-layout.css` |
| `AI-Knowledge/06-UPDATE-LOG/` | `KNOWLEDGE-BASELINE.json`, `UPDATE-LOG.md`, báo cáo Audit | Khi đồng bộ tài liệu theo Git commit mới hoặc kiểm toán Knowledge |

---

## 3. Technology Stack & Key Dependencies

- **Runtime & Language:** .NET 8 (`net8.0`), C# 12.
- **Web Framework:** ASP.NET Core MVC (Controllers & Razor Views `.cshtml`), Server-side Mobile Detection (`HttpRequestExtensions.IsMobileDevice`).
- **Database & ORM:** PostgreSQL kết hợp Entity Framework Core 8 (`Npgsql.EntityFrameworkCore.PostgreSQL`).
- **Caching & High Throughput:** Redis (`StackExchange.Redis 2.8.16`) — dùng cho cả bộ đệm điểm danh QR (`IRedisAttendanceService`, prefix `ATT:*`) và bộ đệm chung (`ICacheService` / `RedisCacheService`, ví dụ `staff_lookup:student_360:{studentId}`).
- **Background Jobs:** Hangfire (`1.8.14`, `Hangfire.PostgreSql 1.20.9`).
- **Logging:** Serilog (`Serilog.AspNetCore 8.0.3`) ghi console và rolling file logs.
- **Client, Mobile-First UI & PWA:**
  - Bootstrap 5.3 + Bootstrap Icons 1.11, Vanilla JavaScript.
  - `mobile-layout.css`: Khung giao diện Mobile-First, Safe Area Insets (`env(safe-area-inset-*)`), chuẩn nút chạm `44x44px`, tối ưu GPU compositing.
  - `pwa-installer.js` & `manifest.json`: Quản lý cài đặt PWA và kiểm soát cập nhật Service Worker (`window.__pwaUserRequestedUpdate`).
  - `Html5Qrcode` (quét camera QR), IndexedDB (`offline-attendance.js` / `ParishOfflineEngine`), Chart.js.
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
- **Class, Interface, Method, Property, Enum:** Sử dụng `PascalCase` (ví dụ: `StaffStudentLookupService`, `CalculateSemesterAverage`, `StudentCode`).
- **Tham số & Biến cục bộ:** Sử dụng `camelCase` (ví dụ: `studentId`, `academicYearId`, `selectedOption`).
- **Private Dependency Injected Fields:** Bắt đầu bằng gạch dưới `_` + `camelCase` (ví dụ: `_context`, `_logger`, `_cacheService`, `_lookupService`).
- **Endpoints / Actions:** Đặt tên rõ ràng, sử dụng Attribute HTTP verb chuẩn MVC (`[HttpGet]`, `[HttpPost]`).

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
  - Các entity nghiệp vụ chính kế thừa `ISoftDelete` (chứa `IsDeleted`, `DeletedAt`).
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
- **Authentication & Roles:** Phân quyền chặt chẽ bằng `[Authorize(Roles = ...)]` sử dụng hằng số từ `UserRole` (ví dụ: `UserRole.Admin`, `UserRole.ExecutiveBoard`, `UserRole.BranchHead`, `UserRole.Teacher`). Với các API AJAX nội bộ đặt trong Controller dùng chung (như `HomeController`), bắt buộc kiểm tra trạng thái xác thực và quyền truy cập trước khi trả dữ liệu nhạy cảm.
- **Anti-CSRF:**
  - Áp dụng `[ValidateAntiForgeryToken]` cho toàn bộ action `[HttpPost]`.
  - Các cuộc gọi AJAX/Fetch `POST` phải đính kèm header `X-CSRF-TOKEN` lấy từ thẻ input `__RequestVerificationToken` trên view.
- **Rate Limiting:** Sử dụng `[EnableRateLimiting("PolicyPublicLookup")]` cho các API tra cứu công khai hoặc xác thực để tránh spam và brute-force.
- **Input Validation:** Kiểm tra `ModelState.IsValid` và giới hạn độ dài chuỗi đầu vào trước khi ghi DB.

### 4.4. Response, Layout & Mobile-First UI Conventions
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
- **Phân tách Giao diện Mobile-First vs Desktop (`FEAT-P5-02`):**
  - Giữ nguyên cấu trúc rẽ nhánh `@if (isMobile)` (`bool isMobile = Context.Request.IsMobileDevice();`) tại `Views/Shared/_Layout.cshtml`: mọi nâng cấp cho giao diện Mobile **không được làm hỏng hoặc xê dịch bố cục Desktop (`else`)**.
  - Trên giao diện Mobile: đảm bảo kích thước vùng chạm tối thiểu `44x44px` (`.touch-target-44`), tôn trọng vùng an toàn `env(safe-area-inset-*)`, **tránh dùng `backdrop-filter: blur(...)`** trên thanh cố định và tránh gắn `scroll` listener nặng gây giật khung hình trên điện thoại tầm trung.
  - Menu trượt `#mobileMenuOffcanvas` (`z-index: 1070`) phải giữ `.offcanvas-body` cuộn độc lập (`flex-grow-1 overflow-y-auto`) và ghim nút **Đăng xuất** cố định ở `.offcanvas-footer` (`flex-shrink-0`), không hiển thị nút cuộn nổi đè lên thanh điều hướng đáy (`.mobile-bottom-nav`).

---

## 5. Core Architectural Flows

### 5.1. Điểm danh QR tốc độ cao (Fast QR Check-in & Write-Through)
```text
Client (Web/Camera) 
   ──> POST /Attendance/ScanCheckIn (QrScanRequestDto)
   ──> AttendanceController 
   ──> RedisAttendanceService.ProcessQrCheckInViaRedisAsync()
   ──> Kiểm tra Redis Cache O(1) [ATT:QR:DATE:{date}:CODE:{code}]
          ├── Cache Hit: Đánh giá thời gian (AttendanceTimeHelper) -> Cập nhật IsDirty = true
          └── Cache Miss: Đọc PostgreSQL -> Nạp lại Cache (Pre-warm) -> Cập nhật Redis
   ──> Đồng bộ Write-Through tức thì xuống PostgreSQL (SyncDirtyAttendancesToDatabaseAsync)
   ──> Trả về QrScanResponseDto (Success/Duplicate/Error) + UI phát âm thanh
```

### 5.2. Điểm danh Ngoại tuyến (Offline-First) & Đồng bộ
```text
Mất mạng: Quét QR ──> Lưu trữ cục bộ IndexedDB (ParishOfflineEngine)
Có mạng:  Nhấn "Đồng bộ" ──> POST /Attendance/SyncOfflineQueue (OfflineSyncQueueRequestDto)
          ──> Controller xác thực quyền truy cập lớp
          ──> Hợp nhất trạng thái (Conflict Resolution)
          ──> Batch Upsert vào Database qua ExecutionStrategy & Transaction
          ──> Phản hồi syncedCount và làm sạch hàng đợi IndexedDB
```

### 5.3. Đồng bộ dữ liệu nền (Hangfire Background Jobs)
- **Đồng bộ dự phòng Redis về Database:** Định kỳ mỗi 10 phút, worker quét các lớp còn cờ `IsDirty = true` trên Redis để ghi hàng loạt xuống PostgreSQL.
- **Chốt vắng cuối ngày:** Tự động đánh vắng không phép cho học sinh không điểm danh vào chiều Thứ 5 và Chúa Nhật.
- **Xuất báo cáo ngầm:** Tác vụ xuất file Excel danh sách học sinh / điểm số lớn chạy trong Hangfire (`BackgroundJobsController`) kèm toast theo dõi tiến trình.

### 5.4. Tra cứu 360 Độ Thiếu nhi Nội bộ tại Trang chủ (`FEAT-P5-01`)
```text
GLV/BĐH gõ từ khóa tại HomePage (#staffStudentQuickSearchInput - debounce 280ms)
   ──> GET /Home/StaffSearchSuggestions?term=...
   ──> StaffStudentLookupService.SearchStudentsQuickAsync()
       (Tìm trên Students + không dấu + mã cũ trong StudentCodeHistories)
   ──> Chọn thiếu nhi: Kiểm tra student360ClientCache (0ms) hoặc gọi:
       GET /Home/StaffStudentLookup?studentId=...&option=...
   ──> ICacheService.GetOrSetAsync("staff_lookup:student_360:{studentId}", TTL 5 phút)
   ──> (Cache Miss) StaffStudentLookupService.GetStudent360DetailAsync() + IGradeCalculationService
   ──> Ghi nhận lịch sử vào StaffQuickLookupLogs (RecordLookupLogAsync)
   ──> Render tức thì 4 tab Option (OVERVIEW | ATTENDANCE_DETAIL | ACADEMIC_2Y_SACRAMENT | PARENT_CONTACT)
```

### 5.5. Phân nhánh Giao diện Mobile-First & Desktop (`FEAT-P5-02`)
```text
HTTP Request ──> Views/Shared/_Layout.cshtml
   ──> Context.Request.IsMobileDevice() (Query ?device= -> Sec-CH-UA-Mobile -> Compiled UA Regex)
          ├── isMobile == true:  Bỏ tải Google Fonts & nút cuộn nổi -> Render _LayoutMobileHeader
          │                      + .mobile-main-wrapper + _LayoutMobileBottomNav (5 phân khu)
          └── isMobile == false: Giữ nguyên #app-sidebar + #main-wrapper + Topbar 5 phân khu Desktop
```

---

## 6. Project Directory Layout

```text
ManagerStudentCaltholic/
├── AI-Knowledge/                 # Kho tri thức kiến trúc, DB schema, JSONL code indexes & đặc tả feature
└── ManagerStudentCaltholic/      # Web Application Project (.NET 8 MVC)
    ├── Controllers/              # MVC Controllers (HomeController, AttendanceController, GradesController...)
    ├── Data/                     # ParishDbContext (40+ DbSets, Fluent API, Indexes, Seed Data)
    ├── Extensions/               # DI, Security, Logging, RateLimit, HttpRequestExtensions (Mobile detect)
    ├── Interface/                # Interfaces cho Services & Models (ISoftDelete, IStaffStudentLookupService...)
    ├── Middlewares/              # Custom Middlewares (SecurityHeaders...)
    ├── Migrations/               # EF Core Database Migrations
    ├── Models/
    │   ├── DTOs/                 # Data Transfer Objects cho AJAX/API (QrScan, StaffStudentLookupDtos...)
    │   ├── Entities/             # Entity Classes ánh xạ bảng PostgreSQL
    │   ├── Enum/                 # Enums hệ thống (SacramentTypeEnum...)
    │   └── ViewModels/           # ViewModels phục vụ hiển thị lên Razor Views (MasterWorkspaceViewModel...)
    ├── Security/                 # Cấu hình Token, Password Hashing, Policy
    ├── Services/                 # Business Logic Layer (RedisAttendance, StaffStudentLookup, Grades, Excel...)
    ├── Views/                    # Razor Views (.cshtml), bao gồm Shared/_Layout*.cshtml & Home/_StaffStudentLookupWidget.cshtml
    └── wwwroot/                  # Static files (css/mobile-layout.css, js/staff-student-lookup.js, pwa-installer.js...)
```

---

## 7. Development & Deployment Workflows

### 7.1. Quản lý Migration (EF Core)
```powershell
# Tạo migration mới khi thay đổi Entity hoặc ParishDbContext
dotnet ef migrations add <TenMigration> --project ManagerStudentCaltholic

# Cập nhật migration vào cơ sở dữ liệu (sử dụng chuỗi kết nối từ appsettings.Development.json)
dotnet ef database update --project ManagerStudentCaltholic

# Hoặc chỉ định rõ connection string:
dotnet ef database update --project ManagerStudentCaltholic --connection "<connection_string>"
```

### 7.2. Build và Run Local
> **Lưu ý:** Nếu ứng dụng đang chạy ở tiến trình khác (ví dụ khóa file `ManagerStudentCaltholic.dll`), hãy dừng tiến trình đang chạy trước khi `dotnet build` hoặc dùng `--no-build` khi phù hợp.
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

1. **Tuân thủ hiện trạng Monolith:** Không tự ý tách solution thành nhiều project `.csproj` con hoặc đưa vào các pattern phức tạp không có sẵn (như CQRS, Clean Architecture, Generic Repository). Hãy giữ kiến trúc nhất quán với `ParishDbContext` và `Services/`.
2. **Kiểm tra Soft-Delete & Niên khóa hiện hành:** Bất kỳ thao tác query/update nào cũng cần lưu ý cờ `IsDeleted` và niên khóa hiện hành (`AcademicYear.IsCurrent`).
3. **Giữ an toàn cho Luồng Điểm danh & Bộ đệm Redis:** Khi sửa đổi logic điểm danh hoặc hồ sơ học sinh, luôn đảm bảo tương thích giữa Redis key format (`ATT:*`, `staff_lookup:student_360:*`), DTOs của `Html5Qrcode`, và bảng `Attendances` trong PostgreSQL.
4. **Bảo toàn Trải nghiệm Kép (Desktop & Mobile-First):** Khi chỉnh sửa Razor Views hoặc CSS, luôn kiểm tra cả hai chế độ (`?device=desktop` và `?device=mobile`), không để phần tử cố định che khuất nút chức năng hoặc làm giật cuộn trên thiết bị di động.
5. **Đồng bộ & Kiểm chứng `AI-Knowledge` từ Source Code:**
   - Source code luôn là **nguồn sự thật duy nhất (Single Source of Truth)**.
   - Khi thêm/sửa Entity, Service, Controller, Route hoặc luồng nghiệp vụ, cần cập nhật chính xác các file tương ứng trong `AI-Knowledge/` (`00-OVERVIEW/`, `02-DATABASE/`, `03-CODE-INDEX/*.jsonl`, `05-FEATURES/`, `06-UPDATE-LOG/`), không tự suy diễn query/route không có thật và gắn nhãn `NEEDS_VERIFICATION` cho mọi thông tin chưa đủ căn cứ xác minh.
   - Không bao giờ đưa mật khẩu thật, connection string chứa secret, hoặc JWT secret key vào tài liệu `AI-Knowledge/` hay `GEMINI.md`.
