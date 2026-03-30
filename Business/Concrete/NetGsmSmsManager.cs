using Business.Abstract;
using Business.Utilities;
using Core.Aspect.Autofac.ExceptionHandling;
using Core.Utilities.Results;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Business.Concrete
{
    /// <summary>
    /// Net GSM REST v2 OTP entegrasyonu.
    /// POST https://api.netgsm.com.tr/sms/rest/v2/otp
    /// Authentication: Basic Auth (Username / Password)
    /// Kod bizim tarafımızdan üretilir, SMS mesajı içinde gönderilir.
    /// Doğrulama IMemoryCache ile yapılır.
    /// </summary>
    public class NetGsmSmsManager : ISmsVerifyService
    {
        private const string OTP_ENDPOINT = "https://api.netgsm.com.tr/sms/rest/v2/otp";
        /// <summary>Kod geçerliliği; frontend OTP geri sayımı ile aynı (60 sn).</summary>
        private const int OTP_VALIDITY_SECONDS = 60;
        private const string CACHE_PREFIX = "otp_";
        private const string ATTEMPTS_PREFIX = "otp_attempts_";
        private const string RESEND_COOLDOWN_PREFIX = "otp_resend_cd_";
        private const int OTP_RESEND_COOLDOWN_SECONDS = 60;
        private const string DEV_OTP_CODE = "123456";
        private const int MAX_OTP_ATTEMPTS = 5;
        private const int OTP_ATTEMPTS_TTL_SECONDS = 600; // Deneme sayacı OTP'den bağımsız, 10 dk
        /// <summary>Aynı numaraya aynı saat dilimi içinde gönderilebilecek maksimum OTP sayısı.</summary>
        private const int MAX_HOURLY_OTP = 3;
        private const string HOURLY_PREFIX = "otp_hourly_";

        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _cache;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<NetGsmSmsManager> _logger;
        private readonly bool _enabled;

        public NetGsmSmsManager(
            IConfiguration configuration,
            IMemoryCache cache,
            IHttpClientFactory httpClientFactory,
            ILogger<NetGsmSmsManager> logger)
        {
            _configuration = configuration;
            _cache = cache;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _enabled = configuration.GetValue<bool>("NetGsm:Enabled", false);
        }

        [ExceptionHandlingAspect(customErrorMessage: "OTP gönderilemedi. Lütfen daha sonra tekrar deneyin.")]
        public async Task<IResult> SendAsync(string e164, string? language = null)
        {
            var cacheKey = CACHE_PREFIX + e164;
            var otpCode = GenerateOtpCode();

            // Saatlik limit kontrolü: aynı numara aynı saat diliminde MAX_HOURLY_OTP kadar OTP alabilir
            var nowUtc = DateTime.UtcNow;
            var hourKey = HOURLY_PREFIX + e164 + "_" + nowUtc.ToString("yyyyMMddHH");
            var hourlySent = _cache.TryGetValue(hourKey, out int hCount) ? hCount : 0;
            if (hourlySent >= MAX_HOURLY_OTP)
            {
                var nextHour = new DateTime(nowUtc.Year, nowUtc.Month, nowUtc.Day, nowUtc.Hour, 0, 0, DateTimeKind.Utc).AddHours(1);
                var waitMinutes = (int)Math.Ceiling((nextHour - nowUtc).TotalMinutes);
                return new ErrorResult($"Bu saat diliminde maksimum {MAX_HOURLY_OTP} kod hakkınızı kullandınız. Yeni saat başında ({waitMinutes} dakika sonra) tekrar deneyebilirsiniz.");
            }

            // Aynı numaraya çok sık SMS (modal kapat-aç / spam)
            var cooldownKey = RESEND_COOLDOWN_PREFIX + e164;
            if (_cache.TryGetValue(cooldownKey, out DateTime lastSendUtc))
            {
                var wait = OTP_RESEND_COOLDOWN_SECONDS - (int)(DateTime.UtcNow - lastSendUtc).TotalSeconds;
                if (wait > 0)
                {
                    return new ErrorResult(
                        $"Çok sık kod istediniz. Lütfen {wait} saniye sonra tekrar deneyin.");
                }
            }

            // Önceki aktif kodu ve deneme sayacını temizle (yeniden gönderim için)
            _cache.Remove(cacheKey);
            _cache.Remove(ATTEMPTS_PREFIX + e164);

            if (!_enabled)
            {
                // Development modu: SMS gönderilmez, sabit kod kullanılır
                _cache.Set(cacheKey, DEV_OTP_CODE, TimeSpan.FromSeconds(OTP_VALIDITY_SECONDS));
                _cache.Set(cooldownKey, DateTime.UtcNow, TimeSpan.FromSeconds(OTP_RESEND_COOLDOWN_SECONDS * 2));
                // Saatlik sayacı artır
                var nextHourBoundary = new DateTime(nowUtc.Year, nowUtc.Month, nowUtc.Day, nowUtc.Hour, 0, 0, DateTimeKind.Utc).AddHours(1);
                _cache.Set(hourKey, hourlySent + 1, nextHourBoundary);
                _logger.LogInformation("[NetGSM DEV] OTP kodu: {Code} | Numara: {Phone}", DEV_OTP_CODE, MaskPhone(e164));
                return new SuccessResult("OTP gönderildi.");
            }

            var username = _configuration["NetGsm:UserCode"] ?? "";
            var password = _configuration["NetGsm:Password"] ?? "";
            var msgHeader = _configuration["NetGsm:MsgHeader"] ?? "GUMUSMAKAS";
            var appName = _configuration["NetGsm:AppName"] ?? "Gümüş Makas";

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                _logger.LogError("[NetGSM] Yapılandırma eksik. UserCode veya Password boş.");
                return new ErrorResult("SMS servisi yapılandırılmamış.");
            }

            // E.164 (+905XXXXXXXXX) → Net GSM formatı (5XXXXXXXXX)
            var netGsmPhone = ToNetGsmPhone(e164);

            var smsBody = OtpSmsTemplate.BuildMessage(language, otpCode, OTP_VALIDITY_SECONDS);
            var body = new
            {
                msgheader = msgHeader,
                appname = appName,
                msg = smsBody,
                no = netGsmPhone
            };

            try
            {
                var client = _httpClientFactory.CreateClient("NetGsm");

                // Basic Auth header
                var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);

                var json = JsonSerializer.Serialize(body);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.PostAsync(OTP_ENDPOINT, content);
                var responseText = await response.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(responseText);
                var root = doc.RootElement;
                var code = root.GetProperty("code").GetString();

                if (code == "00")
                {
                    var jobId = root.TryGetProperty("jobid", out var j) ? j.GetString() : "-";
                    // Kodu cache'e yaz (doğrulama için)
                    _cache.Set(cacheKey, otpCode, TimeSpan.FromSeconds(OTP_VALIDITY_SECONDS));
                    _cache.Set(cooldownKey, DateTime.UtcNow, TimeSpan.FromSeconds(OTP_RESEND_COOLDOWN_SECONDS * 2));
                    // Saatlik sayacı artır
                    var nextHourBoundary = new DateTime(nowUtc.Year, nowUtc.Month, nowUtc.Day, nowUtc.Hour, 0, 0, DateTimeKind.Utc).AddHours(1);
                    _cache.Set(hourKey, hourlySent + 1, nextHourBoundary);
                    _logger.LogInformation("[NetGSM] OTP gönderildi. JobId: {JobId} | Numara: {Phone}", jobId, MaskPhone(e164));
                    return new SuccessResult("OTP gönderildi.");
                }

                var description = root.TryGetProperty("description", out var d) ? d.GetString() : "Bilinmeyen hata";
                _logger.LogError("[NetGSM] OTP gönderilemedi. Kod: {Code} | Açıklama: {Desc} | Numara: {Phone}", code, description, MaskPhone(e164));
                // Aktif kod varsa (NetGSM duplicate rejection) kullanıcıya anlamlı mesaj göster
                var userMessage = code == "20" || (description != null && description.Contains("active", StringComparison.OrdinalIgnoreCase))
                    ? $"Bu numaraya zaten kod gönderildi. Lütfen {OTP_VALIDITY_SECONDS} saniye bekleyip tekrar deneyin."
                    : "SMS gönderilemedi. Lütfen tekrar deneyin.";
                return new ErrorResult(userMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[NetGSM] HTTP isteği başarısız. Numara: {Phone}", MaskPhone(e164));
                return new ErrorResult("SMS servisi şu anda kullanılamıyor.");
            }
        }

        [ExceptionHandlingAspect(customErrorMessage: "Doğrulama başarısız. Lütfen tekrar deneyin.")]
        public async Task<IResult> CheckAsync(string e164, string code)
        {
            var cacheKey = CACHE_PREFIX + e164;

            if (!_cache.TryGetValue(cacheKey, out string? storedCode) || string.IsNullOrEmpty(storedCode))
                return new ErrorResult("Doğrulama kodunun süresi dolmuş. Lütfen yeni kod isteyin.");

            var attemptsKey = ATTEMPTS_PREFIX + e164;
            var attempts = _cache.TryGetValue(attemptsKey, out int a) ? a : 0;

            if (attempts >= MAX_OTP_ATTEMPTS)
            {
                _cache.Remove(cacheKey);
                _cache.Remove(attemptsKey);
                return new ErrorResult("Çok fazla hatalı deneme yapıldı. Lütfen yeni kod isteyin.");
            }

            if (!string.Equals(storedCode, code?.Trim(), StringComparison.Ordinal))
            {
                attempts++;
                _cache.Set(attemptsKey, attempts, TimeSpan.FromSeconds(OTP_ATTEMPTS_TTL_SECONDS));
                int remaining = MAX_OTP_ATTEMPTS - attempts;
                if (remaining <= 0)
                {
                    _cache.Remove(cacheKey);
                    _cache.Remove(attemptsKey);
                    return new ErrorResult("Çok fazla hatalı deneme yapıldı. Lütfen yeni kod isteyin.");
                }
                return new ErrorResult($"Geçersiz doğrulama kodu. {remaining} deneme hakkınız kaldı.");
            }

            _cache.Remove(cacheKey);
            _cache.Remove(attemptsKey);
            _logger.LogInformation("[NetGSM] OTP doğrulandı. Numara: {Phone}", MaskPhone(e164));
            return await Task.FromResult(new SuccessResult("Doğrulandı."));
        }

        /// <summary>
        /// E.164 formatını (+905XXXXXXXXX) Net GSM'nin beklediği formata (5XXXXXXXXX) çevirir.
        /// </summary>
        private static string ToNetGsmPhone(string e164)
        {
            // +905XXXXXXXXX → 5XXXXXXXXX
            if (e164.StartsWith("+90"))
                return e164[3..];
            // 905XXXXXXXXX → 5XXXXXXXXX
            if (e164.StartsWith("90") && e164.Length == 12)
                return e164[2..];
            // Başında + varsa sadece kaldır
            return e164.TrimStart('+');
        }

        private static string GenerateOtpCode()
        {
            return Random.Shared.Next(100_000, 1_000_000).ToString();
        }

        private static string MaskPhone(string e164)
        {
            if (e164.Length < 6) return "***";
            return e164[..4] + "***" + e164[^2..];
        }
    }
}
