using ManagerStudentCaltholic.Models.Entities;
using Microsoft.AspNetCore.Authorization;

namespace ManagerStudentCaltholic.Security
{
    public class GradeScopeRequirement : IAuthorizationRequirement { }

    public class GradeScopeHandler : AuthorizationHandler<GradeScopeRequirement, string>
    {
        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            GradeScopeRequirement requirement,
            string targetGradeLevel)
        {
            // Admin, Cha Tuyên Úy, Ban Điều Hành có toàn quyền trên mọi khối
            if (context.User.IsInRole(UserRole.Admin) ||
                context.User.IsInRole(UserRole.SpiritualDirector) ||
                context.User.IsInRole(UserRole.ExecutiveBoard))
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }

            // Nếu là Trưởng khối, kiểm tra khối phụ trách có trùng khớp không
            if (context.User.IsInRole(UserRole.BranchHead))
            {
                var managedGrade = context.User.FindFirst("ManagedGradeLevel")?.Value;
                if (!string.IsNullOrEmpty(managedGrade) &&
                    string.Equals(managedGrade, targetGradeLevel, StringComparison.OrdinalIgnoreCase))
                {
                    context.Succeed(requirement);
                }
            }

            return Task.CompletedTask;
        }
    }
}
