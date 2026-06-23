namespace Core.Utilities.Configuration
{
    /// <summary>Admin panel HttpOnly session cookie (tek oturum).</summary>
    public class AdminAuthCookieSettings
    {
        public const string SectionName = "AdminAuthCookies";

        public string SessionCookieName { get; set; } = "gm_admin_session";

        /// <summary>Prod: .gumusmakas.com.tr — dev'de boş bırakın (API host'a özel cookie).</summary>
        public string? CookieDomain { get; set; }

        public string Path { get; set; } = "/";

        public bool UseSecure { get; set; } = true;
    }
}
