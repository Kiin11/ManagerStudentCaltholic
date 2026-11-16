# AI-KNOWLEDGE UPDATE LOG

## Lịch sử Cập nhật (Changelog)

### Phiên cập nhật: Nâng hạn AccessToken lên 8 ngày & Mặc định duy trì đăng nhập 14 ngày
- **Thời gian cập nhật:** `2026-11-16T13:20:00+07:00`
- **Mục tiêu:** Cập nhật `AccessTokenExpirationMinutes` lên `11520` phút (8 ngày) và mặc định chọn ô "Duy trì đăng nhập (14 ngày)" (`RememberMe = true`) trên trang đăng nhập.
- **Danh sách file source cập nhật:**
  - `ManagerStudentCaltholic/appsettings.json` (`AccessTokenExpirationMinutes`: `30` ➔ `11520`)
  - `ManagerStudentCaltholic/appsettings.Development.json` (`AccessTokenExpirationMinutes`: `30` ➔ `11520`)
  - `ManagerStudentCaltholic/Services/TokenService.cs` (fallback `11520` phút trong `GenerateAccessToken`)
  - `ManagerStudentCaltholic/Models/ViewModels/AuthViewModels.cs` (`LoginRequestDto.RememberMe = true`)
  - `ManagerStudentCaltholic/Controllers/AccountController.cs` (truyền `new LoginRequestDto { RememberMe = true, ReturnUrl = returnUrl }` ở `GET Login`, và dùng `AddDays(8)` khi `!RememberMe` ở `POST Login`)
- **Danh sách tài liệu `AI-Knowledge` đã cập nhật:**
  - `AI-Knowledge/00-OVERVIEW/TECH-STACK.md`
  - `AI-Knowledge/06-UPDATE-LOG/UPDATE-LOG.md`

---

### Phiên cập nhật: Sửa cảnh báo EF Core Production (Global Query Filters & SplitQuery)
- **Thời gian cập nhật:** `2026-11-16T13:12:00+07:00`
- **Mục tiêu:** Xử lý triệt để 2 nhóm cảnh báo `[WRN]` trong log Production:
  1. Cảnh báo `PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning` trên 14 thực thể con có khóa ngoại bắt buộc trỏ tới các thực thể đã có Global Query Filter (`Student`, `ClassRoom`, `Enrollment`, `MassCampaign`, `SacramentApprovalBatch`) và 2 thực thể phụ thuộc bắc cầu (`AttendanceAuditLog`, `ClassCurriculumVersion`).
  2. Cảnh báo `MultipleCollectionIncludeWarning` khi biên dịch truy vấn nạp nhiều collection navigation mà chưa cấu hình `QuerySplittingBehavior`.
- **Danh sách file source cập nhật:**
  - `ManagerStudentCaltholic/Data/ParishDbContext.cs` (bổ sung Matching Global Query Filter cho 16 thực thể con/bắc cầu)
  - `ManagerStudentCaltholic/Extensions/ServiceCollectionExtensions.cs` (bổ sung `UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)` trong `UseNpgsql`)
  - `ManagerStudentCaltholic/Controllers/ClassesController.cs` (`.AsSplitQuery()`)
  - `ManagerStudentCaltholic/Controllers/LookupController.cs` (`.AsSplitQuery()`)
  - `ManagerStudentCaltholic/Controllers/PrintReportsController.cs` (`.AsSplitQuery()`)
  - `ManagerStudentCaltholic/Services/ClassEvaluationService.cs` (`.AsSplitQuery()`)
  - `ManagerStudentCaltholic/Services/StaffStudentLookupService.cs` (`.AsSplitQuery()` tại 2 truy vấn đa tập hợp)
- **Danh sách tài liệu `AI-Knowledge` đã cập nhật:**
  - `AI-Knowledge/02-DATABASE/DATABASE-OVERVIEW.md`
  - `AI-Knowledge/00-OVERVIEW/CODING-RULES.md`
  - `AI-Knowledge/06-UPDATE-LOG/UPDATE-LOG.md`

---

