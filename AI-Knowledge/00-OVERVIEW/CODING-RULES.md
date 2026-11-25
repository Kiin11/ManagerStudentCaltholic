# CODING RULES & CONVENTIONS

Dựa trên việc phân tích mã nguồn thực tế, các quy tắc và convention sau đang được áp dụng:

## 1. Naming Conventions
- **Class / Interface / Method / Property:** Sử dụng `PascalCase`. (VD: `RedisAttendanceService`, `CalculateSemesterAverage`, `StudentCode`).
- **Biến cục bộ & Tham số:** Sử dụng `camelCase`. (VD: `studentId`, `academicYearId`).
- **Private Fields (Dependency Injection):** Bắt đầu bằng dấu gạch dưới `_` kết hợp `camelCase`. (VD: `_context`, `_logger`, `_cache`).

## 2. Database Query & Entity Framework
- **Tối ưu hóa đọc:** Luôn sử dụng `.AsNoTracking()` cho các truy vấn chỉ để đọc (Read-only) nhằm tối ưu bộ nhớ. (VD: Rất phổ biến trong `DashboardController.cs`, `AttendanceController.cs`).
- **Soft Delete (Xóa mềm) & Matching Query Filters:** Áp dụng triệt để qua Interface `ISoftDelete`. Sử dụng Global Query Filter trong EF Core (`modelBuilder.Entity<T>().HasQueryFilter(e => !e.IsDeleted)`). Cấu hình ghi đè `SaveChangesAsync()` để tự động đổi lệnh Delete thành Update `IsDeleted = true`. Mọi thực thể con có khóa ngoại bắt buộc (`Required`) trỏ tới thực thể dùng Soft Delete phải cấu hình Matching Query Filter tương ứng trong `ParishDbContext.OnModelCreating` (kể cả phụ thuộc bắc cầu) để đảm bảo tính nhất quán và tránh cảnh báo EF Core.
- **Query Splitting (Truy vấn đa tập hợp):** Hệ thống cấu hình `UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)` mặc định trên `UseNpgsql` (`ServiceCollectionExtensions.AddDatabaseConfiguration`). Khi viết truy vấn có từ 2 collection navigation trở lên qua `.Include()` / `.ThenInclude()`, bổ sung tường minh `.AsSplitQuery()` để tránh Cartesian explosion.
- **Bảo mật Transaction:** Khi thực thi lưu trữ nhiều thao tác/bảng cùng lúc, luôn bọc trong `IExecutionStrategy` và `BeginTransactionAsync()`. (VD: `EnrollmentsController.AssignStudents`, `PromotionController.ExecuteBatchCreate`).

