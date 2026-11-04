## 1. Overview

* **Mục đích module:** Quản lý toàn diện tiến trình điểm danh của thiếu nhi bao gồm Thánh Lễ (Thứ Năm, Chúa Nhật) và Giờ học Giáo lý. Hỗ trợ điểm danh tốc độ cao qua mã QR, ghi nhận ngoại tuyến (offline), xin phép vắng, bù điểm danh và giải quyết khiếu nại (báo sai sót) từ phụ huynh.
* **Các chức năng đã được triển khai:**
* Quét mã QR điểm danh (Online & Offline).


* Sổ điểm danh lưới thủ công theo lớp.


* Đồng bộ dữ liệu ngoại tuyến (Offline Sync) xử lý xung đột.


* Thống kê chuyên cần và xuất báo cáo Excel.


* Phụ huynh nộp đơn xin phép vắng và GLV duyệt đơn.


* Phụ huynh báo sai sót điểm danh (Dispute) và GLV/BĐH duyệt.


* Điểm danh bù (Makeup attendance).


* Hangfire Background Jobs nạp bộ đệm (Pre-warm) và đồng bộ DB (Sync-back).




* **Các project liên quan:** `ManagerStudentCaltholic` (Monolith).


* **Các module khác có tương tác:**
* **Class / Enrollment:** Lấy danh sách thiếu nhi theo lớp.


* **Academic / Grades:** Sử dụng dữ liệu chuyên cần để xét duyệt điều kiện Bí tích / Lên lớp.


* **MassCampaigns:** Điểm danh chiến dịch đi lễ 1 tháng.





## 2. Features

* **Fast QR Check-in:** Quét QR trực tiếp qua Redis với độ trễ thấp.


* **Offline-First Resilience:** Ghi nhận điểm danh tại Client (IndexedDB) khi rớt mạng và đồng bộ hàng loạt lên máy chủ.


* **Automated Absence Marking:** Tự động ghi vắng không phép cho học sinh chưa điểm danh cuối ngày.


* **Dispute Resolution:** Quy trình 2 bước giải quyết khiếu nại điểm danh (Phụ huynh gửi yêu cầu -> Hệ thống đối chiếu -> GLV duyệt).



## 3. Components

* **Controllers:**
* `D:\github\...\Controllers\AttendanceController.cs` (Xử lý điểm danh, QR, Offline Sync, Thống kê).


* `D:\github\...\Controllers\AbsenceController.cs` (Xử lý đơn xin nghỉ phép).




* **Services:**
* `D:\github\...\Services\RedisAttendanceService.cs` (Xử lý logic Redis, Pre-warm, Sync-back).


* `D:\github\...\Services\AttendanceDisputeService.cs` (Xử lý khiếu nại điểm danh).


* `D:\github\...\Services\AttendanceTimeHelper.cs` (Helper xác định ca học/trễ giờ).




* **Models / Entities:**
* `D:\github\...\Models\Entities\Attendance.cs`.


* `D:\github\...\Models\Entities\AttendanceAuditLog.cs`.


* `D:\github\...\Models\Entities\AbsenceRequest.cs`.


* `D:\github\...\Models\Entities\AttendanceDisputeRequest.cs`.




* **DTOs / ViewModels:**
* `D:\github\...\Models\ViewModels\AttendanceViewModels.cs` (`AttendanceBatchSubmitDto`, `QrScanRequestDto`, `ClassAttendanceSheetViewModel`, v.v.).


* `D:\github\...\Models\DTOs\HighThroughputAttendanceModels.cs` (`RedisClassAttendancePayload`, `RedisQrStudentLookupItem`).




* **Views:**
* `D:\github\...\Views\Attendance\Index.cshtml` (Sổ điểm danh lưới).


* `D:\github\...\Views\Attendance\ScanQr.cshtml` (Giao diện quét QR).


* `D:\github\...\Views\Attendance\Statistics.cshtml` (Thống kê).


* `D:\github\...\Views\Absence\Manage.cshtml` (Duyệt phép).





## 4. Business Flows

### Flow 1: Fast QR Check-in (Online)

* **Entry point:** `AttendanceController.ScanCheckIn` (POST).


* **Input:** `QrScanRequestDto` (StudentCode).


* **Validation:** Kiểm tra null/trống mã QR.


