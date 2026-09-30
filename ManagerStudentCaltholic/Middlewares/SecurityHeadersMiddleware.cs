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
            // TASK-707: Bổ sung các Security Headers chuẩn OWASP
            var headers = context.Response.Headers;

            // Ngăn chặn trang web bị nhúng vào iframe (Chống Clickjacking)
            headers["X-Frame-Options"] = "SAMEORIGIN";

            // Ngăn chặn trình duyệt tự ý phỏng đoán MIME type
            headers["X-Content-Type-Options"] = "nosniff";

            // Kích hoạt bộ lọc XSS tích hợp của trình duyệt cũ
            headers["X-XSS-Protection"] = "1; mode=block";

            // Kiểm soát thông tin Referrer gửi sang domain khác
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

            // Chính sách Content Security Policy (CSP) cơ bản bảo vệ tài nguyên
            headers["Content-Security-Policy"] = "default-src 'self'; " +
                "script-src 'self' 'unsafe-inline' 'unsafe-eval' https://cdn.jsdelivr.net; " +
                "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net; " +
                "img-src 'self' data: https:; " +
                "font-src 'self' https://cdn.jsdelivr.net; " +
                "connect-src 'self';";

            // Bỏ header nhận diện máy chủ để tránh bị dò quét lỗ hổng
            headers.Remove("Server");
            headers.Remove("X-Powered-By");

            await _next(context);
        }
    }
}
