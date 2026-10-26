using ManagerStudentCaltholic.Models.DTOs;
using ManagerStudentCaltholic.Models.Entities;

namespace ManagerStudentCaltholic.Models.ViewModels
{
    public class PromotionPreviewViewModel
    {
        public int SourceYearId { get; set; }
        public string SourceYearName { get; set; } = string.Empty;

        public int SourceClassId { get; set; }
        public string SourceClassName { get; set; } = string.Empty;
        public string SourceGradeLevel { get; set; } = string.Empty;

        public int TargetYearId { get; set; }
        public string TargetYearName { get; set; } = string.Empty;

        // Ngưỡng xét lên lớp linh hoạt
        public decimal MinYearAverage { get; set; } = 5.0m;
        public double MinAttendanceRate { get; set; } = 60.0;

        // Cờ bật/tắt xét Bí tích (Mặc định false theo yêu cầu hiện tại)
        public bool EnableSacramentEvaluation { get; set; } = false;

        // Danh sách lớp thuộc Niên khóa mới để chọn làm Lớp Đích
        public List<ClassRoom> TargetYearClasses { get; set; } = new();

        // Gợi ý ID lớp đích khi Lên lớp và khi Ở lại lớp
        public int? SuggestedPromoteClassId { get; set; }
        public int? SuggestedRetainClassId { get; set; }

        public List<StudentPromotionRowDto> Students { get; set; } = new();

        public int TotalStudents => Students.Count;
        public int PassedCount => Students.Count(s => s.IsPromoted);
        public int RetainedCount => Students.Count(s => !s.IsPromoted);
    }
}
