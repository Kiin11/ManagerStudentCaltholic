# BÁO CÁO KIỂM ĐỊNH ĐỘC LẬP TÀI LIỆU TRI THỨC (KNOWLEDGE-VALIDATION-REPORT)

> **Dự án:** `ManagerStudentCaltholic`  
> **Vai trò thực hiện:** Independent Codebase Knowledge Auditor  
> **Thời điểm kiểm định:** `2026-11-04T22:18:00+07:00`  
> **Phạm vi kiểm định:** Đối chiếu toàn bộ `AI-Knowledge/` sau cập nhật (`7c61591` ➔ `c0e828a`) và `UPDATE-REPORT.md` trực tiếp với mã nguồn thực tế tại `HEAD` (`c0e828a`).  
> **Kết luận tổng thể:** **FAIL / REQUIRES CORRECTION (KHÔNG ĐẠT — CẦN HIỆU CHỈNH)**

---

## 1. Tổng quan Kết quả Kiểm định (Executive Summary)

Qua quá trình đối chiếu độc lập từng dòng mã nguồn của 19 file thay đổi (và các file liên đới trong `ManagerStudentCaltholic/`) với bộ tài liệu `AI-Knowledge/` vừa cập nhật, kiểm định viên ghi nhận:
- **Điểm đạt (Verified Accurate):** Cú pháp JSON/JSONL hợp lệ 100% (không dính Markdown fence); danh sách 11 file mới trong `FILE-INDEX.jsonl` và tên bảng/cột của 2 bảng mới (`StaffQuickLookupLogs`, `LookupViewOptions`) khớp với C# Entities; không có tham chiếu tới file đã xóa trong dải `7c61591..c0e828a`.
- **Điểm không đạt (Blocking Issues):** Phát hiện **14 vấn đề (`AUD-001` ➔ `AUD-014`)**, bao gồm:
  - **3 lỗi `CRITICAL`:** Suy diễn sai sự thật (hallucination) về việc truy vấn bảng `ClassTeachers`, `StudentRegistrationRequests`, `ParentMessageTemplates` và cơ chế chống trùng log 10 phút trong `FEAT-P5-01-STAFF-QUICK-LOOKUP.md` & `ARCHITECTURE.md`; bỏ sót dependency cốt lõi `IGradeCalculationService` của `StaffStudentLookupService` trong cả `DEPENDENCY-INDEX.jsonl`, `UPDATE-REPORT.md` và tài liệu feature.
  - **5 lỗi `HIGH`:** Mô tả sai cơ chế phân quyền `[Authorize]` trên `HomeController` (`StaffLookupOptions` thực tế đang public, các action khác chỉ kiểm tra `IsAuthenticated` thủ công không lọc Role); mô tả sai bố cục 5 nút Bottom Nav, class `.mobile-subnav-strip` không tồn tại, sai CSS variables/IDs trong `FEAT-P5-02-MOBILE-FIRST-UI.md`; sai chữ ký hàm (`term` vs `keyword`, `[FromQuery]` ảo) và lệch toàn bộ số dòng trong `SYMBOL-INDEX.jsonl`; sai dữ liệu Seed `IconClass`/`DisplayName` và tên thuộc tính DTO; bỏ sót bảng `StudentCodeHistories` trong `TABLE-INDEX.json` dù bảng này có thêm index `NewCode`.
  - **4 lỗi `MEDIUM`:** Đứt gãy khóa ngoại symbol (`targetSymbol`) giữa `DEPENDENCY-INDEX.jsonl` và `SYMBOL-INDEX.jsonl`; chưa cập nhật `INTEGRATION-OVERVIEW.md` và bộ `*-INVENTORY.*`; nâng `KNOWLEDGE-BASELINE.json` lên `"completed"` trước khi pass bước Audit; thiếu nhãn `NEEDS_VERIFICATION` ngay trong `DATABASE-OVERVIEW.md` đối với 2 bảng thiếu EF Migration.
  - **2 lỗi `LOW`:** Chưa ghi chú XML comment lỗi thời (`Cookie Parish_Device_Mode`) và class CSS dư thừa (`.device-switch-banner`); chưa phân biệt tham số `limit` giữa `HomeController.Index` (`5`) và `StaffRecentLookups` (`6`) — endpoint chưa được gọi từ JS.

---

## 2. Kết quả Kiểm định theo 10 Hạng mục (10-Point Validation Matrix)