* **Business processing:** Gọi `RedisAttendanceService.ProcessQrCheckInViaRedisAsync` -> Lookup Redis -> Đánh giá thời gian bằng `AttendanceTimeHelper` -> Cập nhật trạng thái `IsDirty = true` trên Redis.


* **Database access:** Ghi Write-Through tức thì xuống PostgreSQL thông qua `SyncDirtyAttendancesToDatabaseAsync` (trong cùng Request).


* **Output:** Trả về `QrScanResponseDto` dạng JSON cho UI để phát âm thanh.



### Flow 2: Offline Synchronization

* **Entry point:** `AttendanceController.SyncOfflineQueue` (POST).


* **Input:** `OfflineSyncQueueRequestDto` (Mảng dữ liệu điểm danh offline từ IndexedDB).


* **Validation:** Kiểm tra quyền `CanAccessClassAsync`, rà soát khớp EnrollmentId hoặc StudentCode.


* **Business processing (Conflict Resolution):**
* Ưu tiên giữ trạng thái có mặt (`AttendedMass = true`).


* Ưu tiên giữ thời gian quét QR sớm nhất (`MassCheckInTime < existing`).


* Qr Scan được ưu tiên hơn Grid Save nếu có xung đột.




* **Database access:** Mở `BeginTransactionAsync`, Upsert dữ liệu vào bảng `Attendances`.


* **Output:** JSON báo cáo số lượng bản ghi đã đồng bộ (`syncedCount`).



### Flow 3: Parent Dispute Resolution

* **Entry point:** `AttendanceController.ReviewDispute` (POST) thông qua `AttendanceDisputeService`.


* **Input:** `ReviewAttendanceDisputeRequestDto` (Id, Quyết định, Ghi chú).


* **Validation:** Giới hạn tối đa 5 yêu cầu Pending / 1 học sinh (chống spam).


* **Business processing:** GLV chọn "APPROVED" -> Hệ thống tự động thay đổi trạng thái trong DB thành `RequestedStatus` -> Ghi Log vào `AttendanceAuditLogs` -> Xóa Cache Redis ngày hôm đó để UI nạp lại.



## 5. Business Rules

* **Quy tắc thời gian (Time Evaluation):**


* *Lễ Thứ Năm:* 17:30 - 19:30 (Mặc định Present).
* *Lễ Chúa Nhật:* 06:20 - 08:20 (< 06:35: Lined Up / Xếp hàng; < 07:00: Present / Đúng giờ; > 07:00: Late / Trễ).
* *Học Khối Sáng:* 08:55 - 11:00 (< 09:30: Present; > 09:30: Late).
* *Học Khối Chiều:* 14:30 - 17:10 (< 15:15: Present; > 15:15: Late).


* **Quy tắc tính chuyên cần:**


* `LINED_UP` = 1.0; `PRESENT` = 1.0; `LATE` = 0.8; `ABSENT_PERMITTED` = 0.5. (Tùy chỉnh được thông qua `AttendanceRuleConfigs`).


* **Quy tắc cấp quyền truy cập lớp học (GetAuthorizedClassesAsync):**


* *Leadership (Admin, Cha Xứ, BĐH):* Xem tất cả các lớp.
* *BranchHead (Trưởng khối):* Xem lớp thuộc khối được phân công + lớp đứng dạy.
* *Teacher (GLV):* Chỉ xem các lớp có gán `UserId` trong bảng `ClassTeachers`.



## 6. Dependencies

* **Dependencies nội bộ:** Gọi `IStudentCodeRolloverService` (để xử lý mã QR cũ của năm ngoái).


* **Database dependencies:** Tables: `Attendances`, `AbsenceRequests`, `AttendanceAuditLogs`, `AttendanceDisputeRequests`, `ClassSchedules`, `Announcements`.


* **External Dependencies:** `StackExchange.Redis` (Caching), `Hangfire` (Background Job).



## 7. Database Usage

* Sử dụng trực tiếp Entity Framework Core qua `ParishDbContext` (Không qua Repository Layer).
* Sử dụng `CreateExecutionStrategy()` kết hợp `BeginTransactionAsync()` để bọc các tiến trình ghi số lượng lớn (Batch Upsert).


* Cột khóa phụ: Sử dụng `EnrollmentId` và `AttendanceDate` để tạo khóa Unique ngăn trùng lặp dữ liệu điểm danh.



## 8. Integrations

* **Redis Cache (High-Throughput):**
* Nạp đệm bằng Hangfire lúc 5h30 sáng CN (`PreWarmDailyAttendanceAndTasksAsync`).


