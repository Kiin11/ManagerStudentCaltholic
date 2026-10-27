using Microsoft.AspNetCore.Http;

namespace ManagerStudentCaltholic.Middlewares
{
    public class SecurityHeadersMiddleware
    {
        private readonly RequestDelegate _next;

        public SecurityHeadersMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var headers = context.Response.Headers;

            // 1. Ngăn chặn trang web bị nhúng vào iframe bên ngoài (Chống Clickjacking)
            headers["X-Frame-Options"] = "SAMEORIGIN";

            // 2. Ngăn chặn trình duyệt tự ý phỏng đoán MIME type
            headers["X-Content-Type-Options"] = "nosniff";

            // 3. Kích hoạt bộ lọc XSS tích hợp của trình duyệt cũ
            headers["X-XSS-Protection"] = "1; mode=block";

            // 4. Kiểm soát thông tin Referrer gửi sang domain khác
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

            // 5. SEC-11 FIX: Chỉ cấp quyền Camera cho chính domain của ứng dụng (phục vụ Quét QR Điểm danh)
            // và khóa toàn bộ quyền nhạy cảm không sử dụng (Microphone, Geolocation, USB, Payment)
            headers["Permissions-Policy"] = "camera=(self), microphone=(), geolocation=(), payment=(), usb=()";

            // 6. SEC-11 FIX: Loại bỏ 'unsafe-eval', khóa chặt object-src, base-uri, form-action và frame-ancestors
            headers["Content-Security-Policy"] =
                "default-src 'self'; " +
                "script-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net; " +
                "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net; " +
                "img-src 'self' data: blob: https:; " +
                "font-src 'self' data: https://cdn.jsdelivr.net; " +
                "connect-src 'self' https://cdn.jsdelivr.net; " +
                "media-src 'self' blob:; " +
                "object-src 'none'; " +
                "base-uri 'self'; " +
                "form-action 'self'; " +
                "frame-ancestors 'self';";

            // 7. Ẩn header nhận diện máy chủ
            headers.Remove("Server");
            headers.Remove("X-Powered-By");

            await _next(context);
        }
    }
}