using ManagerStudentCaltholic.Models.Entities;

namespace ManagerStudentCaltholic.Models.ViewModels
{
    public class MasterTabDto
    {
        public string MasterCode { get; set; } = "COMMON";
        public string Title { get; set; } = string.Empty;
        public string ShortTitle { get; set; } = string.Empty;
        public string IconClass { get; set; } = "bi-grid";
        public string Description { get; set; } = string.Empty;
        public List<FeatureActionCardDto> Features { get; set; } = new();
    }

    public class FeatureActionCardDto
    {
        public string FeatureCode { get; set; } = string.Empty;
        public string MasterCode { get; set; } = "COMMON";
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string IconClass { get; set; } = "bi-circle";
        public string TargetUrl { get; set; } = "/";
        public string BadgeColor { get; set; } = "primary";
        public bool SacramentClassesOnly { get; set; }
    }

    public class MasterWorkspaceViewModel
    {
        public string ActiveMasterCode { get; set; } = "COMMON";
        public bool IsAuthenticated { get; set; }
        public string CurrentUserFullName { get; set; } = "Khách / Phụ huynh";
        public string CurrentUserRoleDisplay { get; set; } = "Cổng thông tin chung";
        public string CurrentAcademicYearName { get; set; } = "Chưa thiết lập";

        // Chỉ số KPI nhanh trên HomePage
        public int TotalStudents { get; set; }
        public int TotalClasses { get; set; }
        public int TotalEnrolled { get; set; }
        public int PendingRegistrations { get; set; }

        public List<string> AssignedClassNames { get; set; } = new();
        public List<MasterTabDto> Masters { get; set; } = new();
        public List<Announcement> PinAnnouncements { get; set; } = new();

        // FEAT-P4-07: Dữ liệu học sinh & thông báo lớp được nhận diện từ thiết bị đã tra cứu
        public DeviceDetectedStudentContextViewModel DeviceStudentContext { get; set; } = new();
    }

    public class FeaturePermissionManageViewModel
    {
        public List<FeaturePermissionRule> Rules { get; set; } = new();
        public List<ClassRoom> CurrentYearClasses { get; set; } = new();
    }

    public class SaveFeaturePermissionItemDto
    {
        public int Id { get; set; }
        public List<string> AllowedRoles { get; set; } = new();
        public bool SacramentClassesOnly { get; set; }
        public string? SpecificClassIdsCsv { get; set; }
        public bool IsEnabled { get; set; }
        public int SortOrder { get; set; }
    }
}