### Phiên cập nhật: `c0e828a` ➔ `2a3263d` (+ Working Tree `2026-11-15`)
- **Thời gian cập nhật:** `2026-11-15T13:50:56+07:00`
- **Commit trước đó (`Baseline`):** `c0e828a` (`perf(mobile): optimize mobile layout rendering, remove scroll listener & prevent SW reload loop`)
- **Commit hiện tại (`HEAD`):** `2a3263d` (`add`) kèm thay đổi working tree tại `appsettings.json`, `appsettings.Development.json`, `docker-compose.yml`
- **Phạm vi mã nguồn thay đổi:** `53` tệp mã nguồn & cấu hình (`7 Added`, `46 Modified`, `0 Deleted`, `0 Renamed`, loại trừ `3` build artifacts; ghi nhận `2` tệp untracked `nginx/default.conf`, `script/seed-curriculum-data.sql`).
- **Các tính năng / Module được đồng bộ:**
  1. **Module `LessonPlans` (`FEAT-P5-07`):** Đồng bộ đầy đủ `IClassCurriculumService`, `ClassCurriculumService`, `ClassCurriculumDocument`, `ClassCurriculumVersion`, `ClassLessonPlan`, `CurriculumDtos`, `LessonPlanWorkspaceViewModel`, `LessonPlansController` (6 action mới) vào `ARCHITECTURE.md`, `TABLE-INDEX.json`, `TABLE-RELATIONSHIPS.md`, `DATABASE-OVERVIEW.md` và `03-CODE-INDEX/*.jsonl`. Ghi nhận `NEEDS_VERIFICATION` do `ManagerStudentCaltholic/Migrations/` hiện rỗng.
  2. **Module `Attendance` (`FEAT-P5-04`):** Đồng bộ `UndoScanCheckIn` / `UndoScanCheckInAsync` (5 giây), `UndoScanRequestDto`, tra cứu mã năm cũ `StudentCodeHistories` + alias cache O(1), `GetOfflineStudentsCache` (`HistoricalCodes`), `ParishOfflineEngine.undoOfflineQrScan` vào `ATTENDANCE.md`, `FAST-QR-CHECKIN-AND-OFFLINE-SYNC.md`, `ARCHITECTURE.md`, `PROJECT.md` và `03-CODE-INDEX/*.jsonl`.
  3. **Module `PublicRegistration`:** Bổ sung luồng `Resubmit`, `SubmitResubmit`, `ViewOwnCertificate`, `PublicRegistrationResubmitDto`, `Views/PublicRegistration/Resubmit.cshtml` vào `PROJECT.md` và `03-CODE-INDEX/*.jsonl`.
  4. **Module `UserAccount`, `Security`, `Cache`, `PWA`:** Đồng bộ cấu hình duy trì phiên Cookie & Refresh Token **14 ngày**, `PersistKeysToFileSystem` (`dataprotection-keys`), phân quyền `AccountController.Profile(long? id)` / `UpdateProfile`, Circuit Breaker 30s + `IMemoryCache` fallback trong `RedisCacheService`, sửa `PwaController.Offline` trả `Views/Pwa/Index.cshtml` (`/Offline` và `/icons/{fileName}`).
- **Danh sách tài liệu `AI-Knowledge` đã cập nhật trong đợt đồng bộ (`18` files):**
  - `AI-Knowledge/AI-KNOWLEDGE-STRUCTURE.md`
  - `AI-Knowledge/00-OVERVIEW/PROJECT.md`
  - `AI-Knowledge/00-OVERVIEW/ARCHITECTURE.md`
  - `AI-Knowledge/00-OVERVIEW/TECH-STACK.md`
  - `AI-Knowledge/01-MODULES/ATTENDANCE.md`
  - `AI-Knowledge/02-DATABASE/DATABASE-OVERVIEW.md`
  - `AI-Knowledge/02-DATABASE/TABLE-INDEX.json`
  - `AI-Knowledge/02-DATABASE/TABLE-RELATIONSHIPS.md`
  - `AI-Knowledge/03-CODE-INDEX/FILE-INDEX.jsonl`
  - `AI-Knowledge/03-CODE-INDEX/SYMBOL-INDEX.jsonl`
  - `AI-Knowledge/03-CODE-INDEX/ROUTE-INDEX.jsonl`
  - `AI-Knowledge/03-CODE-INDEX/DEPENDENCY-INDEX.jsonl`
  - `AI-Knowledge/04-INTEGRATIONS/INTEGRATION-OVERVIEW.md`
  - `AI-Knowledge/05-FEATURES/FAST-QR-CHECKIN-AND-OFFLINE-SYNC.md`
  - `AI-Knowledge/06-UPDATE-LOG/KNOWLEDGE-BASELINE.json`
  - `AI-Knowledge/06-UPDATE-LOG/UPDATE-LOG.md`
  - `AI-Knowledge/06-UPDATE-LOG/PENDING-UPDATES.md`
  - `AI-Knowledge/06-UPDATE-LOG/UPDATE-REPORT.md`
  *(Trước đó trong bước phân tích đã tạo/cập nhật `CHANGE-ANALYSIS.md`, `IMPACT-ANALYSIS.md`, `KNOWLEDGE-UPDATE-PLAN.md`, `AFFECTED-FILES.json`.)*
