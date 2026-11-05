# FEAT-P5-04: Nâng Cấp Giao Diện Quét QR Điểm Danh & Khung Ngắm HUD

> **Feature Code:** `FEAT-P5-04`  
> **Module:** Điểm danh & QR (Attendance & Fast QR Check-in)  
> **Sub-tasks:** `TASK-P5-04.1`, `TASK-P5-04.2`, `TASK-P5-04.3`, `TASK-P5-04.4`  
> **Status:** Completed  
> **Updated Files:**
> - `ManagerStudentCaltholic/Views/Attendance/ScanQr.cshtml`
> - `ManagerStudentCaltholic/Controllers/AttendanceController.cs`
> - `ManagerStudentCaltholic/Services/RedisAttendanceService.cs`
> - `ManagerStudentCaltholic/Interface/Services/IRedisAttendanceService.cs`
> - `ManagerStudentCaltholic/Models/ViewModels/AttendanceViewModels.cs`
> - `ManagerStudentCaltholic/wwwroot/js/offline-attendance.js`

---

## 1. Feature Purpose & Overview

Nâng cấp toàn diện trải nghiệm quét mã QR điểm danh của Giáo lý viên và Ban Điều hành, tối ưu cho cả thiết bị di động (Mobile PWA) và máy tính trạm bàn có súng quét mã vạch USB:
1. **Khung ngắm toàn màn hình HUD (Viewfinder HUD):** 4 góc ngắm định tâm đổi màu trực quan theo trạng thái quét (Xanh: Hợp lệ, Vàng: Đi trễ / Mã cũ, Đỏ: Lỗi), kết hợp tia laser quét chuyển động mượt mà.
2. **Điều khiển phần cứng thiết bị:** Bật/tắt đèn Flash (Torch), đảo chiều Camera trước/sau, thanh trượt Zoom quang/số theo khả năng phần cứng (`MediaStreamTrack.getCapabilities()`), kèm phản hồi rung haptic và âm báo tổng hợp qua Web Audio API.
3. **Thanh chuyển đổi nhanh buổi quét (Session Quick-Switcher):** Đặt ngay trên đầu Camera với 4 chế độ (`SUNDAY_MASS`, `THURSDAY_MASS`, `CATECHISM_CLASS`, `CAMPAIGN_MONTH`), tự động gợi ý buổi quét theo ngày/giờ.
4. **Cảnh báo thẻ mã năm cũ:** Tự động tra cứu trong `StudentCodeHistories` khi học sinh mang thẻ năm trước; hiển thị nhãn cảnh báo trực quan mã cũ ➔ mã mới cần in lại mà vẫn ghi nhận điểm danh.
5. **Khay lịch sử vừa quét & Hoàn tác (Undo) 5 giây:** Hiển thị thẻ kết quả tức thì kèm đếm lùi 5 giây; cho phép hoàn tác cả Online (`/Attendance/UndoScanCheckIn`) và Offline (`ParishOfflineEngine.undoOfflineQrScan`).
6. **Quét liên tục (Continuous Scan) & Chống quét trùng 3 giây:** Cho phép quét hàng loạt liên tục mà không dừng luồng Camera; chống spam lặp lại cùng một mã trong 3000ms.
7. **Đồng bộ hàng đợi Offline:** Hiển thị rõ số lượt quét đang lưu trong IndexedDB khi mất mạng và tự động đồng bộ khi có sóng.

---

## 2. Architecture & Data Flow

### 2.1. Luồng Quét QR Trực Tuyến & Cảnh Báo Mã Cũ (TASK-P5-04.1 & TASK-P5-04.2)
```text
Html5Qrcode Camera (15 FPS, Continuous)
   ──> Giải mã chuỗi QR: code
   ──> Kiểm tra recentScannedCodesMap (Bỏ qua nếu cùng code < 3000ms)
   ──> POST /Attendance/ScanCheckIn { studentCode, sessionMode }
   ──> RedisAttendanceService.ProcessQrCheckInViaRedisAsync()
          ├── Cache Hit: Kiểm tra mã hiện hành vs alias mã cũ
          └── Cache Miss:
                 ├── Tra cứu Enrollments (Mã hiện hành trong năm)
                 └── Nếu không thấy ──> Tra cứu StudentCodeHistories (OriginalCode / ArchivedYearCode)
                         ├── Tìm thấy ──> isHistoricalCode = true, oldCode = cleanCode
                         └── Cache thêm alias key vào Redis O(1)
   ──> Phân tích SessionMode (Lễ Chúa Nhật, Lễ Thứ Năm, Giáo lý, Chiến dịch)
   ──> Cập nhật lưới điểm danh Redis (IsDirty = true) & Write-Through DB
   ──> Trả về QrScanResponseDto (isHistoricalCode, oldCode, currentCode, ...)
   ──> Client HUD:
          ├── Đổi màu khung ngắm (.hud-success hoặc .hud-warning)
          ├── Phát âm báo chime / cảnh báo + Rung haptic
          ├── Hiện thẻ kết quả + Banner cảnh báo thẻ cũ cần in lại
          └── Kích hoạt đếm lùi Hoàn tác 5s
```

