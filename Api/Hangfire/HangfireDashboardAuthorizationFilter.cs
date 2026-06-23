using Core.Extensions;
using Hangfire.Dashboard;

namespace Api.Hangfire
{
    /// <summary>Hangfire dashboard yalnızca Admin JWT ile erişilebilir.</summary>
    public sealed class HangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
    {
        public bool Authorize(DashboardContext context)
        {
            var httpContext = context.GetHttpContext();
            var user = httpContext.User;
            return user?.Identity?.IsAuthenticated == true && user.ClaimRoles().Contains("Admin");
        }
    }
}