- **Điểm tồn đọng (`NEEDS_VERIFICATION`):** Xem chi tiết tại `PENDING-UPDATES.md` và `UPDATE-REPORT.md`.

---

### Phiên cập nhật: `FEAT-P5-04`
- **Thời gian cập nhật:** `2026-11-05T09:48:00+07:00`
- **Mã tính năng:** `FEAT-P5-04` (Điểm danh & QR - Nâng cấp giao diện Quét QR Điểm danh)
- **Sub-tasks:**
  - `TASK-P5-04.1`: Thiết kế lại Khung ngắm Camera (Viewfinder HUD) & Điều khiển Phần cứng (Flash Torch, Flip, Zoom slider, rung & âm báo).
  - `TASK-P5-04.2`: Thanh Chuyển đổi Nhanh Buổi Quét (Session Quick-Switcher) & Cảnh báo Thẻ Mã Năm Cũ (`StudentCodeHistories`).
  - `TASK-P5-04.3`: Khay Lịch sử Vừa Quét (Recent Scans Drawer) & Hoàn tác (Undo) 5 Giây (`/Attendance/UndoScanCheckIn` & `ParishOfflineEngine.undoOfflineQrScan`).
  - `TASK-P5-04.4`: Tối ưu Quét Liên tục (Continuous Scan, chống trùng 3s) & Đồng bộ Hàng đợi Offline.
- **Danh sách file source cập nhật:**
  - `ManagerStudentCaltholic/Views/Attendance/ScanQr.cshtml`
  - `ManagerStudentCaltholic/Controllers/AttendanceController.cs`
  - `ManagerStudentCaltholic/Services/RedisAttendanceService.cs`
  - `ManagerStudentCaltholic/Interface/Services/IRedisAttendanceService.cs`
  - `ManagerStudentCaltholic/Models/ViewModels/AttendanceViewModels.cs`
  - `ManagerStudentCaltholic/wwwroot/js/offline-attendance.js`
- **Danh sách tài liệu `AI-Knowledge` tạo mới:**
  - `AI-Knowledge/05-FEATURES/FEAT-P5-04-QR-SCANNER-UPGRADE.md`

### Phiên cập nhật: `7c61591` ➔ `c0e828a`
- **Thời gian cập nhật:** `2026-11-04T22:14:00+07:00`
- **Commit trước đó (`Baseline`):** `7c61591` (`feat: Implement attendance dispute submission and management...`)
- **Commit hiện tại (`HEAD`):** `c0e828a` (`perf(mobile): optimize mobile layout rendering, remove scroll listener & prevent SW reload loop`)
- **Tổng số file source thay đổi:** `19` files (`11 Added`, `8 Modified`, `0 Deleted`, `0 Renamed`).
- **Các tính năng / Module được cập nhật:**
  1. **`FEAT-P5-01` (Module `Common` / `StaffLookup`):** Cổng tra cứu 360 độ thiếu nhi nội bộ tại Trang chủ cho GLV/BĐH đã đăng nhập (`StaffStudentLookupService`, `IStaffStudentLookupService`, `LookupViewOption`, `StaffQuickLookupLog`, `StaffStudentLookupDtos`, `HomeController`, `_StaffStudentLookupWidget.cshtml`, `staff-student-lookup.js`).
  2. **`FEAT-P5-02` / `TASK-P5-02.1` (Module `Common` / Responsive UI):** Thiết kế khung giao diện Mobile-First với nhánh `@if (isMobile)` trong `_Layout.cshtml`, `HttpRequestExtensions.IsMobileDevice`, Header thu gọn `_LayoutMobileHeader.cshtml`, Thanh điều hướng đáy 5 phân khu `_LayoutMobileBottomNav.cshtml`, `mobile-layout.css`, và tối ưu vòng đời PWA trong `pwa-installer.js`.
