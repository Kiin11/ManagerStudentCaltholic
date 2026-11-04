# DANH MỤC YÊU CẦU HIỆU CHỈNH TÀI LIỆU KNOWLEDGE (REQUIRED-CORRECTIONS)

> **Căn cứ:** Báo cáo kiểm định độc lập `KNOWLEDGE-VALIDATION-REPORT.md` và danh sách lỗi `KNOWLEDGE-ISSUES.jsonl`  
> **Nguyên tắc:** Mọi chỉnh sửa dưới đây đều được đối chiếu 1-1 từ mã nguồn thực tế tại commit `c0e828a`. Chỉ sau khi hoàn tất các mục **Ưu tiên 1 (`CRITICAL`)** và **Ưu tiên 2 (`HIGH`)**, `KNOWLEDGE-BASELINE.json` mới được phép chốt trạng thái `"completed"`.

---

## Ưu tiên 1 — Khắc phục Lỗi `CRITICAL` (Bắt buộc sửa ngay)

### 1. Sửa `AI-Knowledge/05-FEATURES/FEAT-P5-01-STAFF-QUICK-LOOKUP.md` & `AI-Knowledge/00-OVERVIEW/ARCHITECTURE.md` (`AUD-001`, `AUD-002`, `AUD-003`)
- **Vị trí 1 (`ARCHITECTURE.md` -> Section 2 -> `Flow 4`, dòng 45–46):**
  - **Xóa bỏ:** Các bảng không được truy vấn gồm `ClassTeachers`, `StudentRegistrationRequests`, `ParentMessageTemplates`, và nhãn `([Authorize])` trên `HomeController`.
  - **Thay bằng:** Ghi rõ `HomeController` kiểm tra xác thực thủ công `User.Identity?.IsAuthenticated == true`; `StaffStudentLookupService.GetStudent360DetailAsync()` tiêm `IGradeCalculationService` và truy vấn các bảng `Students`, `AcademicYears`, `Enrollments` (kèm `Classes`, `GradeRecords`, `GradeConfigurations`), `Attendances`, `MassCampaigns`, `MassCampaignRecords`, `StudentCodeHistories`, `StudentSacramentRecords`, đồng thời tạo sẵn 4 mẫu tin nhắn nhanh trong bộ nhớ (`PopulateParentContact`).
- **Vị trí 2 (`FEAT-P5-01-STAFF-QUICK-LOOKUP.md` -> Section 3.3, dòng 43–51):**
  - **Mục Thông tin cơ bản:** Bỏ `+ ClassTeachers` và bỏ chi tiết "chuỗi mã học viên 2 năm trước" trong `StudentBasicInfoDto` (mã cũ chỉ hiển thị lúc gợi ý tìm kiếm ở `SearchStudentsQuickAsync` và dùng để nối kết quả năm cũ ở `PopulateAcademicHistoryAsync`).
  - **Mục Học vụ 2 năm & Bí tích:** Sửa tên hàm tính điểm thành `_gradeCalcService.CalculateSemesterAverage(scoreDict, configs)` (thuộc `IGradeCalculationService`), sửa tên thuộc tính thành `CurrentYearAverage`, `PastTwoYears`, và mốc thứ 4 là `SolemnProfession` (`Bao Đồng`).
  - **Mục Liên hệ Phụ huynh:** Sửa lại đúng theo `PopulateParentContact` (`Services/StaffStudentLookupService.cs` dòng `512–555`): lấy `Student.ParentPhone`, chuẩn hóa link `tel:`, `sms:`, `https://zalo.me/...`, gán địa chỉ mặc định Giáo xứ Phú Trung và sinh trực tiếp 4 mẫu `ParentMessageQuickTemplateDto` (`ATTENDANCE`, `REMINDER`, `SACRAMENT`, `ENCOURAGE`) trong bộ nhớ — **không** truy vấn `StudentRegistrationRequests` hay `ParentMessageTemplates`.
  - **Mục Ghi Audit Log (`RecordLookupLogAsync`):** Xóa hoàn toàn câu *"Nếu cùng UserId và StudentId đã tra cứu trong vòng 10 phút gần nhất, chỉ cập nhật lại LookedUpAt..."*. Ghi đúng hành vi dòng `557–577`: luôn thêm bản ghi mới vào `StaffQuickLookupLogs` (cắt `SearchTerm` tối đa 150 ký tự, bắt ngoại lệ an toàn không làm gián đoạn tra cứu), và việc khử trùng lặp theo `StudentId` được thực hiện lúc đọc tại `GetRecentLookupsAsync` (`.GroupBy(l => l.StudentId)`).

