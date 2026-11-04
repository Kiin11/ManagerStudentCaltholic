using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;

namespace ManagerStudentCaltholic.Extensions
{
    /// <summary>
    /// FEAT-P5-02 / TASK-P5-02.1: Bộ nhận diện thiết bị thông minh (Mobile vs Desktop)
    /// Hỗ trợ chuyển đổi giao diện Mobile-First và Desktop linh hoạt.
    /// </summary>
    public static class HttpRequestExtensions
    {
        public const string CookieDeviceMode = "Parish_Device_Mode";

        private static readonly Regex MobileRegex = new Regex(
            @"(android|bb\d+|meego).+mobile|avantgo|bada\/|blackberry|blazer|compal|elaine|fennec|hiptop|iemobile|ip(hone|od)|iris|kindle|lge |maemo|midp|mmp|mobile.+firefox|netfront|opera m(ob|in)i|palm( os)?|phone|p(ixi|re)\/|plucker|pocket|psp|series(4|6)0|symbian|treo|up\.(browser|link)|vodafone|wap|windows ce|xda|xiino",
            RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled,
            TimeSpan.FromMilliseconds(50));

        private static readonly Regex MobilePrefixRegex = new Regex(
            @"1207|6310|6590|3gso|4thp|50[1-6]i|770s|802s|a wa|abac|ac(er|oo|s\-)|ai(ko|rn)|al(av|ca|co)|amoi|an(ex|ny|yw)|aptu|ar(ch|go)|as(te|us)|attw|au(di|\-m|r |s )|avan|be(ck|ll|nq)|bi(lb|rd)|bl(ac|az)|br(e|v)w|bumb|bw\-(n|u)|c55\/|capi|ccwa|cdm\-|cell|chtm|cldc|cmd\-|co(mp|nd)|craw|da(it|ll|ng)|dbte|dc\-s|devi|dica|dmob|do(c|p)o|ds(12|\-d)|el(49|ai)|em(l2|ul)|er(ic|k0)|esl8|ez([4-7]0|os|wa|ze)|fetc|fly(\-|_)|g1 u|g560|gene|gf\-5|g\-mo|go(\.w|od)|gr(ad|un)|haie|hcit|hd\-(m|p|t)|hei\-|hi(pt|ta)|hp( i|ip)|hs\-c|ht(c(\-| |_|a|g|p|s|t)|tp)|hu(aw|tc)|i\-(20|go|ma)|i230|iac( |\-|\/)|ibro|idea|ig01|ikom|im1k|inno|ipaq|iris|ja(t|v)a|jbro|jemu|jigs|kddi|keji|kgt( |\/)|klstr|kj\-o|kpt |kwc\-|kyo(c|k)|le(no|xi)|lg( g|\/(k|l|u)|50|54|\-[a-w])|libw|lynx|m1\-w|m3ga|m50\/|ma(te|ui|xo)|mc(01|21|ca)|m\-cr|me(rc|ri)|mi(o8|oa|ts)|mmef|mo(01|02|bi|de|do|t(\-| |o|v)|zz)|mt(50|p1|v )|mwbp|mywa|n10[0-2]|n20[2-3]|n30(0|2)|n50(0|2|5)|n7(0(0|1)|10)|ne((c|m)\-|on|tf|wf|wg|wt)|nok(6|i)|nzph|o2im|op(ti|wv)|oran|owg1|p800|pan(a|d|t)|pdxg|pg(13|\-([1-8]|c))|phil|pire|pl(ay|uc)|pn\-2|po(ck|rt|se)|prox|psio|pt\-g|qa\-a|qc(07|12|21|32|60|\-[2-7]|i\-)|qtek|r380|r600|raks|rim9|ro(ve|zo)|s55\/|sa(ge|ma|mm|ms|ny|va)|sc(01|h\-|oo|p\-)|sdk\/|se(c(\-|0|1)|47|mc|nd|ri)|sgh\-|shar|sie(\-|m)|sk\-0|sl(45|id)|sm(al|ar|b3|it|t5)|so(ft|ny)|sp(01|h\-|v\-|v )|sy(01|mb)|t2(18|50)|t6(00|10|18)|ta(gt|lk)|tcl\-|tdg\-|tel(i|m)|tim\-|t\-mo|to(pl|sh)|ts(70|m\-|m3|m5)|tx\-9|up(\.b|g1|si)|utst|v400|v750|veri|vi(rg|te)|vk(40|5[0-3]|\-v)|vm40|voda|vulc|vx(52|53|60|61|70|80|81|83|85|98)|w3c(\-| )|webc|whit|wi(g |nc|nw)|wmlb|wonu|x700|yas\-|your|zeto|zte\-",
            RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled,
            TimeSpan.FromMilliseconds(50));

        /// <summary>
        /// TASK-P5-02.1: Nhận diện thiết bị Mobile (Điện thoại / Tablet màn hình nhỏ / PWA Mobile)
        /// Ưu tiên:
        /// 1. Query parameter ?device=mobile hoặc ?device=desktop (dành cho kiểm thử hoặc chuyển đổi nhanh)
        /// 2. Cookie Parish_Device_Mode ("mobile" hoặc "desktop")
        /// 3. Header Sec-CH-UA-Mobile (?1)
        /// 4. User-Agent regex phát hiện thiết bị di động
        /// </summary>
        public static bool IsMobileDevice(this HttpRequest request)
        {
            if (request == null) return false;

            // 1. Kiểm tra query param ?device=mobile hoặc ?device=desktop
            if (request.Query.TryGetValue("device", out var queryMode))
            {
                var qStr = queryMode.ToString().ToLowerInvariant();
                if (qStr == "mobile") return true;
                if (qStr == "desktop") return false;
            }

            // 2. Kiểm tra Cookie lưu chế độ người dùng đã chủ động chọn
            if (request.Cookies.TryGetValue(CookieDeviceMode, out var cookieMode))
            {
                var cStr = cookieMode?.ToLowerInvariant();
                if (cStr == "mobile") return true;
                if (cStr == "desktop") return false;
            }

            // 3. Client Hints HTTP header hiện đại
            if (request.Headers.TryGetValue("Sec-CH-UA-Mobile", out var secChMobile) && secChMobile == "?1")
            {
                return true;
            }

            // 4. Kiểm tra User-Agent
            var userAgent = request.Headers.UserAgent.ToString();
            if (string.IsNullOrWhiteSpace(userAgent))
            {
                return false;
            }

            try
            {
                if (MobileRegex.IsMatch(userAgent))
                {
                    return true;
                }

                if (userAgent.Length >= 4 && MobilePrefixRegex.IsMatch(userAgent.Substring(0, 4)))
                {
                    return true;
                }
            }
            catch
            {
                // Timeout an toàn khi regex quá dài
            }

            return false;
        }
    }
}
