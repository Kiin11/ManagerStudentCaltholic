# PENDING KNOWLEDGE UPDATES

## 1. Trạng thái Đồng bộ Hiện tại
- Toàn bộ thay đổi mã nguồn từ commit `7c61591` đến commit `c0e828a` (`FEAT-P5-01` và `FEAT-P5-02 / TASK-P5-02.1`) **đã được phân tích và cập nhật đầy đủ** vào kho tri thức `AI-Knowledge/`.

## 2. Các Hạng mục Chờ Triển khai ở Các Sprint Tiếp theo
1. **`FEAT-P5-02` (`TASK-P5-02.2`, `TASK-P5-02.3`, `TASK-P5-02.4`):**
   - Chuyển đổi bảng Sổ điểm danh, Chiến dịch đi lễ, Danh sách thiếu nhi và Duyệt báo sai sót sang giao diện Thẻ rút gọn (Card View) trên Mobile.
   - Tối ưu giao diện Sổ điểm & Xét duyệt bằng Ngăn kéo trượt (Bottom Sheet).
   - Bổ sung Entity `UserHomepageWidgetConfigs` (được đề cập trong đặc tả `FEAT-P5-02` nhưng chưa xuất hiện trong source code tính đến commit `c0e828a` — `NEEDS_VERIFICATION`).
2. **Xác minh Migration / SQL Script trên Database thực tế (`NEEDS_VERIFICATION`):**
   - Hai bảng `StaffQuickLookupLogs` và `LookupViewOptions` cùng các index mới đã được khai báo trong `ParishDbContext.cs`, nhưng file EF Migration `20260410164552_AddStaffQuickLookupTablesAndIndexes.cs` đã bị xóa ở commit `0f5331a` (thay bằng script SQL rời). Cần xác nhận trạng thái thực thi DDL trên PostgreSQL môi trường tích hợp/production.
