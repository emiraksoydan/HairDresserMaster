using Core.Utilities.Security.JWT;

namespace Api.Auth
{
  internal static class AdminAuthResponseHelper
  {
    /// <summary>Token'ları HttpOnly cookie'de tut; JSON'da ham token döndürme.</summary>
    public static AccessToken SanitizeForJson(AccessToken access)
    {
      return new AccessToken
      {
        Token = string.Empty,
        RefreshToken = string.Empty,
        Expiration = access.Expiration,
        RefreshTokenExpires = access.RefreshTokenExpires,
        ShowHelpGuideOnboarding = access.ShowHelpGuideOnboarding,
        AdminId = access.AdminId,
        AdminEmail = access.AdminEmail,
        AdminFullName = access.AdminFullName,
        AdminProfileImageUrl = access.AdminProfileImageUrl,
      };
    }
  }
}
