# FEATURE SPECIFICATION: CỔNG TRA CỨU THIẾU NHI NỘI BỘ TẠI HOMEPAGE (`FEAT-P5-01`)

> **Trạng thái:** NEW FILE (Bổ sung từ commit `c0e828a`)  
> **Module:** `Common` / `StaffLookup`  
> **Mức độ truy cập:** Nội bộ — Bắt buộc `[Authorize(Policy = "RequireStaff")]` (chặn hoàn toàn tài khoản chưa đăng nhập & vai trò `Parent`, kèm lọc phạm vi lớp/khối trong `StaffStudentLookupService` trước khi đọc/trả cache)

---

## 1. Mục đích Nghiệp vụ
Cho phép Giáo lý viên (GLV) và Ban Điều Hành (BĐH) đã đăng nhập tra cứu tức thời hồ sơ 360 độ của bất kỳ thiếu nhi nào ngay tại Trang chủ (`/Home/Index`) mà **không cần nhập Ngày sinh hoặc 4 số cuối SĐT phụ huynh** (khác với cổng công khai `/TraCuu`). Hỗ trợ tìm kiếm đa khóa (Họ tên, Tên Thánh, Mã hiện hành, hoặc Mã học viên của 2 năm học trước) và chuyển đổi nhanh giữa 4 bộ kết quả chuyên biệt.

---

## 2. Thành phần Source Code Tham gia

| Tầng | File Path | Symbol / Thành phần | Vai trò |
|---|---|---|---|
| **Entity** | `Models/Entities/LookupViewOption.cs` | `LookupViewOption` | Bảng `LookupViewOptions` cấu hình các tab chế độ xem kết quả |
| **Entity** | `Models/Entities/StaffQuickLookupLog.cs` | `StaffQuickLookupLog` | Bảng `StaffQuickLookupLogs` lưu lịch sử tra cứu của nhân sự |
| **DTO** | `Models/DTOs/StaffStudentLookupDtos.cs` | `StaffStudentQuickSearchResultDto`, `StaffStudent360DetailDto`, ... (14 DTOs) | Cấu trúc dữ liệu trả về cho gợi ý autocomplete và hồ sơ 360 độ |
| **Interface** | `Interface/Services/IStaffStudentLookupService.cs` | `IStaffStudentLookupService` | Hợp đồng dịch vụ tra cứu nội bộ (8 phương thức, gồm các overload có `ClaimsPrincipal caller` và `CanCallerAccessStudentAsync`) |
| **Service** | `Services/StaffStudentLookupService.cs` | `StaffStudentLookupService` | Truy vấn đa bảng với `.AsNoTracking()`, lọc phạm vi theo vai trò (`ResolveCallerScopeAsync`) và tổng hợp dữ liệu 360 độ |
| **Controller** | `Controllers/HomeController.cs` | `Index`, `StaffSearchSuggestions`, `StaffStudentLookup`, `StaffRecentLookups`, `StaffLookupOptions` | Endpoints gắn `[Authorize(Policy = "RequireStaff")]` và trả `403` nếu tra cứu ngoài phạm vi |
| **View** | `Views/Home/_StaffStudentLookupWidget.cshtml` | Partial View | Giao diện thanh tìm kiếm tức thời, chip lịch sử tra cứu, 4 tab kết quả |
| **Client JS** | `wwwroot/js/staff-student-lookup.js` | IIFE Module | Debounce search (280ms), điều hướng bàn phím, render 4 tab an toàn XSS (`escapeHtml` + delegated click listener thay inline `onclick`) |

---

## 3. Luồng Xử lý Chi tiết (End-to-End Flow)

### 3.1. Khởi tạo Widget tại Trang chủ (`GET /Home/Index`)
1. Khi người dùng đã xác thực (`User.Identity?.IsAuthenticated == true`) và không phải `UserRole.Parent`, `HomeController.Index` lấy `userId` từ `ClaimTypes.NameIdentifier`.
2. Gọi `_lookupService.GetRecentLookupsAsync(userId, User, 5)` (chỉ trả về các học sinh còn nằm trong phạm vi được phép của nhân sự) và `_lookupService.GetViewOptionsAsync()` để gán vào `MasterWorkspaceViewModel.RecentStaffLookups` và `MasterWorkspaceViewModel.LookupOptions`.
3. `Views/Home/Index.cshtml` render partial `_StaffStudentLookupWidget.cshtml` kèm các chip "Đã xem gần đây" và 4 nút Option (chỉ hiển thị khi đang ở phân khu Tab "Chung" `COMMON`, ẩn ở các tab còn lại như "Điểm Danh", "Học Vụ", "Bí Tích", "Quản Trị").

