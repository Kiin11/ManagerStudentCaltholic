using ManagerStudentCaltholic.Data;
using Microsoft.EntityFrameworkCore;

namespace ManagerStudentCaltholic.Services
{
    /// <summary>
    /// Ngăn chặn tài khoản GLV (đặc biệt GLV Dự Bị có LinkedStudentId) tự điểm danh hoặc tự chấm điểm chính mình.
    /// </summary>
    public static class StaffSelfActionGuard
    {
        public static async Task<long?> GetLinkedStudentIdAsync(
            ParishDbContext context,
            long userId,
            CancellationToken cancellationToken = default)
        {
            if (userId <= 0)
                return null;

            return await context.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => u.LinkedStudentId ?? u.StudentId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public static bool IsSelfStudentAction(long? linkedStudentId, long targetStudentId)
        {
            return linkedStudentId.HasValue && linkedStudentId.Value > 0 && linkedStudentId.Value == targetStudentId;
        }
    }
}
