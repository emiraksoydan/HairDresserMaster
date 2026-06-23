using System.Security.Claims;
using System.Text.Encodings.Web;
using Core.Extensions;
using Core.Utilities.Configuration;
using Core.Utilities.Security;
using DataAccess.Abstract;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Api.Auth
{
    /// <summary>Admin panel tek session cookie — JWT access token yerine opaque session.</summary>
    public sealed class AdminSessionAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IOptions<AdminAuthCookieSettings> cookieSettings,
        IAdminUserDal adminUserDal) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        private readonly AdminAuthCookieSettings _cookieSettings = cookieSettings.Value;

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var rawSession = Request.Cookies[_cookieSettings.SessionCookieName];
            if (string.IsNullOrWhiteSpace(rawSession))
                return AuthenticateResult.NoResult();

            var hash = AdminSessionTokenHelper.HashToken(rawSession);
            var admin = await adminUserDal.GetByRefreshTokenHash(hash);
            if (admin == null || !admin.IsActive
                || admin.RefreshTokenExpiresAt == null
                || admin.RefreshTokenExpiresAt < DateTime.UtcNow)
                return AuthenticateResult.Fail("Admin session invalid or expired.");

            var id = admin.Id.ToString();
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, id),
                new("identifier", id),
                new("sub", id),
                new(ClaimTypes.Name, admin.FullName ?? "Admin"),
                new(ClaimTypes.Role, "Admin"),
                new("email", admin.Email),
            };

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
        }
    }
}