- **Danh sách tài liệu `AI-Knowledge` đã cập nhật (Modified):**
  - `AI-Knowledge/00-OVERVIEW/PROJECT.md`
  - `AI-Knowledge/00-OVERVIEW/ARCHITECTURE.md`
  - `AI-Knowledge/00-OVERVIEW/TECH-STACK.md`
  - `AI-Knowledge/02-DATABASE/DATABASE-OVERVIEW.md`
  - `AI-Knowledge/02-DATABASE/TABLE-INDEX.json`
  - `AI-Knowledge/02-DATABASE/TABLE-RELATIONSHIPS.md`
  - `AI-Knowledge/03-CODE-INDEX/FILE-INDEX.jsonl`
  - `AI-Knowledge/03-CODE-INDEX/SYMBOL-INDEX.jsonl`
  - `AI-Knowledge/03-CODE-INDEX/ROUTE-INDEX.jsonl`
  - `AI-Knowledge/03-CODE-INDEX/DEPENDENCY-INDEX.jsonl`
  - `AI-Knowledge/06-UPDATE-LOG/KNOWLEDGE-BASELINE.json`
  - `AI-Knowledge/06-UPDATE-LOG/UPDATE-LOG.md`
  - `AI-Knowledge/06-UPDATE-LOG/PENDING-UPDATES.md`
- **Danh sách tài liệu `AI-Knowledge` tạo mới (NEW FILE):**
  - `AI-Knowledge/05-FEATURES/FEAT-P5-01-STAFF-QUICK-LOOKUP.md`
  - `AI-Knowledge/05-FEATURES/FEAT-P5-02-MOBILE-FIRST-UI.md`
  - `AI-Knowledge/06-UPDATE-LOG/UPDATE-REPORT.md` (đồng bộ với `UPDATE-REPORT.md` tại thư mục gốc)
- **Kết quả kiểm tra (Validation):** Hoàn tất đồng bộ 100% các thay đổi từ commit `7c61591` đến `c0e828a`.

---

### Cập nhật Ngày 05/11/2026 - FEAT-P5-07: Quản Lý Kế Hoạch Lớp Theo Version Ngày Sửa
- **Phân hệ:** `Học vụ & Giáo án` (`LessonPlans`)
- **Mã tính năng:** `FEAT-P5-07` (Sub-tasks: `TASK-P5-07.1`, `TASK-P5-07.2`, `TASK-P5-07.3`, `TASK-P5-07.4`)
- **Nội dung thực hiện:**
  1. **TASK-P5-07.1 (Database & Sync):**
     - Tạo Entity `ClassCurriculumDocument` (kế hoạch hiện hành, snapshot JSON `ContentJson`, thông tin file version mới nhất).
     - Tạo Entity `ClassCurriculumVersion` (lịch sử phiên bản ngày sửa, bản chụp tuần học, đường dẫn file `.xlsx` tự động sinh).
     - Cập nhật `ClassLessonPlan`: Bổ sung `CurriculumDocumentId`, `WeekNumber`, `Semester` và liên kết với `ClassCurriculumDocument`.
     - Cấu hình Fluent API trong `ParishDbContext` cho `ClassCurriculumDocument`, `ClassCurriculumVersion` và `ClassLessonPlan`. Đã được xác nhận áp dụng migration & DDL xuống PostgreSQL (`script/seed-curriculum-data.sql`; thư mục `ManagerStudentCaltholic/Migrations/` hiện để trống sau commit `cfb2b5e`).
  2. **TASK-P5-07.2 (Parser đa định dạng):**
     - Xây dựng dịch vụ đọc file đa định dạng trong `ClassCurriculumService`: hỗ trợ `.xlsx` (ClosedXML), `.docx` (OpenXML), `.csv` (StreamReader với auto delimiter detection), `.md` (Markdown Table & List), và `.pdf` (PdfPig).
     - Tự động chuẩn hóa số tuần, phát hiện ngày thi/nghỉ lễ, trả về DTOs nạp trực tiếp vào giao diện web mà không cần reload trang.
  3. **TASK-P5-07.3 (Biên tập trực tuyến & Tự động sinh file version):**
     - Trình biên tập bảng tuần tương tác: sửa tuần, ngày học, chủ đề, GLV, công tắc đánh dấu thi/nghỉ lễ, tính chuỗi Chúa Nhật tự động.
     - Tự động sinh file Excel `.xlsx` chuẩn mẫu Giáo xứ gắn hậu tố version và ngày sửa (`KeHoach_{Class}_v{Version}_{YYYYMMDD}.xlsx`), lưu an toàn vào `PrivateUploads/curriculums`.
     - Lưu phiên bản vào `ClassCurriculumVersions` và đồng bộ nguyên tử sang `ClassLessonPlans` qua `IExecutionStrategy`.
  4. **TASK-P5-07.4 (Giao diện tích hợp, Xem lịch sử & Rollback):**
     - Cập nhật `LessonPlansController` và View `Views/LessonPlans/Index.cshtml` với giao diện 3 Tab trực quan.
     - Hỗ trợ xem lại snapshot từng phiên bản cũ, tải file `.xlsx` của ngày sửa bất kỳ, và chức năng Khôi phục (Rollback) an toàn.


