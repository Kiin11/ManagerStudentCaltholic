namespace ManagerStudentCaltholic.Interface.Services
{
    public interface IStudentPlacementService
    {
        Task<int?> DetermineBestClassForNewStudentAsync(int academicYearId, string gradeLevel, string gender);

        /// <summary>
        /// Phân bổ hàng loạt học sinh lên lớp vào các lớp đích sao cho cân bằng sĩ số và tỷ lệ Nam/Nữ (TASK-P3-01.3)
        /// </summary>
        Task<Dictionary<long, int>> BatchAssignToTargetClassesAsync(
            List<long> studentIds,
            int targetYearId,
            string targetGradeLevel,
            string? preferredClassPrefix = null);
    }
}
