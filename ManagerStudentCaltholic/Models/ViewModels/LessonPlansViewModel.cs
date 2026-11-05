using ManagerStudentCaltholic.Models.DTOs;
using ManagerStudentCaltholic.Models.Entities;

namespace ManagerStudentCaltholic.Models.ViewModels
{
    public class LessonPlanWorkspaceViewModel
    {
        public List<ClassRoom> Classes { get; set; } = new();
        public AcademicYear? CurrentYear { get; set; }
        public int SelectedClassId { get; set; }
        public string SelectedClassName { get; set; } = string.Empty;
        public string SelectedGrade { get; set; } = string.Empty;
        public bool CanManage { get; set; }

        // FEAT-P5-07: Kế hoạch phân phối chương trình trực tuyến & lịch sử phiên bản
        public ClassCurriculumDocumentDto? CurriculumDocument { get; set; }
        public List<ClassCurriculumVersionDto> Versions { get; set; } = new();

        // Danh sách tài liệu đính kèm truyền thống (PDF, Word,...)
        public List<ClassLessonDocument> LegacyDocuments { get; set; } = new();
    }
}
