# CODING RULES & CONVENTIONS

Dựa trên việc phân tích mã nguồn thực tế, các quy tắc và convention sau đang được áp dụng:

## 1. Naming Conventions
- **Class / Interface / Method / Property:** Sử dụng `PascalCase`. (VD: `RedisAttendanceService`, `CalculateSemesterAverage`, `StudentCode`).
- **Biến cục bộ & Tham số:** Sử dụng `camelCase`. (VD: `studentId`, `academicYearId`).
- **Private Fields (Dependency Injection):** Bắt đầu bằng dấu gạch dưới `_` kết hợp `camelCase`. (VD: `_context`, `_logger`, `_cache`).

## 2. Database Query & Entity Framework
- **Tối ưu hóa đọc:** Luôn sử dụng `.AsNoTracking()` cho các truy vấn chỉ để đọc (Read-only) nhằm tối ưu bộ nhớ. (VD: Rất phổ biến trong `DashboardController.cs`, `AttendanceController.cs`).
- **Soft Delete (Xóa mềm):** Áp dụng triệt để qua Interface `ISoftDelete`. Sử dụng Global Query Filter trong EF Core (`modelBuilder.Entity<T>().HasQueryFilter(e => !e.IsDeleted)`). Cấu hình ghi đè `SaveChangesAsync()` để tự động đổi lệnh Delete thành Update `IsDeleted = true`. (Xác minh: `ParishDbContext.cs`).
- **Bảo mật Transaction:** Khi thực thi lưu trữ nhiều thao tác/bảng cùng lúc, luôn bọc trong `IExecutionStrategy` và `BeginTransactionAsync()`. (VD: `EnrollmentsController.AssignStudents`, `PromotionController.ExecuteBatchCreate`).

## 3. Security & Validation
- **Authentication:** Phân quyền theo Role cứng bằng Attributes. (VD: `[Authorize(Roles = $"{UserRole.Admin},{UserRole.ExecutiveBoard}")]`).
- **Phòng chống CSRF:** Áp dụng `[ValidateAntiForgeryToken]` cho các phương thức `[HttpPost]`. Request Ajax yêu cầu truyền Header `X-CSRF-TOKEN`. (Xác minh: `SecurityExtensions.cs`, JS fetch calls).
- **Rate Limiting:** Sử dụng `[EnableRateLimiting(...)]` cho các API nhạy cảm (Đăng nhập, tra cứu công khai). Các rule được định nghĩa trong `SecurityExtensions.cs` (VD: `PolicyPublicLookup`).

## 4. Response Format
- **Ajax / API endpoints:** Chuẩn hóa cấu trúc trả về JSON dạng: `{ success: boolean, message: string, data: object }`. (VD: `UsersController.BatchCreateTeachers`).
- **View Data Routing:** Thường sử dụng `TempData["SuccessMessage"]` và `TempData["ErrorMessage"]` để đẩy thông báo lên UI sau các hành động redirect.