* Dữ liệu tra cứu O(1) lưu theo Key format: `ATT:QR:DATE:{date}:CODE:{code}`.


* Lưới điểm danh (Class Sheet) lưu theo Key format: `ATT:CLASS:{classId}:DATE:{date}`.





## 9. Risk Areas

* **Memory Overhead khi Xuất Excel:** Trong `AttendanceController.ExportStatisticsExcel`, hệ thống query toàn bộ Enrollments và Attendances của một lớp vào bộ nhớ qua `.ToListAsync()` trước khi xử lý logic tính toán. Nếu dữ liệu điểm danh của cả năm quá lớn, có nguy cơ gây áp lực lên RAM (Mặc dù đã dùng `.AsNoTracking()`).


* **Data Inconsistency rủi ro Redis:** Mặc dù luồng `ScanCheckIn` đã fix lỗi bằng cách áp dụng Write-Through trực tiếp xuống DB (không chờ 10 phút), nhưng luồng `SaveBatchToRedisAsync` vẫn để cờ `IsDirty=true` chờ Hangfire quét. Nếu Redis sập giữa chu kỳ 10 phút, những dữ liệu lưới lưu thủ công chưa sync có thể bị mất.


* **Dynamic Policy/Reflection:** Không có dynamic reflection, mọi policy role được set cứng.



## 10. Source Evidence

* *Time Evaluation Rule:* Xác nhận tại `AttendanceTimeHelper.cs`, hàm `EvaluateCheckIn`.


* *Conflict Resolution Offline:* Xác nhận tại `AttendanceController.cs`, phương thức `SyncOfflineQueue`, dòng chú thích `THUẬT TOÁN XỬ LÝ XUNG ĐỘT (CONFLICT RESOLUTION)`.


* *Hangfire Jobs:* Xác nhận tại `Program.cs`, các hàm `RecurringJob.AddOrUpdate<IRedisAttendanceService>`.



## 11. Missing Information

* Cơ chế giải phóng dung lượng Redis (Eviction Policy) chỉ dựa vào TTL mặc định (`DefaultCacheTtl = TimeSpan.FromHours(18)`), chưa rõ có cấu hình MaxMemory policy cụ thể trong Redis Server hay không, ngoài biến môi trường trong docker-compose.



---

### Component Mapping Table (ATTENDANCE)

| Feature | Controller | Service | Repository | Model/Entity | View / Razor | Database Table |
| --- | --- | --- | --- | --- | --- | --- |
| **QR Check-in** | `AttendanceController` | `RedisAttendanceService` | `NOT_FOUND` (Dùng DbContext) | `Attendance` | `ScanQr.cshtml` | `Attendances` |
| **Class Grid Save** | `AttendanceController` | `RedisAttendanceService` | `NOT_FOUND` | `Attendance` | `Index.cshtml` | `Attendances` |
| **Offline Sync** | `AttendanceController` | `NOT_FOUND` (Logic trong Controller) | `NOT_FOUND` | `Attendance` | `Index.cshtml` | `Attendances` |
| **Absence Mgt** | `AbsenceController` | `NOT_FOUND` (Logic trong Controller) | `NOT_FOUND` | `AbsenceRequest` | `Manage.cshtml` | `AbsenceRequests` |
| **Dispute Review** | `AttendanceController` | `AttendanceDisputeService` | `NOT_FOUND` | `AttendanceDisputeRequest` | `Index.cshtml` (Modal) | `AttendanceDisputeRequests` |
| **Makeup Check-in** | `AttendanceController` | `NOT_FOUND` (Logic trong Controller) | `NOT_FOUND` | `Attendance`, `AttendanceAuditLog` | `Index.cshtml` (Modal) | `Attendances`, `AttendanceAuditLogs` |
| **Statistics** | `AttendanceController` | `NOT_FOUND` (Logic trong Controller) | `NOT_FOUND` | `Attendance` | `Statistics.cshtml` | `Attendances` |
| **Monthly Summary** | `AttendanceController` | `NOT_FOUND` (Logic trong Controller) | `NOT_FOUND` | `Attendance` | `MonthlySummary.cshtml` | `Attendances` |
| **Class Rules** | `AttendanceController` | `NOT_FOUND` | `NOT_FOUND` | `AttendanceRuleConfig` | `Index.cshtml` (Modal) | `AttendanceRuleConfigs` |