### 2. Bổ sung Dependency `IGradeCalculationService` vào `DEPENDENCY-INDEX.jsonl` và `UPDATE-REPORT.md` (`AUD-003`)
- **Vị trí (`AI-Knowledge/03-CODE-INDEX/DEPENDENCY-INDEX.jsonl`):**
  - Thêm dòng `INJECTS`:
    `{"sourceSymbol": "ManagerStudentCaltholic.Services.StaffStudentLookupService", "targetSymbol": "ManagerStudentCaltholic.Interface.Services.IGradeCalculationService", "targetName": "IGradeCalculationService", "relationshipType": "INJECTS", "evidenceType": "SYNTAX", "confidence": "high", "sourceFile": "Services/StaffStudentLookupService.cs", "sourceLine": 17}`
  - Thêm dòng `CALLS`:
    `{"sourceSymbol": "ManagerStudentCaltholic.Services.StaffStudentLookupService.GetStudent360DetailAsync", "targetSymbol": "ManagerStudentCaltholic.Interface.Services.IGradeCalculationService.CalculateSemesterAverage", "targetName": "CalculateSemesterAverage", "relationshipType": "CALLS", "evidenceType": "SYNTAX", "confidence": "high", "sourceFile": "Services/StaffStudentLookupService.cs", "sourceLine": 369}`
- **Vị trí (`UPDATE-REPORT.md` & `AI-Knowledge/06-UPDATE-LOG/UPDATE-REPORT.md` -> Section 5):**
  - Bổ sung `IGradeCalculationService` vào danh sách service được inject vào `StaffStudentLookupService`.

---

## Ưu tiên 2 — Khắc phục Lỗi `HIGH` (Cần sửa trước khi chốt Baseline)

### 3. Hiệu chỉnh Hợp đồng Bảo mật & Bổ sung Routes của `HomeController` (`AUD-004`)
- **Vị trí 1 (`FEAT-P5-01-STAFF-QUICK-LOOKUP.md` dòng 5 & Section 3):**
  - Ghi rõ: `HomeController` **không sử dụng attribute `[Authorize]`**; 3 action `StaffSearchSuggestions`, `StaffStudentLookup`, `StaffRecentLookups` kiểm tra thủ công `if (User.Identity?.IsAuthenticated != true) return Unauthorized(...)` (cho phép mọi tài khoản đã đăng nhập, chưa giới hạn theo `UserRole`), còn `GET /Home/StaffLookupOptions` hiện không kiểm tra xác thực.
- **Vị trí 2 (`AI-Knowledge/03-CODE-INDEX/ROUTE-INDEX.jsonl` dòng 31–35):**
  - Cập nhật lại `startLine` chính xác cho 5 route của `HomeController`:
    - `Index`: `startLine: 29`
    - `StaffSearchSuggestions`: `startLine: 52`
    - `StaffStudentLookup`: `startLine: 67`
    - `StaffRecentLookups`: `startLine: 109`
    - `StaffLookupOptions`: `startLine: 124`
  - Bổ sung 3 route còn thiếu của `HomeController.cs`:
    - `POST /Home/DetachRememberedStudent` (`action: "DetachRememberedStudent"`, `startLine: 132`)
    - `GET /Home/Privacy` (`action: "Privacy"`, `startLine: 143`)
    - `GET /Home/Error` (`action: "Error"`, `startLine: 149`)

