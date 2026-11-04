# PROJECT OVERVIEW

## 1. Mục đích và Vai trò Project
Dự án **ManagerStudentCaltholic** là một ứng dụng Web Monolithic quản lý toàn diện các hoạt động của một trường Giáo lý thuộc Giáo xứ (cụ thể: Giáo xứ Phú Trung). 
- **Vai trò:** Hệ thống hóa việc quản lý hồ sơ thiếu nhi, xếp lớp, điểm danh (tích hợp mã QR và Offline-first), đánh giá học lực, xét duyệt điều kiện lãnh nhận Bí tích, và thông tin liên lạc với phụ huynh.

## 2. Project References & Entry Points
- **Entry Point:** `Program.cs` (Khởi tạo WebApplication, Kestrel server, cấu hình Middleware, Hangfire, Serilog, và Route mặc định).
- **Cấu trúc Solution:** Là một dự án duy nhất (Single-project Monolith), không chia thành các thư viện class (`.csproj`) riêng biệt. Mọi thành phần (Data, Services, Controllers, Views) đều nằm trong project `ManagerStudentCaltholic.csproj`.

## 3. Các Module Chính (Xác minh từ Controllers & Services)
1. **Quản lý Hồ sơ & Lớp học (Student & Class Management):** 
   - *Bằng chứng:* `StudentsController.cs`, `ClassesController.cs`, `EnrollmentsController.cs`. Xử lý thêm, sửa, xóa, phân bổ lớp, import Excel.
2. **Quản lý Điểm danh & Chuyên cần (Attendance):** 
   - *Bằng chứng:* `AttendanceController.cs`, `RedisAttendanceService.cs`. Điểm danh qua quét QR, xử lý Offline-first, lưu cache Redis.
3. **Quản lý Học vụ & Sổ điểm (Academic & Grades):** 
   - *Bằng chứng:* `GradesController.cs`, `PromotionController.cs`, `GradeCalculationService.cs`. Nhập điểm, đánh giá xếp loại, kết chuyển năm học.
4. **Quản lý Sổ bộ Bí tích (Sacraments):** 
   - *Bằng chứng:* `SacramentsController.cs`, `SacramentRegistryService.cs`. Theo dõi tiến trình 4 mốc Bí tích, lập tờ trình xin Cha xứ phê duyệt.
5. **Chiến dịch Đi Lễ (Mass Campaigns):**
   - *Bằng chứng:* `MassCampaignsController.cs`, `MassCampaign.cs`. Theo dõi chuỗi đi lễ liên tục (Streak).
6. **Tương tác Phụ huynh & Cổng Tra cứu (Parent Communication & Internal Staff Lookup):**
   - *Bằng chứng:* `ParentMessagesController.cs`, `LookupController.cs`, `HomeController.cs`, `StaffStudentLookupService.cs`. Cổng tra cứu công khai cho phụ huynh, Cổng tra cứu 360 độ thiếu nhi nội bộ tại Trang chủ cho GLV/BĐH đã đăng nhập (`FEAT-P5-01`), tạo mẫu tin nhắn Zalo/Viber, đơn xin phép nghỉ (`AbsenceController.cs`).
7. **Quản trị Hệ thống & Phân quyền (Governance & System):**
   - *Bằng chứng:* `UsersController.cs`, `GovernanceController.cs`, `LogsController.cs`. Quản lý vai trò (Role), xem System Health (RAM, Database Latency).
8. **Giao diện Đa nền tảng & Mobile-First Web UI (Navigation & Responsive Shell):**
   - *Bằng chứng:* `HttpRequestExtensions.cs`, `_Layout.cshtml`, `_LayoutMobileHeader.cshtml`, `_LayoutMobileBottomNav.cshtml`, `mobile-layout.css`. Tự động nhận diện thiết bị di động ở phía Server (`Context.Request.IsMobileDevice()`) để chuyển sang giao diện Mobile-First với Thanh điều hướng đáy 5 phân khu (`FEAT-P5-02`), giữ nguyên giao diện Desktop trên màn hình lớn.

## 4. External Integrations
- **Redis Cache:** Sử dụng `StackExchange.Redis` để lưu trữ tạm thời dữ liệu điểm danh, tăng tốc độ quét QR, và cache hồ sơ tra cứu 360 độ thiếu nhi nội bộ (`staff_lookup:student_360:{studentId}`). (*Bằng chứng:* `RedisAttendanceService.cs`, `RedisCacheService.cs`, `HomeController.cs`, `appsettings.json`).
- **PostgreSQL Database:** Lưu trữ dữ liệu chính. (*Bằng chứng:* `ParishDbContext.cs`, `Npgsql.EntityFrameworkCore.PostgreSQL`).
- **Nginx (Reverse Proxy):** Xử lý Rate Limiting và chuyển tiếp request. (*Bằng chứng:* `nginx.conf`, `ServiceCollectionExtensions.cs`).
