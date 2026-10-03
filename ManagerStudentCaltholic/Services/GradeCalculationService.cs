using ManagerStudentCaltholic.Models.Entities;

namespace ManagerStudentCaltholic.Services
{
    public interface IGradeCalculationService
    {
        decimal? CalculateSemesterAverage(Dictionary<int, decimal?> scores, List<GradeConfiguration> configs);
        decimal? CalculateYearAverage(decimal? hk1Average, decimal? hk2Average);
        (string AcademicRank, bool IsEligibleForSacrament) EvaluateStudent(decimal? averageScore, double attendanceRate, string gradeLevel, int semester);
    }

    public class GradeCalculationService : IGradeCalculationService
    {
        public decimal? CalculateSemesterAverage(Dictionary<int, decimal?> scores, List<GradeConfiguration> configs)
        {
            if (configs == null || !configs.Any()) return null;

            decimal totalWeightedScore = 0;
            int totalWeight = 0;
            int countValid = 0;

            foreach (var col in configs)
            {
                if (scores.TryGetValue(col.Id, out var score) && score.HasValue)
                {
                    totalWeightedScore += score.Value * col.WeightFactor;
                    totalWeight += col.WeightFactor;
                    countValid++;
                }
            }

            if (totalWeight == 0 || countValid == 0) return null;

            return Math.Round(totalWeightedScore / totalWeight, 2, MidpointRounding.AwayFromZero);
        }

        public decimal? CalculateYearAverage(decimal? hk1Average, decimal? hk2Average)
        {
            if (!hk1Average.HasValue && !hk2Average.HasValue) return null;
            if (hk1Average.HasValue && !hk2Average.HasValue) return hk1Average;
            if (!hk1Average.HasValue && hk2Average.HasValue) return hk2Average;

            // Công thức chuẩn: (HK1 + HK2 * 2) / 3
            var yearAvg = (hk1Average!.Value + (hk2Average!.Value * 2)) / 3.0m;
            return Math.Round(yearAvg, 2, MidpointRounding.AwayFromZero);
        }

        public (string AcademicRank, bool IsEligibleForSacrament) EvaluateStudent(
            decimal? averageScore, double attendanceRate, string gradeLevel, int semester)
        {
            if (!averageScore.HasValue)
            {
                return ("Chưa đủ điểm", false);
            }

            var score = averageScore.Value;
            string rank;

            // Tiêu chuẩn xếp loại kết hợp chuyên cần (TASK-1003)
            if (score >= 9.0m && attendanceRate >= 90.0) rank = "Xuất sắc";
            else if (score >= 8.0m && attendanceRate >= 80.0) rank = "Giỏi";
            else if (score >= 6.5m && attendanceRate >= 70.0) rank = "Khá";
            else if (score >= 5.0m && attendanceRate >= 60.0) rank = "Trung bình";
            else rank = "Yếu / Ở lại lớp";

            // Điều kiện lãnh Bí tích: ĐTB >= 5.0 và Chuyên cần >= 80%
            bool eligible = score >= 5.0m && attendanceRate >= 80.0;

            return (rank, eligible);
        }
    }
}