### 4. Hiệu chỉnh Đặc tả `AI-Knowledge/05-FEATURES/FEAT-P5-02-MOBILE-FIRST-UI.md` (`AUD-005`, `AUD-013`)
- **Section 4.1 (`_LayoutMobileHeader.cshtml` & `mobile-layout.css`):**
  - Sửa chiều cao Header từ `54px` thành `56px` (`--mobile-header-height: 56px`).
  - Sửa tên phần tử Offcanvas từ `#mobileUserOffcanvas` (`z-index: 1085`) thành `#mobileMenuOffcanvas` (`z-index: 1070`, backdrop `z-index: 1065`, footer `z-index: 1075`).
  - Ghi rõ nút góc phải Header hiển thị **Quét QR** khi `isRecognizedStaffDevice` (`isAuthenticated || Cookie "Parish_Staff_Device"`), ngược lại hiển thị nút **Tra Cứu** (`/Lookup/Index`) cho phụ huynh.
- **Section 4.2 (`_LayoutMobileBottomNav.cshtml`):**
  - **Xóa bỏ** mô tả về `.mobile-subnav-strip` (không tồn tại trong code).
  - Sửa chiều cao Bottom Nav thành `--mobile-bottom-nav-height: 62px` (+ `max(6px, env(safe-area-inset-bottom))`).
  - Sửa đúng thứ tự 5 vị trí điều hướng thực tế:
    1. **Chung** (`/Home/Index?master=COMMON`)
    2. **Quét QR** (`/Attendance/ScanQr` với `.qr-highlight .qr-circle`, nếu `isStaff`) **hoặc Điểm Danh** (`/Home/Index?master=ATTENDANCE`, nếu không phải `isStaff`)
    3. **Học Vụ** (`/Home/Index?master=ACADEMIC`)
    4. **Bí Tích** (`/Home/Index?master=SACRAMENT`)
    5. **Quản Trị** (`/Home/Index?master=GOVERNANCE` nếu `canViewGovernance`), hoặc **Cá Nhân** (`/Account/Profile` nếu `isAuthenticated`), hoặc **Tra Cứu** (`/Lookup/Index` nếu chưa đăng nhập).
- **Section 4.3 (Performance Optimizations):**
  - Bổ sung tối ưu trên `_Layout.cshtml` dòng `47–52`: không tải Google Fonts (`Plus Jakarta Sans`) khi `isMobile == true`.
  - Sửa mô tả `scroll-floating-controls`: khối HTML nút cuộn chỉ render khi `!isMobile`, còn hàm JS `initScrollButtons()` tự thoát sớm khi không tìm thấy nút và dùng `{ passive: true }` + `requestAnimationFrame` trên Desktop.
  - Sửa mô tả `pwa-installer.js`: bỏ câu *"Chỉ gọi `reg.update()` trên trang chủ"* (không có trong code), giữ đúng cơ chế chặn reload tự động trong sự kiện `controllerchange` bằng cờ `window.__pwaUserRequestedUpdate`.
  - Bổ sung ghi chú nhỏ (Technical Debt) ở Section 3 về XML comment cũ `Cookie Parish_Device_Mode` tại `HttpRequestExtensions.cs` dòng 26 và class `.device-switch-banner` tại `mobile-layout.css` dòng 287.

