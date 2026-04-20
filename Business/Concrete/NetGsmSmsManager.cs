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
    /// OTP süre/limitleri: NetGsm:Otp* appsettings anahtarları (varsayılanlar ctor içinde).
    /// </summary>
    public class NetGsmSmsManager : ISmsVerifyService
    {
        private const string OTP_ENDPOINT = "https://api.netgsm.com.tr/sms/rest/v2/otp";
        private const string CACHE_PREFIX = "otp_";
        private const string ATTEMPTS_PREFIX = "otp_attempts_";
        private const string RESEND_COOLDOWN_PREFIX = "otp_resend_cd_";
        private const string DEV_OTP_CODE = "123456";
        private const string HOURLY_PREFIX = "otp_hourly_";

        private readonly int _otpValiditySeconds;
        private readonly int _otpResendCooldownSeconds;
        private readonly int _maxHourlyOtp;
        private readonly int _maxOtpAttempts;
        private readonly int _otpAttemptsTtlSeconds;

        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _cache;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<NetGsmSmsManager> _logger;
        private readonly bool _enabled;
        private readonly HashSet<string> _testPhoneNumbers;

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
            var testNumbers = configuration.GetSection("NetGsm:TestPhoneNumbers").Get<string[]>() ?? Array.Empty<string>();
            _testPhoneNumbers = new HashSet<string>(testNumbers, StringComparer.OrdinalIgnoreCase);

            _otpValiditySeconds = configuration.GetValue("NetGsm:OtpValiditySeconds", 60);
            // 0 = iki gönderim arasında bekleme yok (sadece saatlik / NetGSM limitleri geçerli)
            _otpResendCooldownSeconds = configuration.GetValue("NetGsm:OtpResendCooldownSeconds", 0);
            _maxHourlyOtp = configuration.GetValue("NetGsm:OtpMaxHourlyPerPhone", 15);
            _maxOtpAttempts = configuration.GetValue("NetGsm:OtpMaxVerifyAttempts", 5);
            _otpAttemptsTtlSeconds = configuration.GetValue("NetGsm:OtpAttemptsWindowSeconds", 600);

            if (_otpValiditySeconds < 30) _otpValiditySeconds = 30;
            if (_otpResendCooldownSeconds < 0) _otpResendCooldownSeconds = 0;
            if (_maxHourlyOtp < 1) _maxHourlyOtp = 10;
            if (_maxOtpAttempts < 1) _maxOtpAttempts = 5;
            if (_otpAttemptsTtlSeconds < 60) _otpAttemptsTtlSeconds = 600;
        }

        [ExceptionHandlingAspect(customErrorMessage: "OTP gönderilemedi. Lütfen daha sonra tekrar deneyin.")]
        public async Task<IResult> SendAsync(string e164, string? language = null)
        {
            var cacheKey = CACHE_PREFIX + e164;
            var otpCode = GenerateOtpCode();

            var nowUtc = DateTime.UtcNow;
            var hourKey = HOURLY_PREFIX + e164 + "_" + nowUtc.ToString("yyyyMMddHH");
            var hourlySent = _cache.TryGetValue(hourKey, out int hCount) ? hCount : 0;
            if (hourlySent >= _maxHourlyOtp)
            {
                var nextHour = new DateTime(nowUtc.Year, nowUtc.Month, nowUtc.Day, nowUtc.Hour, 0, 0, DateTimeKind.Utc).AddHours(1);
                var waitMinutes = (int)Math.Ceiling((nextHour - nowUtc).TotalMinutes);
                return new ErrorResult($"Bu saat diliminde maksimum {_maxHourlyOtp} kod hakkınızı kullandınız. Yeni saat başında ({waitMinutes} dakika sonra) tekrar deneyebilirsiniz.");
            }

            var cooldownKey = RESEND_COOLDOWN_PREFIX + e164;
            if (_otpResendCooldownSeconds > 0 &&
                _cache.TryGetValue(cooldownKey, out DateTime lastSendUtc))
            {
                var wait = _otpResendCooldownSeconds - (int)(DateTime.UtcNow - lastSendUtc).TotalSeconds;
                if (wait > 0)
                {
                    return new ErrorResult(
                        $"Çok sık kod istediniz. Lütfen {wait} saniye sonra tekrar deneyin.");
                }
            }

            _cache.Remove(cacheKey);
            _cache.Remove(ATTEMPTS_PREFIX + e164);

            if (!_enabled || _testPhoneNumbers.Contains(e164))
            {
                _cache.Set(cacheKey, DEV_OTP_CODE, TimeSpan.FromSeconds(_otpValiditySeconds));
                if (_otpResendCooldownSeconds > 0)
                    _cache.Set(cooldownKey, DateTime.UtcNow, TimeSpan.FromSeconds(_otpResendCooldownSeconds * 2));
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

            var netGsmPhone = ToNetGsmPhone(e164);

            var smsBody = OtpSmsTemplate.BuildMessage(language, otpCode, _otpValiditySeconds);
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
                    _cache.Set(cacheKey, otpCode, TimeSpan.FromSeconds(_otpValiditySeconds));
                    if (_otpResendCooldownSeconds > 0)
                        _cache.Set(cooldownKey, DateTime.UtcNow, TimeSpan.FromSeconds(_otpResendCooldownSeconds * 2));
                    var nextHourBoundary = new DateTime(nowUtc.Year, nowUtc.Month, nowUtc.Day, nowUtc.Hour, 0, 0, DateTimeKind.Utc).AddHours(1);
                    _cache.Set(hourKey, hourlySent + 1, nextHourBoundary);
                    _logger.LogInformation("[NetGSM] OTP gönderildi. JobId: {JobId} | Numara: {Phone}", jobId, MaskPhone(e164));
                    return new SuccessResult("OTP gönderildi.");
                }

                var description = root.TryGetProperty("description", out var d) ? d.GetString() : "Bilinmeyen hata";
                _logger.LogError("[NetGSM] OTP gönderilemedi. Kod: {Code} | Açıklama: {Desc} | Numara: {Phone}", code, description, MaskPhone(e164));
                var userMessage = code == "20" || (description != null && description.Contains("active", StringComparison.OrdinalIgnoreCase))
                    ? $"Bu numaraya zaten kod gönderildi. Lütfen {_otpValiditySeconds} saniye bekleyip tekrar deneyin."
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

            if (attempts >= _maxOtpAttempts)
            {
                _cache.Remove(cacheKey);
                _cache.Remove(attemptsKey);
                return new ErrorResult("Çok fazla hatalı deneme yapıldı. Lütfen yeni kod isteyin.");
            }

            if (!string.Equals(storedCode, code?.Trim(), StringComparison.Ordinal))
            {
                attempts++;
                _cache.Set(attemptsKey, attempts, TimeSpan.FromSeconds(_otpAttemptsTtlSeconds));
                int remaining = _maxOtpAttempts - attempts;
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

        private static string ToNetGsmPhone(string e164)
        {
            if (e164.StartsWith("+90"))
                return e164[3..];
            if (e164.StartsWith("90") && e164.Length == 12)
                return e164[2..];
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