### 3.2. Gợi ý Tìm kiếm Đa khóa (`GET /Home/StaffSearchSuggestions?term=...`)
1. Người dùng gõ từ khóa (tối thiểu 1 ký tự, `term.length > 0`) vào ô `#staffStudentQuickSearchInput`.
2. `staff-student-lookup.js` debounce 280ms và gọi `GET /Home/StaffSearchSuggestions?term=...`.
3. `StaffStudentLookupService.SearchStudentsQuickAsync(term, User, limit = 8)`:
   - Gọi `ResolveCallerScopeAsync(User)`: Leadership xem toàn xứ; `BranchHead` lọc theo `ManagedGradeLevel` niên khóa hiện hành (fail-closed nếu rỗng); `Teacher` lọc theo `ClassTeachers` niên khóa hiện hành; `ProbationaryTeacher` chỉ xem lớp Ca Sáng được phân công (`!GradeLevel.IsAfternoonSession`).
   - Tìm trong bảng `Students` theo `StudentCode`, `FirstName`, `LastName`, `ChristianName`, hoặc chuỗi ghép không dấu/có dấu trong phạm vi được phép.
   - Tìm trong bảng `StudentCodeHistories` theo `OriginalCode`, `ArchivedYearCode` hoặc `NewCode` để phát hiện thiếu nhi được tìm bằng **mã năm cũ**.
   - Kết hợp thông tin lớp hiện hành từ `Enrollments` (thuộc niên khóa `IsCurrent = true`).
   - Trả về danh sách `StaffStudentQuickSearchResultDto` có cờ `IsHistoricalMatch` và `MatchedHistoricalCode`.

### 3.3. Tải Hồ sơ 360 Độ & Cache Redis (`GET /Home/StaffStudentLookup`)
1. Khi chọn một thiếu nhi từ danh sách gợi ý hoặc nhấn vào chip "Đã xem gần đây", client gọi:
   `GET /Home/StaffStudentLookup?studentId={id}&option={optionCode}&searchTerm={term}`
2. **Kiểm tra phạm vi & Tầng Cache (`HomeController.cs`):**
   - Bắt buộc gọi `_lookupService.CanCallerAccessStudentAsync(User, studentId)` **trước** khi truy xuất cache Redis; nếu ngoài phạm vi lớp/khối, trả ngay HTTP **`403 Forbidden`**.
   - Sử dụng khóa Redis `staff_lookup:student_360:{studentId}` thông qua `_cacheService.GetOrSetAsync(..., TimeSpan.FromMinutes(5))`.
3. **Tổng hợp Dữ liệu (`StaffStudentLookupService.GetStudent360DetailAsync`):**
   - **Thông tin cơ bản (`StudentBasicInfoDto`):** Hồ sơ `Students`, lớp hiện tại (`Enrollments` + `Classes` + `ClassTeachers`), và chuỗi mã học viên 2 năm trước (`StudentCodeHistories`).
   - **Chuyên cần & Chiến dịch (`AttendanceOverviewDto`):** Thống kê tỷ lệ đi lễ Chúa Nhật, học giáo lý, đi lễ Thứ Năm; trích xuất trạng thái **Chúa Nhật gần nhất** (`LastSundayAttendance`), **Thứ Năm gần nhất** (`LastThursdayAttendance`), 10 buổi điểm danh mới nhất, và tiến độ các chiến dịch đi lễ (`MassCampaigns` + `MassCampaignRecords`).
   - **Học vụ 2 năm & Bí tích (`AcademicHistoryDto` & `SacramentHistoryDto`):** Bảng điểm chi tiết năm hiện tại (`GradeRecords` + `GradeConfigurations`), điểm trung bình HK1/HK2/Cả năm (`CalculateWeightedAverage`), kết quả học tập 2 niên khóa liền trước (`PastYears`), và tiến trình 4 mốc Bí tích (`Baptism`, `FirstCommunion`, `Confirmation`, `SolemnCommunion` từ `StudentSacramentRecords` & `Students`).
   - **Liên hệ Phụ huynh (`ParentContactDto`):** SĐT phụ huynh, thông tin người giám hộ/đỡ đầu từ `StudentRegistrationRequests` (nếu có), và danh sách mẫu tin nhắn nhanh (`ParentMessageTemplates`) đã điền sẵn biến `{TenThieuNhi}`, `{TenThanh}`, `{Lop}`, `{MaHV}`, `{NgayHienTai}`.
4. **Ghi Audit Log (`RecordLookupLogAsync`):**
   - Lưu bản ghi vào `StaffQuickLookupLogs` (`UserId`, `StudentId`, `SearchTerm`, `SelectedOption`, `IpAddress`, `LookedUpAt`).
   - Nếu cùng `UserId` và `StudentId` đã tra cứu trong vòng 10 phút gần nhất, chỉ cập nhật lại `LookedUpAt`, `SelectedOption` và `SearchTerm` để tránh làm rác bảng log.

---

## 4. Cấu hình 4 Chế độ Xem (`LookupViewOptions` Seed Data)

| Id | OptionCode | DisplayName | IconClass | BadgeColor | SortOrder |
|---|---|---|---|---|---|
| 1 | `OVERVIEW` | Tổng quan | `bi-person-badge` | `primary` | 1 |
| 2 | `ATTENDANCE_DETAIL` | Chuyên cần & Chiến dịch | `bi-calendar-check` | `success` | 2 |
| 3 | `ACADEMIC_2Y_SACRAMENT` | Lịch sử 2 năm & Bí tích | `bi-journal-bookmark` | `warning` | 3 |
| 4 | `PARENT_CONTACT` | Liên hệ phụ huynh | `bi-telephone` | `info` | 4 |