### 5. Sửa Chữ ký Hàm, Số dòng và Bổ sung 12 DTO trong `SYMBOL-INDEX.jsonl` (`AUD-006`)
- **Vị trí (`AI-Knowledge/03-CODE-INDEX/SYMBOL-INDEX.jsonl` dòng 63–80):**
  - `HttpRequestExtensions`: `startLine: 10, endLine: 74`; `IsMobileDevice`: `startLine: 30, endLine: 73`.
  - `IStaffStudentLookupService`: `startLine: 8, endLine: 15`.
  - `StaffStudentLookupService`: `startLine: 14, endLine: 713`.
  - `GetViewOptionsAsync`: `startLine: 30, endLine: 51`.
  - `SearchStudentsQuickAsync`: sửa signature thành `"public async Task<List<StaffStudentQuickSearchResultDto>> SearchStudentsQuickAsync(string keyword, int limit = 8)"`, `startLine: 53, endLine: 149`.
  - `GetStudent360DetailAsync`: sửa signature thành `"public async Task<StaffStudent360DetailDto?> GetStudent360DetailAsync(long studentId, string selectedOption = \"OVERVIEW\")"`, `startLine: 151, endLine: 217`.
  - `RecordLookupLogAsync`: `startLine: 557, endLine: 577`.
  - `GetRecentLookupsAsync`: `startLine: 579, endLine: 616`.
  - `LookupViewOption`: `startLine: 7, endLine: 34`; `StaffQuickLookupLog`: `startLine: 7, endLine: 35`.
  - `StaffStudentQuickSearchResultDto`: `startLine: 6, endLine: 18`; `StaffStudent360DetailDto`: `startLine: 20, endLine: 28` + bổ sung đủ 12 class DTO còn lại trong `Models/DTOs/StaffStudentLookupDtos.cs` (dòng `30–171`) để khớp với mảng `symbols` của `FILE-INDEX.jsonl`.
  - `HomeController`: `startLine: 9, endLine: 153`.
  - `StaffSearchSuggestions`: bỏ `[FromQuery]`, `startLine: 52, endLine: 62`.
  - `StaffStudentLookup`: bỏ `[FromQuery]`, `startLine: 67, endLine: 105`.
  - `StaffRecentLookups`: `startLine: 109, endLine: 120`.
  - `StaffLookupOptions`: `startLine: 124, endLine: 128`.

### 6. Sửa Dữ liệu Seed, Tên Thuộc tính DTO & Chi tiết JS trong `FEAT-P5-01-STAFF-QUICK-LOOKUP.md` (`AUD-007`, `AUD-014`)
- **Section 4 (Bảng `LookupViewOptions` Seed Data):** Sửa lại đúng theo `Data/ParishDbContext.cs` dòng `734–778`:
  - Id 1 (`OVERVIEW`): `DisplayName` = `Tổng quan`, `IconClass` = `bi-person-badge`
  - Id 2 (`ATTENDANCE_DETAIL`): `DisplayName` = `Chuyên cần & Chiến dịch`, `IconClass` = `bi-calendar-check`
  - Id 3 (`ACADEMIC_2Y_SACRAMENT`): `DisplayName` = `Lịch sử 2 năm & Bí tích`, `IconClass` = `bi-journal-bookmark`
  - Id 4 (`PARENT_CONTACT`): `DisplayName` = `Liên hệ phụ huynh`, `IconClass` = `bi-telephone`
- **Section 2 & 3.2 (`staff-student-lookup.js` & `SearchStudentsQuickAsync`):**
  - Sửa ô input thành `#staffStudentQuickSearchInput`, kích hoạt từ `1` ký tự (`term.length > 0`), debounce `280ms`, lưu bộ nhớ đệm phía client bằng `student360ClientCache` (`Map`), bỏ chi tiết "IIFE Module" và "điều hướng bàn phím".
  - Sửa tên cột bảng `StudentCodeHistories` thành `OriginalCode`, `ArchivedYearCode`, `NewCode` (kèm lọc không dấu bằng `VietnameseStringHelper.RemoveDiacritics`), và cờ DTO thành `IsHistoricalMatch`, `MatchedHistoricalCode`.
  - Ghi chú `HomeController.Index` gọi `GetRecentLookupsAsync(userId, 5)`, còn `GET /Home/StaffRecentLookups` gọi `GetRecentLookupsAsync(userId, 6)`.

### 7. Đồng bộ `StudentCodeHistories` vào `TABLE-INDEX.json` & Xóa Khối JSON Trùng lặp trong `TABLE-RELATIONSHIPS.md` (`AUD-008`)
- **Vị trí 1 (`AI-Knowledge/02-DATABASE/TABLE-INDEX.json`):**
  - Bổ sung định nghĩa bảng `StudentCodeHistories` (cột: `Id`, `StudentId`, `SourceAcademicYearId`, `SourceClassName`, `OriginalCode`, `ArchivedYearCode`, `NewCode`, `RolloverOutcome`, `ChangedAt`, `ChangedBy`; FK tới `Students.Id` và `AcademicYears.Id`; indexes: `["OriginalCode", "ArchivedYearCode", "NewCode", "StudentId, SourceAcademicYearId"]`) và bổ sung `MassCampaigns`, `MassCampaignRecords` đang bị lệch với `TABLE-RELATIONSHIPS.md`.
