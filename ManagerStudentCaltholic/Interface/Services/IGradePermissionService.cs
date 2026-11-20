using ManagerStudentCaltholic.Models.Entities;

namespace ManagerStudentCaltholic.Interface.Services
{
    public interface IGradePermissionService
    {
        /// <summary>
        /// Kiểm tra người dùng có quyền quản lý lớp (GLV Dự Bị chỉ được thao tác lớp được phân công thuộc Ca Sáng).
        /// </summary>
        Task<bool> CanManageClassAsync(string userId, int classId, string academicYear, bool isAdmin);

        /// <summary>
        /// Lấy danh sách ID các lớp được phép thao tác (GLV Dự Bị bị lọc chỉ còn lớp Ca Sáng).
        /// </summary>
        Task<List<int>> GetAssignedClassIdsAsync(string userId, string academicYear, bool isAdmin);

        Task<List<GradeColumn>> GetEffectiveColumnsAsync(int classId, string academicYear, int semester);
        Task<GradingFormula?> GetEffectiveFormulaAsync(int classId, string academicYear, int semester);
    }
}