### 2.2. Luồng Hoàn Tác Điểm Danh 5 Giây (TASK-P5-04.3)
```text
Người dùng nhấn "Hoàn tác (Xs)" trong vòng 5 giây
   ├── Nếu Online: POST /Attendance/UndoScanCheckIn { enrollmentId, classId, activityType, ... }
   │      ├── Thu hồi cờ điểm danh trong Redis ClassSheet
   │      ├── Reset bản ghi trong PostgreSQL Attendances
   │      └── Trả về success: true
   └── Nếu Offline: ParishOfflineEngine.undoOfflineQrScan(clientRecordId)
          └── Xóa bản ghi trong IndexedDB pending_attendances
   ──> UI:
          ├── Giảm bộ đếm sĩ số ca quét (-1)
          ├── Đánh dấu dòng lịch sử: "Đã hoàn tác"
          ├── Phát âm âm báo hoàn tác và ẩn nút Undo
```

---

## 3. API Endpoints & DTO Contracts

### 3.1. `POST /Attendance/ScanCheckIn`
* **Request:** `QrScanRequestDto`
  * `StudentCode` (string, required)
  * `ScanTimestamp` (DateTime?, optional)
  * `SessionMode` (string?, "AUTO" | "SUNDAY_MASS" | "THURSDAY_MASS" | "CATECHISM_CLASS" | "CAMPAIGN_MONTH")
  * `CampaignId` (long?, optional)
* **Response:** `QrScanResponseDto`
  * `Success` (bool)
  * `Message` (string)
  * `StudentCode` (string)
  * `ChristianName`, `FullName`, `ClassName`, `GradeLevel` (string)
  * `ActivityType`, `Status`, `CheckInTimeStr` (string)
  * `IsDuplicate` (bool)
  * `IsHistoricalCode` (bool)
  * `OldCode`, `CurrentCode`, `WarningMessage` (string?)
  * `EnrollmentId` (long), `ClassId` (int)

### 3.2. `POST /Attendance/UndoScanCheckIn`
* **Request:** `UndoScanRequestDto`
  * `EnrollmentId` (long, required)
  * `ClassId` (int, required)
  * `StudentCode` (string?, optional)
  * `AttendanceDate` (DateTime?, optional)
  * `ActivityType` (string?, optional)
  * `SessionMode` (string?, optional)
* **Response:** `{ success: bool, message: string }`

### 3.3. `GET /Attendance/GetOfflineStudentsCache`
* Bổ sung trường `HistoricalCodes: List<string>` trong từng đối tượng `OfflineStudentCacheItemDto` để hỗ trợ phát hiện mã cũ ngay cả khi ngoại tuyến.

---

## 4. Hardware & Client Features

1. **Hardware Controls:**
   - Torch/Flashlight: Sử dụng `MediaStreamTrack.applyConstraints({ advanced: [{ torch: true/false }] })`.
   - Camera Flip: Đảo chiều qua facingMode (`environment` <-> `user`).
   - Zoom Slider: Tự động phát hiện qua `track.getCapabilities().zoom`, cho phép trượt mượt từ 1.0x đến 3.0x.
2. **Audio & Haptics:**
   - Web Audio API Synthesizer: Không phụ thuộc tài nguyên file mp3 bên ngoài, hoạt động mượt mà không bị delay.
   - Vibration API: Tích hợp rung haptic phân biệt trạng thái Hợp lệ (`[70]`), Cảnh báo/Trễ/Cũ (`[110, 50, 110]`), Lỗi (`[220, 80, 220]`).
3. **USB Handheld Scanner Support:**
   - Tích hợp ô nhập mã hỗ trợ súng đọc mã vạch USB cầm tay với cơ chế xử lý phím Enter đồng bộ vào luồng xử lý quét.
