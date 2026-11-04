# BÁO CÁO PHÂN TÍCH TÁC ĐỘNG (IMPACT ANALYSIS REPORT)

- **Dự án:** `ManagerStudentCaltholic`
- **Thời gian phân tích:** 2026-11-04 22:06:00 (+07:00)
- **Baseline đối chiếu:** Commit `7c61591` $\rightarrow$ Commit `c0e828a` (Nhánh `main`)

---

## 1. Phân loại mức độ ảnh hưởng tổng thể (Impact Level Classification)

### **Mức độ: HIGH (Ảnh hưởng diện rộng)**

### **Lý do & Căn cứ kỹ thuật:**
1. **Ảnh hưởng tới Khung Giao diện Toàn hệ thống (Root Layout):**
   - Tệp `_Layout.cshtml` là layout gốc của hầu hết các View trong ứng dụng. Việc bổ sung rẽ nhánh `@if (isMobile)` tác động trực tiếp đến cách thức toàn bộ các trang chức năng (Điểm danh, Sổ điểm, Hồ sơ thiếu nhi, Bí tích, Quản trị) hiển thị khi người dùng truy cập từ thiết bị di động hoặc trình duyệt di động PWA.
2. **Ảnh hưởng tới Cơ sở dữ liệu (Database Schema & Indexes):**
   - Bổ sung 2 bảng mới (`LookupViewOptions`, `StaffQuickLookupLogs`) vào `ParishDbContext`.
   - Bổ sung 5 chỉ mục (Indexes) trên các bảng cốt lõi có dung lượng bản ghi lớn nhất hệ thống (`Students`, `Enrollments`, `StudentCodeHistories`).
3. **Ảnh hưởng tới Cơ chế Bộ đệm & Hạ tầng (Infrastructure & Cache):**
   - Phân hệ mới tích hợp với `ICacheService` (Redis) để cache đối tượng `Student360DetailDto` với TTL 5 phút theo định dạng key `staff_lookup:student_360:{studentId}`.
4. **Tác động đến Vòng đời Service Worker & PWA:**
   - Thay đổi cơ chế reload trong `pwa-installer.js` và cập nhật header trong `_Layout.cshtml`.

---

## 2. Ma trận Phụ thuộc & Thành phần bị ảnh hưởng (Dependency Matrix)

Quy ước:
- **[Xác nhận được]**: Có bằng chứng trực tiếp trong mã nguồn (khai báo, gọi hàm, route URL).
- **[Suy luận kiến trúc]**: Không có quan hệ gọi hàm trực tiếp nhưng chia sẻ chung dữ liệu hoặc tác động gián tiếp qua UI.

### 2.1. Phân hệ Tra cứu Nội bộ (FEAT-P5-01)

| Thành phần | Thành phần gọi đến (Callers) | Thành phần được gọi (Callees) | Bằng chứng mã nguồn |
|---|---|---|---|
| **`IStaffStudentLookupService`** | `HomeController` (Constructor injection) [Xác nhận được] | Không có (Interface) | `HomeController.cs` (dòng 13, 19) |
| **`StaffStudentLookupService`** | `ServiceCollectionExtensions` (DI registration) [Xác nhận được] | `ParishDbContext`, `ILogger<StaffStudentLookupService>` [Xác nhận được] | `ServiceCollectionExtensions.cs` (dòng 113), `StaffStudentLookupService.cs` (dòng 23) |
| **`ParishDbContext` (Entities mới)** | `StaffStudentLookupService` [Xác nhận được] | `LookupViewOptions`, `StaffQuickLookupLogs`, `Students`, `Enrollments`, `ClassRooms`, `AcademicYears`, `Attendances`, `GradeRecords`, `GradeConfigurations`, `MassCampaigns`, `MassAttendanceRecords`, `SacramentApprovalItems` [Xác nhận được] | `StaffStudentLookupService.cs` (các câu truy vấn LINQ dòng 32, 55, 153, 559, 581) |
| **`HomeController` (APIs mới)** | `staff-student-lookup.js` qua `fetch()` AJAX [Xác nhận được] | `_lookupService`, `_cacheService` [Xác nhận được] | `HomeController.cs` (dòng 51, 66, 108, 123), `staff-student-lookup.js` (dòng 48, 131, 574, 599) |
| **`_StaffStudentLookupWidget.cshtml`** | `Views/Home/Index.cshtml` [Xác nhận được] | `staff-student-lookup.js` [Xác nhận được] | `Views/Home/Index.cshtml` (dòng 190, 360) |

### 2.2. Phân hệ Giao diện Mobile-First (FEAT-P5-02 / TASK-P5-02.1)

| Thành phần | Thành phần gọi đến (Callers) | Thành phần được gọi (Callees) | Bằng chứng mã nguồn |
|---|---|---|---|
| **`HttpRequestExtensions.IsMobileDevice()`** | `_Layout.cshtml` [Xác nhận được] | `HttpRequest.Query`, `HttpRequest.Headers` [Xác nhận được] | `_Layout.cshtml` (dòng 29), `HttpRequestExtensions.cs` (dòng 32) |
| **`_Layout.cshtml`** | Tất cả các Action trả về View không đặt Layout null [Xác nhận được] | `_LayoutMobileHeader.cshtml`, `_LayoutMobileBottomNav.cshtml`, `mobile-layout.css`, `pwa-installer.js` [Xác nhận được] | `_Layout.cshtml` (dòng 56, 349, 360, 977) |
| **`_LayoutMobileHeader.cshtml`** | `_Layout.cshtml` [Xác nhận được] | `AccountController` (`Logout`, `Login`), `AttendanceController` (`ScanQr`), `LookupController` (`Index`) [Xác nhận được] | `_LayoutMobileHeader.cshtml` (dòng 34, 42, 174, 185) |
| **`_LayoutMobileBottomNav.cshtml`** | `_Layout.cshtml` [Xác nhận được] | `HomeController` (`Index`), `AttendanceController` (`ScanQr`), `AccountController` (`Profile`), `LookupController` (`Index`) [Xác nhận được] | `_LayoutMobileBottomNav.cshtml` (dòng 29, 39, 61, 70, 81, 90, 98) |