| STT | Hạng mục Kiểm định | Kết quả | Tóm tắt Bằng chứng & Phát hiện |
|---|---|---|---|
| **1** | **Class, Method, Namespace & File Path** | **FAIL** | Namespace và đường dẫn 11 file mới đúng. Tuy nhiên `SYMBOL-INDEX.jsonl` ghi sai tên tham số (`term` thay vì `keyword` ở `SearchStudentsQuickAsync`), sai kiểu nullable (`string?` thay vì `string` ở `GetStudent360DetailAsync`), tự thêm `[FromQuery]` không có trong `HomeController`, lệch toàn bộ `startLine`/`endLine` (ví dụ `StaffStudentLookupService` dài 714 dòng nhưng ghi `endLine: 498`), và thiếu 12/14 class DTO của `StaffStudentLookupDtos.cs` (`AUD-006`). |
| **2** | **Route & Controller Action** | **FAIL** | 5 route mới của `HomeController` đúng URL và HTTP verb (`GET`), nhưng sai `startLine`, bỏ sót 3 action khác có sẵn trong `HomeController.cs` (`POST /Home/DetachRememberedStudent`, `GET /Home/Privacy`, `GET /Home/Error`), và tài liệu mô tả sai rằng các action có gắn `[Authorize]` theo Role GLV/BĐH (`AUD-004`). |
| **3** | **Dependency có bằng chứng** | **FAIL** | `StaffStudentLookupService` tiêm `IGradeCalculationService` (gọi `_gradeCalcService.CalculateSemesterAverage`) và gọi `VietnameseStringHelper.RemoveDiacritics`, nhưng cả `DEPENDENCY-INDEX.jsonl`, `UPDATE-REPORT.md` và `FEAT-P5-01-STAFF-QUICK-LOOKUP.md` đều bỏ sót `IGradeCalculationService` (`AUD-003`). Ngoài ra `DEPENDENCY-INDEX.jsonl` trỏ tới các `targetSymbol` chưa tồn tại trong `SYMBOL-INDEX.jsonl` (`AUD-009`). |
| **4** | **Feature Document & Luồng xử lý** | **FAIL** | Cả 2 file mới `FEAT-P5-01-STAFF-QUICK-LOOKUP.md` và `FEAT-P5-02-MOBILE-FIRST-UI.md` chứa nhiều chi tiết bị suy diễn sai so với code thực tế: truy vấn 3 bảng không hề diễn ra (`AUD-001`), logic gộp log 10 phút không tồn tại (`AUD-002`), sai thứ tự 5 nút Bottom Nav & bịa class `.mobile-subnav-strip` (`AUD-005`), sai `IconClass` seed data, tên thuộc tính DTO và cơ chế debounce/cache của JS (`AUD-007`). |
| **5** | **Database Table, Column & Relationship** | **FAIL** | Cấu trúc cột, PK, FK (`Cascade`) của `StaffQuickLookupLogs` và `LookupViewOptions` chính xác. Tuy nhiên bảng `StudentCodeHistories` (được thêm index `NewCode` ở dòng 641 `ParishDbContext.cs`) vẫn vắng mặt trong `TABLE-INDEX.json`, và khối JSON nhúng lặp trong `TABLE-RELATIONSHIPS.md` (17 bảng) mâu thuẫn với `TABLE-INDEX.json` (20 bảng), chưa kể `ParishDbContext.cs` thực tế có 41 bảng (`AUD-008`). |
| **6** | **API & Integration** | **PARTIAL / FAIL** | Các AJAX JSON endpoints nội bộ và khóa Redis `staff_lookup:student_360:{studentId}` (TTL 5 phút) có thật trong `HomeController.cs`, nhưng `AI-Knowledge/04-INTEGRATIONS/INTEGRATION-OVERVIEW.md` chưa được cập nhật để ghi nhận tích hợp Redis mới này (`AUD-010`). |
| **7** | **Tham chiếu tới Class / Method đã xóa** | **PASS (với lưu ý)** | Không có class/method nào bị xóa trong dải commit `7c61591..c0e828a`. Lưu ý: XML comment trong `HttpRequestExtensions.cs` vẫn nhắc tới cookie `Parish_Device_Mode` đã bỏ (`AUD-013`), và `DEPENDENCY-INDEX.jsonl` vẫn còn tham chiếu đứt gãy tới các symbol thiếu trong `SYMBOL-INDEX.jsonl` (`AUD-009`). |
| **8** | **Tính hợp lệ của JSON & JSONL** | **PASS** | Đã kiểm tra bằng trình phân tích cú pháp JSON trên toàn bộ 5 file `.json`, 4 file `.jsonl` (tổng 220 dòng) và cả khối JSON nhúng trong `TABLE-RELATIONSHIPS.md`: **100% hợp lệ**, không có lỗi cú pháp, không bị bọc Markdown code fence. |
| **9** | **Tài liệu bị ảnh hưởng nhưng chưa cập nhật** | **FAIL** | Bỏ sót chưa cập nhật: `AI-Knowledge/04-INTEGRATIONS/INTEGRATION-OVERVIEW.md`, các file kiểm kê `MODULE-INVENTORY.json`, `SOURCE-INVENTORY.md`, `PROJECT-INVENTORY.json`, và 6 file được sửa đổi (`Modified`) chưa có mặt trong `FILE-INDEX.jsonl` (`AUD-010`). Đồng thời `KNOWLEDGE-BASELINE.json` bị nâng sớm trước khi qua bước Audit (`AUD-011`). |
| **10** | **Đánh dấu rõ thông tin suy luận / chưa xác minh** | **FAIL** | `UPDATE-REPORT.md` và `PENDING-UPDATES.md` có gắn nhãn `NEEDS_VERIFICATION` tốt, nhưng chính `DATABASE-OVERVIEW.md` lại ghi `"ORM Confirmed"` cho `StaffQuickLookupLogs` và `LookupViewOptions` mà không cảnh báo việc thiếu EF Core Migration trong `Migrations/` (`AUD-012`), và nhiều chi tiết suy diễn trong 2 file `05-FEATURES/*.md` được viết như sự thật đã xác nhận (`AUD-001`, `AUD-002`, `AUD-005`, `AUD-007`). |