## [15/11/2026] - Khắc phục toàn bộ 9 Issues kiểm định độc lập (`ISS-AUD-001` ➔ `ISS-AUD-009`)
- **Phạm vi xử lý:** Khắc phục toàn bộ sai lệch giữa mã nguồn hiện hành (`2a3263d` + working tree), comment đồng bộ Write-Through, cấu trúc bảng CSDL và kho tri thức `AI-Knowledge/`.
- **Các nội dung đã cập nhật:**
  1. **`ISS-AUD-001` (`00-OVERVIEW/`):** Xóa bỏ hoàn toàn module/domain giả lập (`Booking`, `Payment`, `Hotel`). Cập nhật `MODULE-INVENTORY.json` (`8` module thực tế), `PROJECT-INVENTORY.json` (`200` tệp) và `SOURCE-INVENTORY.md` (`200` tệp, đủ `28` Controllers).
  2. **`ISS-AUD-002` (`DATABASE-OVERVIEW.md`, `UPDATE-LOG.md`, `PENDING-UPDATES.md`):** Xác nhận trạng thái migrations/DDL PostgreSQL đã được Developer chạy và kiểm chứng (`verified`); làm rõ mô hình `43 DbSet<T>`, DDL tại `script/seed-curriculum-data.sql` và trạng thái thư mục `Migrations/` sau commit `cfb2b5e`.
  3. **`ISS-AUD-003` (`TABLE-INDEX.json`, `TABLE-RELATIONSHIPS.md`):** Đồng bộ tự động từ `ParishDbContext.cs` và `Models/Entities/*.cs` đủ **`43/43` bảng** (`Diff = 0`).
  4. **`ISS-AUD-004` & `ISS-AUD-009` (`03-CODE-INDEX/*.jsonl`, `PROJECT-DEPENDENCY-DIAGRAM.md`):** Đóng block Mermaid `PROJECT-DEPENDENCY-DIAGRAM.md`; mở rộng `FILE-INDEX.jsonl` lên `114` tệp (đủ `28/28` Controllers); tái đồng bộ `SYMBOL-INDEX.jsonl` (`192` symbols), `ROUTE-INDEX.jsonl` (`52` routes) và `DEPENDENCY-INDEX.jsonl` (`66` edges) với `0` giá trị `null` và `0` tham chiếu mồ côi.
  5. **`ISS-AUD-005` (`01-MODULES/ATTENDANCE.md`, `ARCHITECTURE.md`, `Request Flow Diagram.md`, `INTEGRATION-OVERVIEW.md` & XML/code comments):** Cập nhật thống nhất cơ chế ghi **Write-Through trực tiếp** xuống PostgreSQL cho cả `ScanCheckIn` và `SaveBatch` (`P2-LOG-16 FIX`) kèm Hangfire job dự phòng 10 phút; chỉnh lại dependency QR mã cũ dùng trực tiếp `StudentCodeHistories`.
  6. **`ISS-AUD-006`, `ISS-AUD-007`, `ISS-AUD-008` (`05-FEATURES/`):** Sửa `#staffStudentQuickSearchInput`, `280ms`, `term.length > 0`, `OriginalCode/ArchivedYearCode/NewCode`, `IsHistoricalMatch/MatchedHistoricalCode`, `LookupViewOptions`, cooldown QR `3s (3000ms)`, Mobile UI `56px` / `#mobileMenuOffcanvas` / `z-index 1070`, và `{versionId:long}` trong `FEAT-P5-07`.
