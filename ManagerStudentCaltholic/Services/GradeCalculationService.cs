using System.Text.Json;
using ManagerStudentCaltholic.Interface.Services;
using ManagerStudentCaltholic.Models.Entities;
using NCalc;

namespace ManagerStudentCaltholic.Services
{
    public class GradeCalculationService : IGradeCalculationService
    {
        public decimal CalculateAttendanceScore(
            int absentMassUnexcused, int absentMassExcused, int lateMass,
            int absentClassUnexcused, int absentClassExcused, int lateClass,
            AttendanceScoreRule rule)
        {
            var totalPenalty = (absentMassUnexcused * rule.PenaltyMassUnexcused)
                             + (absentMassExcused * rule.PenaltyMassExcused)
                             + (lateMass * rule.PenaltyMassLate)
                             + (absentClassUnexcused * rule.PenaltyClassUnexcused)
                             + (absentClassExcused * rule.PenaltyClassExcused)
                             + (lateClass * rule.PenaltyClassLate);

            var finalScore = rule.BaseScore - totalPenalty;
            return Math.Round(Math.Max(rule.MinScore, finalScore), 2);
        }

        // 1. Tính ĐTB Học Kỳ: Tổng(Điểm * Hệ số) / Tổng Hệ số
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

        // 2. Tính ĐTB Cả Năm: (ĐTB_HK1 * 1 + ĐTB_HK2 * 2) / 3 (hoặc theo hệ số cấu hình)
        public decimal? CalculateYearAverage(decimal? hk1Average, decimal? hk2Average, int hk1Weight = 1, int hk2Weight = 2)
        {
            if (!hk1Average.HasValue && !hk2Average.HasValue) return null;
            if (hk1Average.HasValue && !hk2Average.HasValue) return hk1Average;
            if (!hk1Average.HasValue && hk2Average.HasValue) return hk2Average;

            var totalWeighted = (hk1Average!.Value * hk1Weight) + (hk2Average!.Value * hk2Weight);
            return Math.Round(totalWeighted / (hk1Weight + hk2Weight), 2, MidpointRounding.AwayFromZero);
        }

        // 3. Xếp loại dựa trên cấu hình ngưỡng JSON
        public string DetermineClassification(decimal average, string evaluationRulesJson)
        {
            try
            {
                var thresholds = JsonSerializer.Deserialize<Dictionary<string, decimal>>(evaluationRulesJson);
                if (thresholds == null) return "Chưa xếp loại";

                if (thresholds.TryGetValue("Gioi", out var g) && average >= g) return "Giỏi";
                if (thresholds.TryGetValue("Kha", out var k) && average >= k) return "Khá";
                if (thresholds.TryGetValue("Dat", out var d) && average >= d) return "Đạt";
                return "Yếu";
            }
            catch
            {
                return "Chưa xếp loại";
            }
        }

        // 4. Tính điểm theo biểu thức động NCalc (An toàn với cột thiếu điểm và lỗi chia cho 0)
        public decimal? EvaluateGradeFormula(
            string formulaExpression,
            Dictionary<string, decimal> scores,
            IEnumerable<string>? allColumnCodes = null)
        {
            if (string.IsNullOrWhiteSpace(formulaExpression)) return null;

            try
            {
                var expr = new Expression(formulaExpression);

                if (allColumnCodes != null)
                {
                    foreach (var code in allColumnCodes)
                    {
                        expr.Parameters[code] = 0.0;
                    }
                }

                foreach (var kvp in scores)
                {
                    expr.Parameters[kvp.Key] = (double)kvp.Value;
                }

                var result = expr.Evaluate();
                if (result == null) return null;

                double numericResult = Convert.ToDouble(result);
                if (double.IsNaN(numericResult) || double.IsInfinity(numericResult))
                    return null;

                return Math.Round(Convert.ToDecimal(numericResult), 2, MidpointRounding.AwayFromZero);
            }
            catch
            {
                return null;
            }
        }

        // 5. Đánh giá Học lực & Điều kiện lãnh Bí tích / Lên lớp
        public (string AcademicRank, bool IsEligibleForSacrament) EvaluateStudent(
            decimal? averageScore,
            double combinedAttendanceRate,
            string gradeLevel,
            int semester = 0)
        {
            if (!averageScore.HasValue) return ("Chưa đủ điểm", false);

            var avg = averageScore.Value;
            string rank;

            if (avg >= 9.0m && combinedAttendanceRate >= 90.0) rank = "Xuất sắc";
            else if (avg >= 8.0m && combinedAttendanceRate >= 80.0) rank = "Giỏi";
            else if (avg >= 6.5m && combinedAttendanceRate >= 70.0) rank = "Khá";
            else if (avg >= 5.0m && combinedAttendanceRate >= 60.0) rank = "Trung bình";
            else rank = "Yếu / Ở lại lớp";

            // Điều kiện lãnh Bí tích / Lên lớp: ĐTB >= 5.0 và Chuyên cần >= 80%
            bool eligible = false;//avg >= 5.0m && combinedAttendanceRate >= 80.0;
            return (rank, eligible);
        }
    }
}