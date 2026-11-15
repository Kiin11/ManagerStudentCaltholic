# PENDING KNOWLEDGE UPDATES

## 1. Trạng thái Đồng bộ Hiện tại
- Toàn bộ thay đổi mã nguồn từ commit `c0e828a` đến commit `2a3263d` (kèm cấu hình working tree ngày `2026-11-15`) **đã được phân tích và đồng bộ** vào kho tri thức `AI-Knowledge/`.

## 2. Các Điểm Chưa Xác Minh (`NEEDS_VERIFICATION`) & Hạng mục Chờ Triển khai
1. **Xác minh Migration / DDL trên PostgreSQL cho `FEAT-P5-07` (`NEEDS_VERIFICATION`):**
   - `ParishDbContext.cs` đã khai báo `DbSet` và cấu hình Fluent API cho `ClassCurriculumDocuments`, `ClassCurriculumVersions` và các cột liên kết trên `ClassLessonPlans` (`CurriculumDocumentId`, `WeekNumber`, `Semester`).
   - Tuy nhiên thư mục `ManagerStudentCaltholic/Migrations/` hiện rỗng (`0` file migration trong repository). Cần kiểm tra môi trường PostgreSQL đích đã có các bảng/cột này chưa trước khi kích hoạt `WebApplicationExtensions.SeedSampleCurriculumData` hoặc các tính năng của `LessonPlansController`.
2. **Xác minh các tệp Untracked ngoài Git `HEAD` (`NEEDS_VERIFICATION`):**
   - `nginx/default.conf` (được mount trong `docker-compose.yml:44`) và `script/seed-curriculum-data.sql` hiện tồn tại trong working tree ở trạng thái untracked. Cần rà soát và quyết định commit hoặc tách khỏi phạm vi triển khai.
3. **Xác minh Schema `StaffQuickLookupLogs` & `LookupViewOptions` trên Database thực tế (`NEEDS_VERIFICATION`):**
   - Hai bảng này đã được khai báo trong `ParishDbContext.cs`, nhưng file EF Migration `20260410164552_AddStaffQuickLookupTablesAndIndexes.cs` đã bị xóa từ commit `0f5331a`. Cần đảm bảo môi trường tích hợp/production đã thực thi DDL tương ứng.
4. **`FEAT-P5-02` (`TASK-P5-02.2`, `TASK-P5-02.3`, `TASK-P5-02.4`) & `UserHomepageWidgetConfigs` (`NEEDS_VERIFICATION`):**
   - Entity `UserHomepageWidgetConfigs` được nhắc trong đặc tả ban đầu của `FEAT-P5-02` nhưng chưa xuất hiện trong source code tính đến commit `2a3263d`.
   - Một số màn hình Mobile đã được tinh chỉnh (`Attendance/Index.cshtml`, `Lookup/Result.cshtml`, `PublicRegistration/*`, `Users/Index.cshtml`, `Account/Profile.cshtml`), nhưng phạm vi chuyển đổi toàn bộ các bảng dữ liệu còn lại sang Card View / Bottom Sheet cần đối chiếu tiếp với backlog `FEAT-P5-02`.