- **Vị trí 2 (`AI-Knowledge/02-DATABASE/TABLE-RELATIONSHIPS.md` dòng `50–265`):**
  - Gỡ bỏ khối `### 3. TABLE-INDEX.json` nhúng trùng lặp ở cuối file Markdown, thay bằng liên kết tham chiếu sang file chuẩn `02-DATABASE/TABLE-INDEX.json` theo đúng quy định tại `AI-KNOWLEDGE-STRUCTURE.md` Section 5 & Section 6.
- **Vị trí 3 (`AI-Knowledge/02-DATABASE/DATABASE-OVERVIEW.md`):**
  - Bổ sung bảng thứ 41 `ClassLessonPlan` (`ClassLessonPlans` — cấu hình Fluent API tại `ParishDbContext.cs` dòng `318–349`).

---

## Ưu tiên 3 — Khắc phục Lỗi `MEDIUM` & `LOW` (Hoàn thiện tính nhất quán toàn cục)

1. **Chuẩn hóa Khóa Tham chiếu trong `DEPENDENCY-INDEX.jsonl` (`AUD-009`):**
   - Bổ sung symbol cho `IFeatureNavigationService`, `IDeviceStudentRecognitionService`, và các method của `IStaffStudentLookupService` vào `SYMBOL-INDEX.jsonl` (hoặc trỏ `CALLS` tới symbol tương ứng đã khai báo), đồng thời sửa lại `sourceLine` trong `DEPENDENCY-INDEX.jsonl` (`16`, `11..14`, `81`, `96`, `60`).
2. **Cập nhật `INTEGRATION-OVERVIEW.md` và `FILE-INDEX.jsonl` (`AUD-010`):**
   - Trong `AI-Knowledge/04-INTEGRATIONS/INTEGRATION-OVERVIEW.md` (Section 1 - Redis Cache): bổ sung mục lưu bộ đệm hồ sơ 360 độ thiếu nhi nội bộ (`staff_lookup:student_360:{studentId}`, TTL 5 phút thông qua `ICacheService` / `RedisCacheService`).
   - Trong `AI-Knowledge/03-CODE-INDEX/FILE-INDEX.jsonl`: bổ sung 6 file `Modified` còn thiếu (`Extensions/ServiceCollectionExtensions.cs`, `Models/ViewModels/MasterNavigationViewModels.cs`, `Views/Home/Index.cshtml`, `Views/Shared/_Layout.cshtml`, `Views/_ViewImports.cshtml`, `wwwroot/js/pwa-installer.js`).
   - Lên kế hoạch dọn dẹp các tên module mẫu sai (`Hotel`, `Booking`, `PaymentPaymentModule`) trong `MODULE-INVENTORY.json` và `SOURCE-INVENTORY.md`.
3. **Gắn nhãn `NEEDS_VERIFICATION` trong `DATABASE-OVERVIEW.md` & Quản lý `KNOWLEDGE-BASELINE.json` (`AUD-011`, `AUD-012`):**
   - Tại `DATABASE-OVERVIEW.md` (2 dòng `StaffQuickLookupLog` và `LookupViewOption`): đổi cột `Mapping Type` thành `ORM Confirmed (DB Migration: NEEDS_VERIFICATION)` do file migration đã bị gỡ ở commit `0f5331a`.
   - Trong `AI-Knowledge/06-UPDATE-LOG/KNOWLEDGE-BASELINE.json`: chuyển `"status"` thành `"pending_correction"` cho tới khi hoàn tất sửa các mục **Ưu tiên 1** và **Ưu tiên 2** nêu trên.
