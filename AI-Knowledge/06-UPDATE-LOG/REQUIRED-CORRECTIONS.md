# HƯỚNG DẪN KHẮC PHỤC CÁC VẤN ĐỀ KHO TRI THỨC (REQUIRED CORRECTIONS)

> **Tài liệu liên quan:** [`KNOWLEDGE-VALIDATION-REPORT.md`](file:///d:/github/ManagerStudentCaltholic/AI-Knowledge/06-UPDATE-LOG/KNOWLEDGE-VALIDATION-REPORT.md), [`KNOWLEDGE-ISSUES.jsonl`](file:///d:/github/ManagerStudentCaltholic/AI-Knowledge/06-UPDATE-LOG/KNOWLEDGE-ISSUES.jsonl)  
> **Lưu ý:** Tài liệu này liệt kê chính xác vị trí và nội dung cần chỉnh sửa cho đợt vá tài liệu tiếp theo. Chưa tự động sửa các file này trong bước audit độc lập theo đúng nguyên tắc kiểm định.

---

## Priority 1 — Mức Độ `HIGH` (Cần xử lý trước tiên)

### 1. [`ISS-AUD-001`] Loại bỏ tên miền giả lập (`Booking`, `Payment`, `Hotel`) & cập nhật kiểm kê trong `00-OVERVIEW/`
- **Vị trí cần sửa:**
  - `AI-Knowledge/00-OVERVIEW/MODULE-INVENTORY.json` (dòng `15-37`)
  - `AI-Knowledge/00-OVERVIEW/SOURCE-INVENTORY.md` (dòng `6-7, 21, 23, 24, 32`)
  - `AI-Knowledge/00-OVERVIEW/PROJECT-INVENTORY.json` (dòng `80, 104`)
- **Sai lệch hiện tại:**
  - `MODULE-INVENTORY.json` định nghĩa `"BookingEnrollmentModule"` (`module: "Booking"`) và `"PaymentPaymentModule"` (`module: "Payment"`).
  - `SOURCE-INVENTORY.md` và `PROJECT-INVENTORY.json` gán các Controller thực tế (`ClassesController`, `EnrollmentsController`, `FacilitiesController`, `MassCampaignsController`) vào các domain không tồn tại (`"Hotel / Class Management"`, `"Booking / Enrollment"`, `"Hotel / Facilities"`, `"Booking / MassCampaigns"`, `"Class / Hotel"`).
  - Cả 3 tệp chưa bao gồm toàn bộ `28` Controllers và các nhóm tệp mới (`PublicRegistrationController`, `PwaController`, `SacramentsController`, `SacramentApprovalsController`, `StudentsController`, `StaffStudentLookupService`, `ClassCurriculumService`).
- **Hành động yêu cầu:**
  1. Trong `MODULE-INVENTORY.json`, thay thế hoàn toàn module `Booking`/`Payment` bằng danh mục module thực tế khớp với `AI-KNOWLEDGE-STRUCTURE.md` & `PROJECT.md` (`Student & Class`, `Attendance`, `Academic / Grades / LessonPlans`, `Sacraments`, `MassCampaigns`, `PublicLookup / PublicRegistration`, `UserAccount & Governance`, `PWA & Common`).
  2. Trong `SOURCE-INVENTORY.md` và `PROJECT-INVENTORY.json`, chuẩn hóa cột `module` về đúng tên miền thực tế (`Class`, `Enrollment`, `Facilities`, `MassCampaigns`) và cập nhật ngày/số lượng kiểm kê khớp hiện trạng (`28` Controllers).

### 2. [`ISS-AUD-002`] Xử lý mâu thuẫn Migration trong `UPDATE-LOG.md` & xác minh DDL PostgreSQL (`NEEDS_VERIFICATION`)
- **Vị trí cần sửa trong tài liệu:**
  - `AI-Knowledge/06-UPDATE-LOG/UPDATE-LOG.md` (dòng `95` trong khối lịch sử `05/11/2026 - FEAT-P5-07`)
  - `AI-Knowledge/02-DATABASE/DATABASE-OVERVIEW.md` (dòng `5`)
- **Sai lệch hiện tại:**
  - `UPDATE-LOG.md:95` ghi: `Cấu hình Fluent API và tạo EF Migration AddClassCurriculumDocumentsAndVersions.`
  - `DATABASE-OVERVIEW.md:5` ghi: `Phương pháp tiếp cận: Code-First Migrations.` (nhưng `ManagerStudentCaltholic/Migrations/` hiện có **`0` file**).
- **Hành động yêu cầu:**
  1. Cập nhật `UPDATE-LOG.md:95` thêm chú thích `[NEEDS_VERIFICATION - Tệp migration hiện không còn trong ManagerStudentCaltholic/Migrations/]` để đồng nhất với dòng `11` của cùng file và `UPDATE-REPORT.md:86`.
  2. Bổ sung ngay tại `DATABASE-OVERVIEW.md:5` ghi chú rằng repository hiện chưa lưu tệp migration nào trong `Migrations/` nên quy trình đồng bộ DDL thực tế cần Developer xác nhận.
  3. **Hành động cho Developer (Ngoài scope sửa tài liệu):**
     - Kiểm tra trên PostgreSQL sự tồn tại các bảng `ClassCurriculumDocuments`, `ClassCurriculumVersions`, `StaffQuickLookupLogs`, `LookupViewOptions` và các cột `CurriculumDocumentId`, `WeekNumber`, `Semester` trên `ClassLessonPlans`.
     - Quyết định tạo lại EF Core Migration baseline vào `ManagerStudentCaltholic/Migrations/` hoặc ghi nhận SQL script quản lý schema chính thức.

---

## Priority 2 — Mức Độ `MEDIUM` (Cần đồng bộ để tránh hiểu sai luồng & index)

### 3. [`ISS-AUD-003`] Đồng bộ tập bảng giữa `TABLE-INDEX.json`, `TABLE-RELATIONSHIPS.md` và `DATABASE-OVERVIEW.md`
- **Vị trí cần sửa:**
  - `AI-Knowledge/02-DATABASE/TABLE-INDEX.json` (hiện `23` bảng)
  - `AI-Knowledge/02-DATABASE/TABLE-RELATIONSHIPS.md` (dòng `69-276`, khối `### 3. TABLE-INDEX.json` hiện `20` bảng)
- **Hành động yêu cầu:**
  1. Khôi phục `5` bảng đang thiếu trong JSON của `TABLE-RELATIONSHIPS.md` nhưng đã có trong `TABLE-INDEX.json`: `Announcements`, `BuildingZones`, `ClassRoomLocations`, `ClassSchedules`, `RoomIncidentReports`.
  2. Bổ sung `2` bảng `MassCampaigns`, `MassCampaignRecords` vào `TABLE-INDEX.json` (hiện chỉ có ở JSON nhúng của `TABLE-RELATIONSHIPS.md`).
  3. Mở rộng `TABLE-INDEX.json` (và/hoặc loại bỏ phần JSON lặp trong `TABLE-RELATIONSHIPS.md` nếu giữ single source of truth) để bao phủ thêm `20` bảng còn lại đã liệt kê trong `DATABASE-OVERVIEW.md` (`StudentCodeHistories`, `StudentRegistrationRequests`, `GradeColumns`, `StudentGrades`, `AttendanceScoreRules`, `GradingFormulas`, `StudentGradeSummaries`, `TeacherClasses`, `ParentMessageTemplates`, `PromotionRuleConfigs`, `SacramentApprovalBatches`, `SacramentApprovalItems`, `BranchHeadAssignments`, `UserRoleHistories`, `ClassLessonDocuments`, `AbsenceRequests`, `AttendanceRuleConfigs`, `FeaturePermissionRules`, v.v.).

### 4. [`ISS-AUD-004`] Hiệu chỉnh số dòng, chữ ký & bổ sung Symbol/Dependency còn thiếu trong `03-CODE-INDEX/`
- **Vị trí cần sửa:**
  - `AI-Knowledge/03-CODE-INDEX/SYMBOL-INDEX.jsonl` (dòng `63-80` + thêm định nghĩa còn thiếu)
  - `AI-Knowledge/03-CODE-INDEX/ROUTE-INDEX.jsonl` (dòng `31-35`)
  - `AI-Knowledge/03-CODE-INDEX/DEPENDENCY-INDEX.jsonl` (dòng `43-49` + bổ sung cạnh mới)
- **Giá trị chuẩn từ source code cần sửa vào:**
  1. `Controllers/HomeController.cs` (tổng `154` dòng):
     - Class `HomeController`: `startLine: 9, endLine: 153`.
     - `Index`: `startLine: 29, endLine: 48` (route `startLine: 28` hoặc `29`).
     - `StaffSearchSuggestions`: `signature: "public async Task<IActionResult> StaffSearchSuggestions(string? term)"` (bỏ `[FromQuery]`), `startLine: 52, endLine: 62`.
     - `StaffStudentLookup`: `signature: "public async Task<IActionResult> StaffStudentLookup(long studentId, string? option = \"OVERVIEW\", string? searchTerm = null)"` (bỏ `[FromQuery]`), `startLine: 67, endLine: 105`.
     - `StaffRecentLookups`: `startLine: 109, endLine: 120`.
     - `StaffLookupOptions`: `startLine: 124, endLine: 128`.
     - `DEPENDENCY-INDEX.jsonl`: sửa `sourceLine` của `HomeController` DI thành `16` (constructor) hoặc `22-25`; sửa `StaffSearchSuggestions -> SearchStudentsQuickAsync` thành `60`, `StaffStudentLookup -> GetStudent360DetailAsync` thành `81`, `StaffStudentLookup -> RecordLookupLogAsync` thành `96`.
  2. `Interface/Services/IStaffStudentLookupService.cs`: `startLine: 8, endLine: 15` (cũng kiểm tra chữ ký `SearchStudentsQuickAsync(string keyword, int limit = 8)`).
  3. `Services/StaffStudentLookupService.cs`: `endLine: 714`; chỉnh lại `startLine`/`endLine` các method; thêm cạnh `INJECTS` cho `StaffStudentLookupService -> IGradeCalculationService` tại `sourceLine: 20` (hoặc `22`).
  4. `Extensions/HttpRequestExtensions.cs`: `class` `startLine: 10, endLine: 74`; `IsMobileDevice` `startLine: 30, endLine: 73`.
  5. Bổ sung vào `SYMBOL-INDEX.jsonl` bản ghi `Class`/`Interface`/`Method` cho `22` `symbolKey` đang được tham chiếu trong `DEPENDENCY-INDEX.jsonl` nhưng chưa có trong `SYMBOL-INDEX.jsonl` (ví dụ: `RedisAttendanceService`, `AttendanceDisputeService`, `RedisCacheService`, `TokenService`, `AcademicYearsController`, `IFeatureNavigationService`, `IDeviceStudentRecognitionService`, `IClassEvaluationService`, `IMassCampaignService`, `AbsenceRequest`, `AttendanceTimeHelper.EvaluateCheckIn`, các method của `IClassCurriculumService` & `IStaffStudentLookupService`).

### 5. [`ISS-AUD-005`] Cập nhật Dependency & Cơ chế Write-Through của `SaveBatch` trong `01-MODULES/ATTENDANCE.md`
- **Vị trí cần sửa:** `AI-Knowledge/01-MODULES/ATTENDANCE.md` (dòng `240` và dòng `280`).
- **Nội dung sửa:**
  1. Dòng `240` (Section 6 `Dependencies`): Đổi `"Gọi IStudentCodeRolloverService (để xử lý mã QR cũ của năm ngoái)"` thành:
     `"Truy vấn trực tiếp bảng StudentCodeHistories thông qua ParishDbContext (trong RedisAttendanceService.ProcessQrCheckInViaRedisAsync và AttendanceController.GetOfflineStudentsCache) để nhận diện thẻ mã năm cũ; IStudentCodeRolloverService được dùng bởi AttendanceDisputeService và LookupController."`
  2. Dòng `280` (Section 9 `Risk Areas`): Loại bỏ nhận định lỗi thời rằng `SaveBatchToRedisAsync` chỉ để `IsDirty=true` chờ 10 phút dễ mất dữ liệu. Ghi nhận chính xác rằng `AttendanceController.SaveBatch` (`P2-LOG-16 FIX`, dòng `147-151`) thực hiện **Write-Through tức thì** xuống PostgreSQL qua `SyncDirtyAttendancesToDatabaseAsync(request.AttendanceDate)` ngay trong cùng HTTP request; chỉ có trường hợp ghi Redis gặp ngoại lệ hoặc luồng mất mạng cục bộ mới dựa vào cơ chế dự phòng khác.

### 6. [`ISS-AUD-006`] Sửa tên DOM ID, Debounce, Cột DB & Thuộc tính DTO trong `FEAT-P5-01`
- **Vị trí cần sửa:** `AI-Knowledge/05-FEATURES/FEAT-P5-01-STAFF-QUICK-LOOKUP.md` (dòng `25, 37-38, 41, 43`).
- **Nội dung sửa theo code (`staff-student-lookup.js`, `StudentCodeHistory.cs`, `StaffStudentLookupDtos.cs`):**
  - Đổi `#staffLookupSearchInput` -> `#staffStudentQuickSearchInput`.
  - Đổi `debounce 250ms` -> `debounce 280ms`, bỏ điều kiện `"tối thiểu 2 ký tự"` (thực tế chỉ kiểm tra `term.length === 0`).
  - Đổi `"OldCode hoặc NewCode"` -> `"OriginalCode, ArchivedYearCode hoặc NewCode"`.
  - Đổi `"MatchedByOldCode và MatchedOldCodeDisplay"` -> `"IsHistoricalMatch và MatchedHistoricalCode"`.

### 7. [`ISS-AUD-007`] Đồng bộ Cooldown `3000ms` & Thông số Mobile UI trong `FAST-QR-CHECKIN` và `FEAT-P5-02`
- **Vị trí cần sửa 1:** `AI-Knowledge/05-FEATURES/FAST-QR-CHECKIN-AND-OFFLINE-SYNC.md` (dòng `164`).
  - **Sửa:** Đổi `"UI Client khóa 2.5s giữa các lần quét cùng 1 thẻ"` -> `"UI Client khóa 3s (3000ms) giữa các lần quét cùng 1 thẻ qua recentScannedCodesMap"` (`Views/Attendance/ScanQr.cshtml:865-868`).
- **Vị trí cần sửa 2:** `AI-Knowledge/05-FEATURES/FEAT-P5-02-MOBILE-FIRST-UI.md` (dòng `42, 44-45`).
  - **Sửa:** Đổi `54px` -> `56px` (`--mobile-header-height: 56px`); đổi `#mobileUserOffcanvas` -> `#mobileMenuOffcanvas`; đổi `z-index: 1085` -> `z-index: 1070` (với `.offcanvas-footer` là `1075`, `.offcanvas-backdrop.show` là `1065` theo `wwwroot/css/mobile-layout.css:258-290` & `Views/Shared/_LayoutMobileHeader.cshtml:53,73`).

---

## Priority 3 — Mức Độ `LOW` (Chuẩn hóa chi tiết & vệ sinh cú pháp)

### 8. [`ISS-AUD-008`] Sửa tên tham số route `{versionId:long}` trong `FEAT-P5-07`
- **Vị trí cần sửa:** `AI-Knowledge/05-FEATURES/FEAT-P5-07-CLASS-CURRICULUM-MANAGEMENT.md` (dòng `144-145`).
- **Sửa:** Đổi `/LessonPlans/GetVersionDetail/{id}` và `/LessonPlans/DownloadCurriculumVersion/{id}` thành `/LessonPlans/GetVersionDetail/{versionId:long}` và `/LessonPlans/DownloadCurriculumVersion/{versionId:long}` để khớp chính xác `Controllers/LessonPlansController.cs:205, 228`.

### 9. [`ISS-AUD-009`] Đóng block Mermaid & nâng độ phủ số dòng cho Index Baseline
- **Vị trí cần sửa 1:** `AI-Knowledge/00-OVERVIEW/PROJECT-DEPENDENCY-DIAGRAM.md` (sau dòng `17`).
  - **Sửa:** Thêm dòng đóng ```` ``` ```` sau `BG --> EF`.
- **Vị trí cần sửa 2:** `AI-Knowledge/03-CODE-INDEX/FILE-INDEX.jsonl`, `SYMBOL-INDEX.jsonl`, `ROUTE-INDEX.jsonl`, `DEPENDENCY-INDEX.jsonl`.
  - **Sửa (bảo trì định kỳ):** Điền `startLine`/`endLine`/`sourceLine` cho nhóm bản ghi baseline (`SYMBOL-INDEX:1-62`, `ROUTE-INDEX:1-29`, `DEPENDENCY-INDEX:1-40`) và từng bước mở rộng phạm vi từ `72` tệp lên đầy đủ `28` Controllers cùng các Service/Entity/View còn lại.
