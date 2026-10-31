using Hangfire.Annotations;
using Hangfire.Dashboard;
using ManagerStudentCaltholic.Models.Entities;

namespace ManagerStudentCaltholic.Security
{
    public class HangfireAdminAuthorizationFilter : IDashboardAuthorizationFilter
    {
        public bool Authorize([NotNull] DashboardContext context)
        {
            var httpContext = context.GetHttpContext();
            if (httpContext.User?.Identity == null || !httpContext.User.Identity.IsAuthenticated)
            {
                return false;
            }

            return httpContext.User.IsInRole(UserRole.Admin);
        }
    }
}