---

## 3. Phân tích Chi tiết Các Lỗi Trọng Yếu (Critical & High Findings)

### 3.1. `[CRITICAL]` Suy diễn sai nguồn dữ liệu & logic trong `FEAT-P5-01-STAFF-QUICK-LOOKUP.md` và `ARCHITECTURE.md` (`AUD-001`, `AUD-002`)
- **Tài liệu hiện tại ghi:**
  - `GetStudent360DetailAsync` tổng hợp từ `ClassTeachers`, `StudentRegistrationRequests` (người giám hộ/đỡ đầu) và `ParentMessageTemplates` (điền biến `{TenThieuNhi}`, `{TenThanh}`, `{Lop}`, `{MaHV}`, `{NgayHienTai}`).
  - `RecordLookupLogAsync` kiểm tra nếu cùng `UserId` và `StudentId` trong 10 phút gần nhất thì chỉ cập nhật `LookedUpAt` thay vì thêm dòng mới.
- **Đối chiếu Source Code (`Services/StaffStudentLookupService.cs`):**
  - Không có bất kỳ truy vấn nào tới `_context.ClassTeachers`, `_context.StudentRegistrationRequests`, hay `_context.ParentMessageTemplates`.
  - Hàm `PopulateParentContact` (dòng `512–555`) gán cứng `Address = "Giáo xứ Phú Trung, Hạt Tân Sơn Nhì, TGP Sài Gòn"` và khởi tạo cứng 4 đối tượng `ParentMessageQuickTemplateDto` ngay trong code C#.
  - Hàm `RecordLookupLogAsync` (dòng `557–577`) luôn thực thi `_context.StaffQuickLookupLogs.Add(...)` + `await _context.SaveChangesAsync()` cho mọi lần gọi.

### 3.2. `[CRITICAL]` Bỏ sót Dependency `IGradeCalculationService` của `StaffStudentLookupService` (`AUD-003`)
- **Đối chiếu Source Code (`Services/StaffStudentLookupService.cs` dòng `17–28`, `369`, `414`):**
  ```csharp
  public StaffStudentLookupService(
      ParishDbContext context,
      IGradeCalculationService gradeCalcService,
      ILogger<StaffStudentLookupService> logger)
  ```
- **Sai lệch:** Cả `DEPENDENCY-INDEX.jsonl`, `UPDATE-REPORT.md` (Mục 5) lẫn `FEAT-P5-01-STAFF-QUICK-LOOKUP.md` đều bỏ sót hoàn toàn `IGradeCalculationService` (thậm chí `FEAT-P5-01-STAFF-QUICK-LOOKUP.md` ghi nhầm thành hàm nội bộ `CalculateWeightedAverage`).