---

## 3. Tác động tới các Features và Modules hiện hữu

### 3.1. Các Feature bị ảnh hưởng trực tiếp (Direct Impact)
1. **`FEAT-P5-01`: Cổng tra cứu thiếu nhi nội bộ tại HomePage**
   - *Mức độ:* **HIGH** (Toàn bộ phân hệ mới được thêm vào và vận hành ngay tại trang chủ).
   - *Phạm vi:* GLV và BĐH đăng nhập sẽ thấy widget tra cứu tức thời, tìm kiếm không cần SĐT/Ngày sinh, chuyển đổi 4 góc nhìn 360 độ.
2. **`FEAT-P5-02`: Hệ thống UI / UX Mobile Web (Mobile-First)**
   - *Mức độ:* **HIGH** (Đổi mới toàn bộ layout khi truy cập trên thiết bị di động).
   - *Phạm vi:* 
     - Sub-task 02.1.1: Thanh Bottom Navigation 5 phân khu cố định ở đáy màn hình.
     - Sub-task 02.1.2: Header thu gọn, touch target $\ge 44\times 44\text{px}$, vùng an toàn Safe Area iOS/Android, Offcanvas drawer cố định nút đăng xuất.
     - Sub-task 02.4.1 & 02.4.2: Tối ưu tải trang (loại bỏ Google Fonts chặn hiển thị trên mobile, bỏ backdrop blur nặng GPU, chống reload lặp PWA).

### 3.2. Các Feature / Module bị ảnh hưởng gián tiếp (Indirect / Inferred Impact)
1. **Module Điểm Danh (`ATTENDANCE` / `FEAT-01`):**
   - *Bằng chứng:* Phân khu "Điểm Danh" trên Bottom Nav có nút nổi bật quét QR chuyển thẳng đến `/Attendance/ScanQr`.
   - *Dữ liệu:* `StaffStudentLookupService` truy vấn dữ liệu từ bảng `Attendances` để tính tỷ lệ chuyên cần và kiểm tra trạng thái Lễ Chúa Nhật / Thứ Năm gần nhất.
2. **Module Chiến dịch Đi Lễ (`MassCampaigns`):**
   - *Bằng chứng:* `StaffStudentLookupService` truy vấn bảng `MassCampaigns` và `MassAttendanceRecords` để tổng hợp chuỗi Streak hiện tại và ngày đi lễ gần nhất.
3. **Module Học Vụ (`ACADEMIC`):**
   - *Bằng chứng:* Tab "Lịch sử 2 năm & Bí tích" truy vấn bảng `GradeRecords`, `GradeConfigurations`, `Enrollments` để tổng hợp điểm trung bình cả năm và kết quả lên lớp.
4. **Module Bí Tích (`SACRAMENT`):**
   - *Bằng chứng:* Tab "Lịch sử 2 năm & Bí tích" truy vấn `SacramentApprovalItems` và `Student` để tổng hợp tiến trình 4 mốc Bí tích (Rửa tội, Rước lễ lần đầu, Thêm sức, Bao đồng).
5. **Module Quản trị & Hệ thống (`GOVERNANCE`):**
   - *Bằng chứng:* Phân khu thứ 5 của Bottom Nav hiển thị "Quản Trị" nếu user có vai trò Admin / Ban Điều Hành / Trưởng khối (`canViewGovernance`), hoặc hiển thị "Cá Nhân" đối với GLV thường.
   - Thao tác tra cứu ghi nhận vào `StaffQuickLookupLogs` có liên kết khóa ngoại tới `Users`.

---

## 4. Đánh giá Rủi ro và Tính Tương thích (Risk & Compatibility)

| Hạng mục rủi ro | Mức độ rủi ro | Hiện trạng & Biện pháp đã áp dụng trong mã nguồn |
|---|---|---|
| **Hiệu năng truy vấn tìm kiếm đa khóa** | MEDIUM | Đã bổ sung 5 Indexes chuyên dụng trên `Students`, `Enrollments`, `StudentCodeHistories` trong `ParishDbContext.cs`. Giới hạn tìm kiếm mặc định `limit = 8`. |
| **Tải trọng Database khi GLV chuyển tab 360 độ liên tục** | LOW | Đã tích hợp `ICacheService` (Redis) với TTL 5 phút theo `studentId`. Chuyển Option không truy vấn lại DB nếu cache còn hiệu lực. |
| **Xung đột giao diện giữa Desktop và Mobile** | LOW | Tách biệt tuyệt đối bằng cấu trúc `@if (isMobile) { ... } else { ... }`. Giao diện Desktop được giữ nguyên 100% cấu trúc cũ. |
| **Giật lag khi cuộn trang trên điện thoại Android tầm trung** | LOW | Đã loại bỏ hoàn toàn thuộc tính nặng GPU `backdrop-filter: blur()` trên thanh điều hướng di động, thay bằng màu nền solid kết hợp `contain: layout paint`. |
| **Treo/khóa tệp DLL khi biên dịch song song trên Windows** | LOW | Các tệp View Razor và CSS được cấu hình độc lập, không thay đổi chữ ký các hàm đã phát hành trước đó. |