## 3. Security & Validation
- **Authentication & Backend Scope (Anti-IDOR):** Phân quyền theo Role cứng bằng Attributes (`[Authorize(Roles = ...)]` / `[Authorize(Policy = "RequireStaff")]`), kết hợp kiểm tra lại quyền truy cập từng `classId`/`studentId`/`batchId` tại tầng Controller/Service (fail-closed nếu `BranchHead` thiếu `ManagedGradeLevel`, đối chiếu `ClassTeachers.AcademicYearId` hiện hành, và loại trừ lớp Ca Chiều với `ProbationaryTeacher`). Chặn tự điểm danh/tự hoàn tác/tự chấm điểm/tự duyệt đơn cho `LinkedStudentId` thông qua `StaffSelfActionGuard`.
- **Phòng chống CSRF:** Áp dụng `[ValidateAntiForgeryToken]` cho các phương thức `[HttpPost]`. Request Ajax yêu cầu truyền Header `X-CSRF-TOKEN`. (Xác minh: `SecurityExtensions.cs`, JS fetch calls).
- **Rate Limiting & Anti-Abuse:** Bật `GlobalLimiter` (`200 req/min/IP`, bypass `/health`, tài nguyên tĩnh và quét QR) và `[EnableRateLimiting(...)]` cho các API nhạy cảm (`PolicyAuth`, `PolicyPublicLookup`, `PolicyQrAttendance`); chuẩn hóa IP qua `SecurityExtensions.GetClientIpKey` + `ReverseProxy` cấu hình chặt; các endpoint công khai (`/TraCuu`, `/Absence`, `/PublicRegistration`) dùng Redis lockout qua `ICacheService`, `WebsiteUrlHoneypot` và thông báo lỗi chung chống enumeration.
- **Upload Security, XSS & Timezone:**
  - Tải tệp bắt buộc kiểm tra phần mở rộng allow-list, dung lượng, Magic Bytes (`FileUploadSecurityHelper.ValidateStreamMagicBytesAsync` / `ValidateFormFileMagicBytesAsync`), MIME phía server (`GetSafeMimeType`), tên lưu trữ GUID và đường dẫn nằm trong root (`TryResolveContainedPath`); phục vụ PDF nội bộ qua `Content-Security-Policy: sandbox` và ép `attachment` cho tệp public (`ABSENCE_DOC`).
  - Không dùng `@Html.Raw(...)` trực tiếp với chuỗi dữ liệu người dùng/DB trong JS hoặc attribute (dùng `JsonSerializer.Serialize` / `@Json.Serialize` / `data-*`) và không nội suy chuỗi thô vào `innerHTML` (dùng `createElement` + `textContent` hoặc `escapeHtml`).
  - Chuẩn hóa ngày nghiệp vụ và quy đổi múi giờ Việt Nam (UTC+7) qua `VietnamTimeHelper` (không dùng `DateTime.UtcNow.Date` hoặc JS `toISOString().split('T')[0]`), và kiểm tra tuổi `>= 18` chuẩn năm nhuận `29/02` qua `AgeValidationHelper`.
- **Tối ưu RAM (Low-Memory VM):** Ưu tiên trả `PhysicalFile(...)` hoặc `Stream` thay vì `File.ReadAllBytesAsync` / `MemoryStream.ToArray()`, đọc tail log giới hạn kích thước (`LogsController`), gom nhóm bằng SQL projection (`BackgroundExportJobService`) và xử lý transaction khóa sổ điểm (`ClassRoom.IsGradeLocked` + `AcademicYear.IsCurrent`) bên trong `CreateExecutionStrategy()`.

## 4. Response Format
- **Ajax / API endpoints:** Chuẩn hóa cấu trúc trả về JSON dạng: `{ success: boolean, message: string, data: object }`. (VD: `UsersController.BatchCreateTeachers`).
- **View Data Routing:** Thường sử dụng `TempData["SuccessMessage"]` và `TempData["ErrorMessage"]` để đẩy thông báo lên UI sau các hành động redirect.

## 5. Quy chuẩn SQL Scripts trong thư mục `script/` (PostgreSQL / pgAdmin)
- **Quy tắc đặt tên file:** Khi bổ sung script mới vào thư mục `script/`, tên file bắt buộc tuân thủ cấu trúc `{yyyyMMdd}_{TenBang_Hoac_ChucNang}.sql` (trong đó `{yyyyMMdd}` là ngày tạo script `datecreated`, theo sau là tên bảng hoặc tên chức năng; ví dụ: `20261120_Users_Classes_Students_Phase6.sql`).
- **Tương thích trực tiếp với pgAdmin (PostgreSQL):**
  - Toàn bộ script phải chạy trực tiếp được trong **pgAdmin Query Tool** trên hệ quản trị CSDL PostgreSQL.
  - Tuyệt đối không sử dụng các lệnh meta-command chỉ dành riêng cho `psql` CLI (như `\c`, `\i`, `\set`, `\echo`, `\copy`).
  - Khuyến nghị viết script theo hướng an toàn / idempotent (`ALTER TABLE ... ADD COLUMN IF NOT EXISTS`, `CREATE INDEX IF NOT EXISTS`, hoặc khối `DO $$ BEGIN ... END $$;`) và đặt tên bảng/cột phân biệt hoa thường trong dấu ngoặc kép `"..."` theo chuẩn EF Core Npgsql.