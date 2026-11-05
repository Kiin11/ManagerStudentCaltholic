namespace ManagerStudentCaltholic.Models.DTOs
{
    public class CurriculumItemDto
    {
        public int WeekNumber { get; set; } = 1;
        public string? LessonDateStr { get; set; } // dd/MM/yyyy
        public DateTime? LessonDate { get; set; }
        public string? LiturgicalDay { get; set; } // Ví dụ: "CN 24 TN - Khai giảng"
        public string? LessonCode { get; set; } // "BÀI 1", "KT 1T", "ÔN TẬP"
        public string TopicTitle { get; set; } = string.Empty; // Tiêu đề bài học
        public string? KeyPoints { get; set; } // Trọng tâm giáo lý
        public string? AssignedTeacherName { get; set; } // GLV phụ trách
        public long? AssignedTeacherUserId { get; set; }
        public string? EventNotes { get; set; } // Ghi chú sự kiện
        public bool IsExamDay { get; set; } = false; // Đánh dấu ngày kiểm tra / thi
        public bool IsDayOff { get; set; } = false; // Nghỉ lễ / Không học
        public int Semester { get; set; } = 1; // 1: HK1, 2: HK2
    }

    public class ClassCurriculumDocumentDto
    {
        public long Id { get; set; }
        public int ClassRoomId { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public string GradeLevel { get; set; } = string.Empty;
        public int AcademicYearId { get; set; }
        public string AcademicYearName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string CurrentVersion { get; set; } = "v1";
        public int VersionNumber { get; set; } = 1;
        public string? LastAttachmentFileName { get; set; }
        public string? LastAttachmentFilePath { get; set; }
        public long LastAttachmentFileSize { get; set; }
        public string? UpdatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int TotalWeeks { get; set; }
        public List<CurriculumItemDto> Items { get; set; } = new();
    }

    public class ClassCurriculumVersionDto
    {
        public long Id { get; set; }
        public long ClassCurriculumDocumentId { get; set; }
        public int VersionNumber { get; set; }
        public string VersionTag { get; set; } = string.Empty;
        public string? ChangeSummary { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public int TotalWeeks { get; set; }
        public List<CurriculumItemDto>? Items { get; set; }
    }

    public class SaveCurriculumRequestDto
    {
        public int ClassRoomId { get; set; }
        public int? AcademicYearId { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? ChangeSummary { get; set; }
        public List<CurriculumItemDto> Items { get; set; } = new();
    }

    public class CurriculumParseResultDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? OriginalFileName { get; set; }
        public int TotalItemsParsed { get; set; }
        public List<CurriculumItemDto> Items { get; set; } = new();
    }
}
