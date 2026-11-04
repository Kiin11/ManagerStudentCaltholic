# ARCHITECTURE ANALYSIS

## 1. Cấu trúc Kiến trúc Thực tế
Dự án áp dụng mô hình **Monolithic MVC Architecture** kết hợp với **Service Layer Pattern** cho các tác vụ phức tạp. Không áp dụng Clean Architecture hay Repository Pattern.

### 1.1. Controller Layer (MVC)
- **Đặc điểm:** Tiếp nhận HTTP Request, xử lý Authentication/Authorization, Validation.
- **Business Logic trong Controller:** Rất nhiều logic nghiệp vụ, truy vấn và ghi cơ sở dữ liệu được viết trực tiếp tại Controller thông qua Entity Framework Core.
- *Bằng chứng:* `ClassesController.cs`, `AbsenceController.cs` (gọi trực tiếp `_context.AbsenceRequests.Add(...)`, `_context.SaveChangesAsync()`).

### 1.2. Service Layer (Nghiệp vụ phức tạp)
- **Đặc điểm:** Các logic đòi hỏi tính toán nhiều, đồng bộ dữ liệu hoặc giao tiếp ngoại vi được tách ra Service.
- *Bằng chứng:* 
  - `GradeCalculationService.cs` (tính điểm trung bình).
  - `RedisAttendanceService.cs` (xử lý logic điểm danh trên Cache và đồng bộ về DB).
  - `TokenService.cs` (tạo và rotate JWT/Refresh Token).

### 1.3. Data Access Layer
- **Đặc điểm:** Không sử dụng Repository Pattern. Sử dụng trực tiếp `ParishDbContext` (kế thừa `DbContext` của EF Core) để truy xuất dữ liệu trong cả Controllers và Services.
- *Bằng chứng:* Khai báo `private readonly ParishDbContext _context;` phủ khắp các lớp Controller.

### 1.4. Background Jobs Layer
- **Đặc điểm:** Sử dụng **Hangfire** (PostgreSql Storage) để xử lý các tác vụ nền không đồng bộ (Asynchronous processing).
- *Bằng chứng:* `BackgroundExportJobService.cs`, `Program.cs` (đăng ký `RecurringJob.AddOrUpdate`).
  - Xuất báo cáo Excel (`GenerateClassExcelReportJobAsync`).
  - Đồng bộ điểm danh Redis xuống Database mỗi 10 phút.
  - Tự động đánh vắng học sinh cuối ngày.

## 2. Request Flow (Luồng xử lý dữ liệu)

### Flow 1: Luồng xử lý CRUD cơ bản (Xác nhận từ source)
**HTTP Request** (từ Client) -> **Routing & Rate Limiter Middleware** -> **Controller** (Kiểm tra quyền `[Authorize]`) -> **Direct Data Access** (truy vấn qua `_context`) -> **PostgreSQL Database** -> **Controller** -> Trả về **Razor View / JSON Response**.
*(Bằng chứng: `AnnouncementsController.cs`)*

### Flow 2: Luồng Điểm danh QR Code Tốc độ cao (Xác nhận từ source)
**HTTP Request** (Quét QR) -> **AttendanceController.ScanCheckIn()** -> **RedisAttendanceService.ProcessQrCheckInViaRedisAsync()** -> **Redis Cache** (kiểm tra/lưu bộ đệm O(1)) -> Trả về **JSON Response** cho màn hình quét.
*(Bằng chứng: `AttendanceController.cs`, `RedisAttendanceService.cs`)*

### Flow 3: Luồng Đồng bộ Dữ liệu Nền (Xác nhận từ source)
**Hangfire Worker** (kích hoạt định kỳ) -> **RedisAttendanceService.SyncDirtyAttendancesToDatabaseAsync()** -> Lấy dữ liệu thay đổi từ **Redis** -> Sử dụng **IExecutionStrategy & Transaction** -> Lưu hàng loạt xuống **PostgreSQL Database**.
*(Bằng chứng: `Program.cs`, `RedisAttendanceService.cs`)*