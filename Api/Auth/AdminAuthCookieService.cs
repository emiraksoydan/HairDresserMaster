using Core.Utilities.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Api.Auth
{
    public interface IAdminAuthCookieService
    {
        void SetSessionCookie(HttpContext httpContext, string rawSessionToken, DateTime expiresUtc);
        void ClearSessionCookie(HttpContext httpContext);
        string? GetSessionToken(HttpContext httpContext);
    }

    public sealed class AdminAuthCookieService(IOptions<AdminAuthCookieSettings> options) : IAdminAuthCookieService
    {
        private readonly AdminAuthCookieSettings _settings = options.Value;

        public void SetSessionCookie(HttpContext httpContext, string rawSessionToken, DateTime expiresUtc)
        {
            var opts = BuildOptions(expiresUtc);
            httpContext.Response.Cookies.Append(_settings.SessionCookieName, rawSessionToken, opts);
        }

        public void ClearSessionCookie(HttpContext httpContext)
        {
            var expired = BuildOptions(DateTime.UtcNow.AddDays(-1));
            httpContext.Response.Cookies.Append(_settings.SessionCookieName, string.Empty, expired);
            // Eski ikili cookie modelinden kalanları da temizle
            httpContext.Response.Cookies.Append("gm_admin_access", string.Empty, expired);
            httpContext.Response.Cookies.Append("gm_admin_refresh", string.Empty, expired);
        }

        public string? GetSessionToken(HttpContext httpContext)
        {
            return httpContext.Request.Cookies[_settings.SessionCookieName];
        }

        private CookieOptions BuildOptions(DateTime expiresUtc)
        {
            var opts = new CookieOptions
            {
                HttpOnly = true,
                Secure = _settings.UseSecure,
                SameSite = SameSiteMode.None,
                Path = _settings.Path,
                Expires = new DateTimeOffset(expiresUtc),
            };

            if (!string.IsNullOrWhiteSpace(_settings.CookieDomain))
                opts.Domain = _settings.CookieDomain;

            return opts;
        }
    }
}
