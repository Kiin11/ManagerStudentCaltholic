# EXTERNAL APIs MAP

Dựa vào việc rà soát toàn bộ tệp tin trong thư mục `Controllers` và `Services`, **không phát hiện cấu hình gọi API ngoại vi (External API Integration) nào sử dụng HTTPClient, RestSharp, hoặc các SDK của bên thứ ba (như cổng thanh toán, SMS Gateway, Zalo ZNS)**. 

- Thay vì gửi Zalo qua API (ZNS), hệ thống tạo ra các mẫu tin nhắn (Template) có điền sẵn biến số, cho phép người dùng ấn nút "1-Click Copy" và dán thủ công vào ứng dụng chat. 
- Hệ thống chỉ thiết lập các Endpoint RESTful (đóng vai trò là API Server) cho chính Front-end của mình (AJAX/Fetch) sử dụng cơ chế bảo mật `X-CSRF-TOKEN`.

**Status:** NOT_PROVIDED / KHÔNG SỬ DỤNG EXTERNAL APIS.