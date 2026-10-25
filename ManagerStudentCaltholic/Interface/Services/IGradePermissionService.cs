using ManagerStudentCaltholic.Models.Entities;

namespace ManagerStudentCaltholic.Interface.Services
{
    public interface IGradePermissionService
    {
        Task<bool> CanManageClassAsync(string userId, int classId, string academicYear, bool isAdmin);
        Task<List<int>> GetAssignedClassIdsAsync(string userId, string academicYear, bool isAdmin);
        Task<List<GradeColumn>> GetEffectiveColumnsAsync(int classId, string academicYear, int semester);
        Task<GradingFormula?> GetEffectiveFormulaAsync(int classId, string academicYear, int semester);
    }
}
