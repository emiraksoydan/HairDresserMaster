using System;

namespace Business.Utilities
{
    /// <summary>OTP SMS gövdesi — uygulama diline göre (frontend Language ile uyumlu).</summary>
    public static class OtpSmsTemplate
    {
        public static string BuildMessage(string? language, string otpCode, int validitySeconds)
        {
            var lang = NormalizeLanguage(language);
            var minutes = Math.Max(1, (int)Math.Ceiling(validitySeconds / 60.0));

            return lang switch
            {
                "en" => minutes == 1
                    ? $"Your verification code: {otpCode}. Do not share it with anyone. Valid for 1 minute."
                    : $"Your verification code: {otpCode}. Do not share it with anyone. Valid for {minutes} minutes.",
                "de" => minutes == 1
                    ? $"Ihr Bestätigungscode: {otpCode}. Geben Sie ihn nicht weiter. Gültig für 1 Minute."
                    : $"Ihr Bestätigungscode: {otpCode}. Geben Sie ihn nicht weiter. Gültig für {minutes} Minuten.",
                "ar" => minutes == 1
                    ? $"رمز التحقق: {otpCode}. لا تشاركه مع أحد. صالح لمدة دقيقة واحدة."
                    : $"رمز التحقق: {otpCode}. لا تشاركه مع أحد. صالح لمدة {minutes} دقائق.",
                _ => minutes == 1
                    ? $"Doğrulama kodunuz: {otpCode}. Bu kodu kimseyle paylaşmayın. Geçerlilik süresi 1 dakikadır."
                    : $"Doğrulama kodunuz: {otpCode}. Bu kodu kimseyle paylaşmayın. Geçerlilik süresi {minutes} dakikadır.",
            };
        }

        private static string NormalizeLanguage(string? language)
        {
            if (string.IsNullOrWhiteSpace(language))
                return "tr";
            var s = language.Trim().ToLowerInvariant();
            if (s.Length >= 2)
                s = s[..2];
            return s switch
            {
                "en" or "de" or "ar" => s,
                _ => "tr",
            };
        }
    }
}
