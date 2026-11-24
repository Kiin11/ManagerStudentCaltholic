namespace ManagerStudentCaltholic.Services
{
    /// <summary>
    /// Bộ chuẩn hóa tính tuổi và kiểm tra điều kiện >= 18 tuổi cho ngày Tuyên hứa Giáo lý viên,
    /// xử lý đồng nhất cả trường hợp sinh ngày nhuận 29/02 theo chuẩn DateTime.AddYears của .NET.
    /// </summary>
    public static class AgeValidationHelper
    {
        /// <summary>
        /// Tính tuổi chính xác tại một mốc ngày mục tiêu.
        /// Với người sinh 29/02/2008: dob.AddYears(18) = 28/02/2026 nên ngày 27/02/2026 trả về 17,
        /// từ 28/02/2026 trở đi trả về 18 — nhất quán 100% với IsAtLeastAge.
        /// </summary>
        public static int CalculateAgeAtDate(DateTime dateOfBirth, DateTime targetDate)
        {
            var dob = dateOfBirth.Date;
            var target = targetDate.Date;
            int age = target.Year - dob.Year;
            if (age > 0 && target < dob.AddYears(age))
            {
                age--;
            }
            else if (age < 0)
            {
                age = 0;
            }
            return age;
        }

        /// <summary>
        /// Kiểm tra targetDate >= dateOfBirth.AddYears(minimumYears) bằng AddYears chuẩn .NET.
        /// </summary>
        public static bool IsAtLeastAge(DateTime dateOfBirth, DateTime targetDate, int minimumYears = 18)
        {
            return targetDate.Date >= dateOfBirth.Date.AddYears(minimumYears);
        }
    }
}
