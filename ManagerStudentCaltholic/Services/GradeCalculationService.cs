using ManagerStudentCaltholic.Interface.Services;
using ManagerStudentCaltholic.Models.Entities;
using System.Linq.Expressions;
using System.Text.Json;
using NCalc;

namespace ManagerStudentCaltholic.Services
{
    public class GradeCalculationService : IGradeCalculationService
    {
        public decimal CalculateAttendanceScore(int absentMassUnexcused, int absentMassExcused, int lateMass, int absentClassUnexcused, int absentClassExcused, int lateClass, AttendanceScoreRule rule)
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

        public decimal? EvaluateGradeFormula(string formulaExpression, Dictionary<string, decimal> scores)
        {
            try
            {
                var expr = new NCalc.Expression(formulaExpression);
                foreach (var kvp in scores)
                {
                    expr.Parameters[kvp.Key] = (double)kvp.Value;
                }
                var result = expr.Evaluate();
                return Math.Round(Convert.ToDecimal(result), 2);
            }
            catch
            {
                return null;
            }
        }


        // 3. Tính Điểm Chuyên Cần theo quy ước Điểm Gốc - Điểm Trừ + Điểm Cộng
        //public decimal CalculateAttendanceScore(List<Attendance> attendances, AttendanceRuleConfig rule)
        //{
        //    decimal score = rule.BaseAttendanceScore;

        //    // Nghỉ học
        //    int unpermittedClass = attendances.Count(a => a.AttendanceDate.DayOfWeek == DayOfWeek.Sunday && a.ClassStatus == "ABSENT_UNPERMITTED");
        //    int permittedClass = attendances.Count(a => a.AttendanceDate.DayOfWeek == DayOfWeek.Sunday && a.ClassStatus == "ABSENT_PERMITTED");
        //    score -= (unpermittedClass * rule.UnpermittedClassDeduction);
        //    score -= (permittedClass * rule.PermittedClassDeduction);

        //    // Nghỉ Lễ Chúa Nhật
        //    int unpermittedSunMass = attendances.Count(a => a.AttendanceDate.DayOfWeek == DayOfWeek.Sunday && a.MassStatus == "ABSENT_UNPERMITTED");
        //    int permittedSunMass = attendances.Count(a => a.AttendanceDate.DayOfWeek == DayOfWeek.Sunday && a.MassStatus == "ABSENT_PERMITTED");
        //    score -= (unpermittedSunMass * rule.UnpermittedSunMassDeduction);
        //    score -= (permittedSunMass * rule.PermittedSunMassDeduction);

        //    // Tham dự Lễ Thứ Năm (Cộng điểm thưởng lũy kế nhiều mức)
        //    int thuAttendedCount = attendances.Count(a => a.AttendanceDate.DayOfWeek == DayOfWeek.Thursday && a.MassAttended);
        //    if (!string.IsNullOrEmpty(rule.ThuMassBonusTiersJson))
        //    {
        //        try
        //        {
        //            var tiers = JsonSerializer.Deserialize<List<ThuBonusTier>>(rule.ThuMassBonusTiersJson)
        //                            ?.OrderByDescending(t => t.MinDays).ToList();
        //            if (tiers != null)
        //            {
        //                var matchedTier = tiers.FirstOrDefault(t => thuAttendedCount >= t.MinDays);
        //                if (matchedTier != null)
        //                {
        //                    score += matchedTier.Bonus;
        //                }
        //            }
        //        }
        //        catch { }
        //    }

        //    // Giới hạn thang điểm từ 0.0 đến 10.0
        //    if (score < 0m) score = 0m;
        //    if (score > 10m) score = 10m;

        //    return Math.Round(score, 2);
        //}

        public (string AcademicRank, bool IsEligibleForSacrament) EvaluateStudent(
            decimal? averageScore, decimal attendanceScore, string gradeLevel)
        {
            if (!averageScore.HasValue) return ("Chưa đủ điểm", false);

            var avg = averageScore.Value;
            string rank;

            // Tiêu chuẩn xếp loại kết hợp Điểm TB & Điểm Chuyên cần
            if (avg >= 9.0m && attendanceScore >= 9.0m) rank = "Xuất sắc";
            else if (avg >= 8.0m && attendanceScore >= 8.0m) rank = "Giỏi";
            else if (avg >= 6.5m && attendanceScore >= 6.5m) rank = "Khá";
            else if (avg >= 5.0m && attendanceScore >= 5.0m) rank = "Trung bình";
            else rank = "Yếu / Ở lại lớp";

            bool eligible = avg >= 5.0m && attendanceScore >= 7.0m;
            return (rank, eligible);
        }
    }
}