### 3.3. `[HIGH]` Sai lệch Hợp đồng Bảo mật (`[Authorize]`) và Thiếu Routes trên `HomeController` (`AUD-004`)
- **Đối chiếu Source Code (`Controllers/HomeController.cs` dòng `9–153`):**
  - Class `HomeController` **không có** `[Authorize]`.
  - Ba action `StaffSearchSuggestions`, `StaffStudentLookup`, `StaffRecentLookups` chỉ kiểm tra `User.Identity?.IsAuthenticated != true` (không kiểm tra Role `Admin`, `ExecutiveBoard`, `BranchHead`, `Teacher`).
  - Action `GET /Home/StaffLookupOptions` (dòng `124–128`) **không có bất kỳ kiểm tra xác thực nào** (ai cũng truy cập được).
  - Ba action `POST /Home/DetachRememberedStudent`, `GET /Home/Privacy`, `GET /Home/Error` chưa được đưa vào `ROUTE-INDEX.jsonl`.

### 3.4. `[HIGH]` Sai lệch Cấu trúc Giao diện Mobile-First trong `FEAT-P5-02-MOBILE-FIRST-UI.md` (`AUD-005`)
- **Đối chiếu Source Code (`_LayoutMobileBottomNav.cshtml`, `_LayoutMobileHeader.cshtml`, `mobile-layout.css`):**
  - Không hề tồn tại dải `.mobile-subnav-strip` trong `_LayoutMobileBottomNav.cshtml`.
  - Thứ tự thực tế 5 nút trên `.mobile-bottom-nav`: **(1) Chung** ➔ **(2) Quét QR** (nếu `isStaff`, nổi bật `.qr-highlight .qr-circle`) / **Điểm Danh** (nếu không phải `isStaff`) ➔ **(3) Học Vụ** ➔ **(4) Bí Tích** ➔ **(5) Quản Trị** (nếu `canViewGovernance`) / **Cá Nhân** (nếu `isAuthenticated`) / **Tra Cứu** (nếu khách). Tài liệu đang mô tả sai rằng nút Quét QR/Tra Cứu nằm ở vị trí số 3 trung tâm.
  - Biến CSS thực tế là `--mobile-header-height: 56px` và `--mobile-bottom-nav-height: 62px` (tài liệu ghi `54px` và `64px`); Offcanvas thực tế có ID `#mobileMenuOffcanvas` và `z-index: 1070` (tài liệu ghi `#mobileUserOffcanvas` và `z-index: 1085`).

### 3.5. `[HIGH]` Sai Chữ ký Hàm, Lệch Số dòng trong `SYMBOL-INDEX.jsonl` & Thiếu `StudentCodeHistories` trong `TABLE-INDEX.json` (`AUD-006`, `AUD-008`)
- Chi tiết từng dòng cần sửa được liệt kê đầy đủ trong `KNOWLEDGE-ISSUES.jsonl` và hướng dẫn khắc phục tại `REQUIRED-CORRECTIONS.md`.

---

## 4. Các Tồn đọng Kế thừa Chưa Đủ Source / Chưa Khắc phục từ Baseline Trước (`Inherited Baseline Gaps`)

Ngoài các vấn đề phát sinh trong đợt cập nhật `7c61591..c0e828a`, kiểm định viên ghi nhận bộ `AI-Knowledge/` gốc vẫn còn các tồn đọng từ lần kiểm định trước (chưa thuộc phạm vi sửa của 19 file vừa qua nhưng khiến tổng thể kho tri thức chưa thể đạt `PASS` tuyệt đối):
1. `FILE-INDEX.jsonl` hiện mới lập chỉ mục `56` files, `ROUTE-INDEX.jsonl` mới có `35` routes, và `TABLE-INDEX.json` mới có `20/41` bảng của `ParishDbContext.cs`.
2. `MODULE-INVENTORY.json` và `SOURCE-INVENTORY.md` vẫn còn các tên module mẫu sai lệch (`Hotel`, `Booking`, `PaymentPaymentModule`).
3. `ATTENDANCE.md` vẫn còn nhận định cũ chưa cập nhật về Write-Through trong `SaveBatch` (`P2-LOG-16 FIX`).

> **Tuân thủ nguyên tắc Audit:** Không có bất kỳ file source code hay file tài liệu hiện hữu nào trong `AI-Knowledge/` bị chỉnh sửa trong bước kiểm định này. Toàn bộ danh sách lỗi máy đọc được xuất tại `KNOWLEDGE-ISSUES.jsonl` và danh mục hướng dẫn sửa chữa chi tiết nằm tại `REQUIRED-CORRECTIONS.md`.
