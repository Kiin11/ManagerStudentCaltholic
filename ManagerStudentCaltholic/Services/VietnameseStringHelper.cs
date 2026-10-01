using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ManagerStudentCaltholic.Services
{
    public static class VietnameseStringHelper
    {
        /// <summary>
        /// Loại bỏ dấu tiếng Việt, chuyển sang chữ thường không dấu và ký tự trắng
        /// </summary>
        public static string RemoveDiacritics(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;

            var normalizedString = text.Normalize(NormalizationForm.FormD);
            var stringBuilder = new StringBuilder();

            foreach (var c in normalizedString)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            var clean = stringBuilder.ToString().Normalize(NormalizationForm.FormC);
            clean = clean.Replace('đ', 'd').Replace('Đ', 'D');
            clean = Regex.Replace(clean, @"[^a-zA-Z0-9\s]", "");
            return clean.ToLowerInvariant();
        }
    }
}
