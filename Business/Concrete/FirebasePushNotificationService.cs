using Business.Abstract;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;
using Google.Apis.Auth.OAuth2;
using System.IO;

namespace Business.Concrete
{
    /// <summary>
    /// Firebase Cloud Messaging (FCM) push notification service
    /// Handles background notifications when app is closed
    /// </summary>
    public class FirebasePushNotificationService : IPushNotificationService
    {
        private readonly IUserFcmTokenDal _fcmTokenDal;
        private readonly ISettingDal _settingDal;
        private readonly IConfiguration _configuration;
        private readonly ILogger<FirebasePushNotificationService> _logger;
        private readonly HttpClient _httpClient;
        private readonly bool _isFirebaseEnabled;
        private readonly byte[] _fcmEncKey;
        // OS launcher badge için toplam (notification + chat) hesaplamak üzere kullanılır.
        // Hata durumunda notification.BadgeCount fallback olarak kalır.
        private readonly BadgeService? _badgeService;

        // Static fields: Firebase credential is loaded once per process lifetime
        private static readonly object _firebaseLock = new();
        private static bool _firebaseInitialized;
        private static bool _isFirebaseEnabledStatic;
        private static GoogleCredential? _credentialStatic;
        private static string? _projectIdStatic;

        // Instance accessors map to static state
        private string? _projectId => _projectIdStatic;
        private GoogleCredential? _credential => _credentialStatic;

        /// <summary>
        /// FCM data payload'ını oluşturur. Frontend tüm değerleri buradan okur (manuel
        /// badge senkronizasyonu, navigation vb.). Badge değeri sadece BadgeCount set
        /// edildiyse eklenir — yoksa frontend mevcut rozeti değiştirmez.
        /// </summary>
        private static Dictionary<string, string> BuildDataDict(NotificationDto notification)
        {
            var data = new Dictionary<string, string>
            {
                { "notificationId", notification.Id.ToString() },
                { "type", ((int)notification.Type).ToString() },
                { "appointmentId", notification.AppointmentId?.ToString() ?? "" },
                { "payload", notification.PayloadJson ?? "{}" },
                { "click_action", "FLUTTER_NOTIFICATION_CLICK" }
            };
            if (notification.BadgeCount.HasValue)
            {
                data["badge"] = notification.BadgeCount.Value.ToString();
            }
            return data;
        }

        private static string MaskToken(string? token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return "***";
            if (token.Length <= 10)
                return $"{token[0]}***{token[^1]}";
            return $"{token[..6]}***{token[^4..]}";
        }

        private static string ComputeTokenHash(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return string.Empty;
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            return Convert.ToBase64String(bytes);
        }

        private string EncryptToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token) || _fcmEncKey.Length != 32)
                return token;

            using var aes = Aes.Create();
            aes.Key = _fcmEncKey;
            aes.GenerateIV();
            using var encryptor = aes.CreateEncryptor();
            var plain = Encoding.UTF8.GetBytes(token);
            var cipher = encryptor.TransformFinalBlock(plain, 0, plain.Length);
            var result = new byte[aes.IV.Length + cipher.Length];
            Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
            Buffer.BlockCopy(cipher, 0, result, aes.IV.Length, cipher.Length);
            return Convert.ToBase64String(result);
        }

        private string DecryptToken(string? encryptedOrPlain)
        {
            if (string.IsNullOrWhiteSpace(encryptedOrPlain) || _fcmEncKey.Length != 32)
                return encryptedOrPlain ?? string.Empty;

            try
            {
                var fullCipher = Convert.FromBase64String(encryptedOrPlain);
                if (fullCipher.Length < 17)
                    return encryptedOrPlain;
                using var aes = Aes.Create();
                aes.Key = _fcmEncKey;
                var iv = new byte[16];
                Buffer.BlockCopy(fullCipher, 0, iv, 0, 16);
                aes.IV = iv;
                var cipher = new byte[fullCipher.Length - 16];
                Buffer.BlockCopy(fullCipher, 16, cipher, 0, cipher.Length);
                using var decryptor = aes.CreateDecryptor();
                var plain = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
                return Encoding.UTF8.GetString(plain);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "FCM token decryption failed, using raw value");
                return encryptedOrPlain;
            }
        }

        public FirebasePushNotificationService(
            IUserFcmTokenDal fcmTokenDal,
            ISettingDal settingDal,
            IConfiguration configuration,
            ILogger<FirebasePushNotificationService> logger,
            IHttpClientFactory httpClientFactory,
            BadgeService? badgeService = null)
        {
            _fcmTokenDal = fcmTokenDal;
            _settingDal = settingDal;
            _configuration = configuration;
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient("FCM");
            _badgeService = badgeService;
            try
            {
                var keyBase64 = _configuration["Encryption:MessageKey"];
                _fcmEncKey = string.IsNullOrWhiteSpace(keyBase64) ? Array.Empty<byte>() : Convert.FromBase64String(keyBase64);
            }
            catch
            {
                _fcmEncKey = Array.Empty<byte>();
            }

            // Initialize Firebase Admin SDK once per process (static)
            lock (_firebaseLock)
            {
                if (!_firebaseInitialized)
                {
                    _firebaseInitialized = true;
                    try
                    {
                        var serviceAccountPath = _configuration["Firebase:ServiceAccountPath"];
                        var firebaseEnabled = _configuration.GetValue<bool>("Firebase:Enabled", true);

                        if (string.IsNullOrEmpty(serviceAccountPath) || !firebaseEnabled)
                        {
                            _isFirebaseEnabledStatic = false;
                            _logger.LogWarning("Firebase push notifications are disabled. Firebase:ServiceAccountPath is not configured or Firebase:Enabled is false.");
                        }
                        else
                        {
                            var basePath = AppContext.BaseDirectory;
                            var fullPath = Path.IsPathRooted(serviceAccountPath)
                                ? serviceAccountPath
                                : Path.Combine(basePath, serviceAccountPath);

                            if (!File.Exists(fullPath))
                            {
                                _isFirebaseEnabledStatic = false;
                                _logger.LogWarning($"Firebase service account file not found at: {fullPath}. Push notifications will be disabled.");
                            }
                            else
                            {
                                using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read);
                                _credentialStatic = GoogleCredential.FromStream(stream)
                                    .CreateScoped(new[] { "https://www.googleapis.com/auth/firebase.messaging" });

                                var json = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(fullPath));
                                _projectIdStatic = json.GetProperty("project_id").GetString()
                                    ?? throw new InvalidOperationException("project_id not found in service account JSON");

                                _isFirebaseEnabledStatic = true;
                                _logger.LogInformation($"Firebase Admin SDK initialized successfully for project: {_projectIdStatic}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _isFirebaseEnabledStatic = false;
                        _logger.LogWarning(ex, "Failed to initialize Firebase Admin SDK. Push notifications will be disabled.");
                    }
                }
            }
            _isFirebaseEnabled = _isFirebaseEnabledStatic;
        }

        public async Task<bool> SendPushNotificationAsync(Guid userId, NotificationDto notification)
        {
            // Firebase devre dışıysa sadece log yaz ve false döndür
            if (!_isFirebaseEnabled || _credential == null || string.IsNullOrEmpty(_projectId))
            {
                _logger.LogDebug($"Firebase push notifications are disabled. Skipping notification for user {userId}");
                return false;
            }

            try
            {
                var userSetting = await _settingDal.GetByUserIdAsync(userId);
                var soundEnabled = userSetting?.EnableNotificationSound ?? true;

                // OS launcher badge: notification + chat unread toplamı.
                // BadgeService varsa "tek hakikat"ten oku (BadgeController ile aynı kaynak),
                // hata durumunda çağıranın set ettiği notification.BadgeCount fallback kalır.
                if (_badgeService != null)
                {
                    try
                    {
                        var totals = await _badgeService.GetBadgeCountsAsync(userId);
                        notification.BadgeCount = totals.NotificationUnreadCount + totals.ChatUnreadCount;
                    }
                    catch (Exception badgeEx)
                    {
                        _logger.LogWarning(badgeEx, "BadgeService failed in push pipeline; falling back to per-message BadgeCount. UserId={UserId}", userId);
                    }
                }

                // Get all active FCM tokens for the user
                var tokens = await _fcmTokenDal.GetActiveTokensByUserIdAsync(userId);
                if (tokens == null || tokens.Count == 0)
                {
                    _logger.LogWarning("[FCM.Send] No active FCM tokens found. UserId={UserId}, Type={Type}, AppointmentId={AppointmentId}, Title={Title}",
                        userId, notification.Type, notification.AppointmentId, notification.Title);
                    return false;
                }

                _logger.LogInformation(
                    "[FCM.Send] Attempting push. UserId={UserId}, TokenCount={TokenCount}, Type={Type} ({TypeName}), AppointmentId={AppointmentId}, Title={Title}, SoundEnabled={SoundEnabled}",
                    userId,
                    tokens.Count,
                    (int)notification.Type,
                    notification.Type,
                    notification.AppointmentId,
                    notification.Title,
                    soundEnabled);

                var successCount = 0;
                var failedTokens = new List<string>();

                foreach (var token in tokens)
                {
                    try
                    {
                        // Get OAuth2 access token
                        if (_credential?.UnderlyingCredential is not ServiceAccountCredential serviceAccountCredential)
                        {
                            throw new InvalidOperationException("Invalid credential type");
                        }

                        var accessToken = await serviceAccountCredential.GetAccessTokenForRequestAsync();

                        // FCM v1 API format
                        var plainToken = DecryptToken(token.FcmTokenEncrypted);
                        if (string.IsNullOrWhiteSpace(plainToken))
                        {
                            _logger.LogWarning("FCM token decrypt returned empty for user {UserId}. Token row will be skipped.", userId);
                            continue;
                        }
                        // Cihaz ikonu rozeti: NotificationManager bu sayıyı set ediyor (okunmamış adedi).
                        // Set edilmediyse payload'a HİÇ yazılmaz — böylece (örn. chat push'u gelince)
                        // mevcut rozet sıfırlanmaz. iOS Apple kuralı: aps.badge alanı yoksa dokunulmaz,
                        // 0 ise siler. Aynı yaklaşımı Android notification_count için de uyguluyoruz.
                        var pushBody = notification.Body ?? notification.Title;

                        // APNs aps payload'ı:
                        //   - alert (title+body) → iOS bildirimi GÖSTERSİN diye zorunlu
                        //   - badge → uygulama ikonunda kırmızı sayı (sadece sayı belirtildiyse)
                        //   - sound → ses ayarı açıksa
                        //   - mutable-content → notification service extension için (opsiyonel)
                        // ÖNEMLİ: content-available KALDIRILDI. Alert + content-available aynı payload'da
                        // olunca iOS bildirimi silent push gibi yorumluyor ve görünmüyordu.
                        var apsDict = new Dictionary<string, object>
                        {
                            ["alert"] = new Dictionary<string, object>
                            {
                                ["title"] = notification.Title,
                                ["body"] = pushBody
                            },
                            ["mutable-content"] = 1
                        };
                        if (notification.BadgeCount.HasValue)
                        {
                            apsDict["badge"] = notification.BadgeCount.Value;
                        }
                        if (soundEnabled)
                        {
                            apsDict["sound"] = "default";
                        }

                        // Android notification bloğu — launcher badge'i için notification_count
                        // (Samsung One UI / MIUI / OEM launcher'lar bu değeri rozet olarak gösterir)
                        var androidNotification = new Dictionary<string, object?>
                        {
                            ["sound"] = soundEnabled ? "default" : null
                            // channelId belirtilmez → FCM otomatik olarak
                            // fcm_fallback_notification_channel kullanır
                            // (Firebase SDK tarafından her cihazda otomatik oluşturulur)
                        };
                        if (notification.BadgeCount.HasValue)
                        {
                            androidNotification["notification_count"] = notification.BadgeCount.Value;
                        }

                        var fcmMessage = new
                        {
                            message = new
                            {
                                token = plainToken,
                                notification = new
                                {
                                    title = notification.Title,
                                    body = pushBody,
                                },
                                data = BuildDataDict(notification),
                                android = new
                                {
                                    priority = "high",
                                    notification = androidNotification
                                },
                                apns = new
                                {
                                    headers = new Dictionary<string, string>
                                    {
                                        { "apns-priority", "10" },
                                        { "apns-push-type", "alert" }
                                    },
                                    payload = new Dictionary<string, object>
                                    {
                                        ["aps"] = apsDict
                                    }
                                }
                            }
                        };

                        // Send using FCM v1 REST API
                        var request = new HttpRequestMessage(HttpMethod.Post, $"https://fcm.googleapis.com/v1/projects/{_projectId}/messages:send");
                        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                        request.Content = JsonContent.Create(fcmMessage);

                        _logger.LogDebug(
                            "[FCM.Send] Dispatching to FCM. UserId={UserId}, Platform={Platform}, DeviceId={DeviceId}, Token={Token}",
                            userId, token.Platform, token.DeviceId, MaskToken(plainToken));

                        var response = await _httpClient.SendAsync(request);
                        var responseContent = await response.Content.ReadAsStringAsync();

                        if (response.IsSuccessStatusCode)
                        {
                            successCount++;
                            _logger.LogInformation(
                                "[FCM.Send] OK. UserId={UserId}, Token={Token}, Platform={Platform}, Status={Status}",
                                userId, MaskToken(plainToken), token.Platform, (int)response.StatusCode);
                            // Update token's last used timestamp
                            token.UpdatedAt = DateTime.UtcNow;
                            await _fcmTokenDal.Update(token);
                        }
                        else
                        {
                            _logger.LogWarning(
                                "[FCM.Send] FAILED. UserId={UserId}, Token={Token}, Platform={Platform}, Status={Status}, Response={Response}",
                                userId, MaskToken(plainToken), token.Platform, (int)response.StatusCode, responseContent);
                            
                            // If token is invalid, deactivate it
                            var shouldDeactivate = response.StatusCode == System.Net.HttpStatusCode.BadRequest ||
                                response.StatusCode == System.Net.HttpStatusCode.NotFound ||
                                responseContent.Contains("INVALID_ARGUMENT") ||
                                responseContent.Contains("NOT_FOUND") ||
                                responseContent.Contains("UNREGISTERED");
                            
                            if (shouldDeactivate)
                            {
                                failedTokens.Add(plainToken);
                                await _fcmTokenDal.DeactivateTokenAsync(plainToken);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error sending FCM notification to token {MaskToken(token.FcmToken)}");
                        var plainToken = DecryptToken(token.FcmTokenEncrypted);
                        failedTokens.Add(plainToken);
                    }
                }

                _logger.LogInformation(
                    "[FCM.Send] Summary. UserId={UserId}, Success={Success}/{Total}, DeactivatedTokens={Deactivated}, Type={Type}, AppointmentId={AppointmentId}",
                    userId, successCount, tokens.Count, failedTokens.Count, notification.Type, notification.AppointmentId);
                return successCount > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in SendPushNotificationAsync for user {userId}");
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<bool> SendBadgeOnlySyncAsync(Guid userId)
        {
            if (!_isFirebaseEnabled || _credential == null || string.IsNullOrEmpty(_projectId))
            {
                _logger.LogDebug("Firebase disabled; skipping SendBadgeOnlySyncAsync for user {UserId}", userId);
                return false;
            }

            int badgeTotal;
            try
            {
                if (_badgeService == null)
                {
                    _logger.LogDebug("BadgeService not available; skipping SendBadgeOnlySyncAsync for user {UserId}", userId);
                    return false;
                }

                var totals = await _badgeService.GetBadgeCountsAsync(userId);
                badgeTotal = Math.Max(0, totals.NotificationUnreadCount + totals.ChatUnreadCount);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SendBadgeOnlySyncAsync: badge totals failed for user {UserId}", userId);
                return false;
            }

            try
            {
                var tokens = await _fcmTokenDal.GetActiveTokensByUserIdAsync(userId);
                if (tokens == null || tokens.Count == 0)
                {
                    _logger.LogDebug("[FCM.BadgeSync] No active FCM tokens. UserId={UserId}", userId);
                    return false;
                }

                if (_credential?.UnderlyingCredential is not ServiceAccountCredential serviceAccountCredential)
                {
                    _logger.LogWarning("[FCM.BadgeSync] Invalid credential type for user {UserId}", userId);
                    return false;
                }

                var accessToken = await serviceAccountCredential.GetAccessTokenForRequestAsync();
                var dataDict = new Dictionary<string, string>
                {
                    ["badge"] = badgeTotal.ToString(),
                    ["silentBadgeSync"] = "1",
                    ["click_action"] = "FLUTTER_NOTIFICATION_CLICK"
                };

                var successCount = 0;
                foreach (var token in tokens)
                {
                    try
                    {
                        var plainToken = DecryptToken(token.FcmTokenEncrypted);
                        if (string.IsNullOrWhiteSpace(plainToken))
                            continue;

                        // Data-only: tray'de başlık yok. iOS rozeti aps.badge; Android data.badge + arka plan handler.
                        var fcmMessage = new
                        {
                            message = new
                            {
                                token = plainToken,
                                data = dataDict,
                                android = new { priority = "high" },
                                apns = new
                                {
                                    headers = new Dictionary<string, string>
                                    {
                                        { "apns-priority", "5" },
                                        { "apns-push-type", "background" }
                                    },
                                    payload = new Dictionary<string, object>
                                    {
                                        ["aps"] = new Dictionary<string, object>
                                        {
                                            ["content-available"] = 1,
                                            ["badge"] = badgeTotal
                                        }
                                    }
                                }
                            }
                        };

                        var request = new HttpRequestMessage(HttpMethod.Post, $"https://fcm.googleapis.com/v1/projects/{_projectId}/messages:send");
                        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                        request.Content = JsonContent.Create(fcmMessage);

                        var response = await _httpClient.SendAsync(request);
                        var responseContent = await response.Content.ReadAsStringAsync();

                        if (response.IsSuccessStatusCode)
                        {
                            successCount++;
                            token.UpdatedAt = DateTime.UtcNow;
                            await _fcmTokenDal.Update(token);
                        }
                        else
                        {
                            _logger.LogWarning(
                                "[FCM.BadgeSync] FAILED. UserId={UserId}, Token={Token}, Status={Status}, Response={Response}",
                                userId, MaskToken(plainToken), (int)response.StatusCode, responseContent);

                            var shouldDeactivate = response.StatusCode == System.Net.HttpStatusCode.BadRequest ||
                                response.StatusCode == System.Net.HttpStatusCode.NotFound ||
                                responseContent.Contains("INVALID_ARGUMENT", StringComparison.Ordinal) ||
                                responseContent.Contains("NOT_FOUND", StringComparison.Ordinal) ||
                                responseContent.Contains("UNREGISTERED", StringComparison.Ordinal);

                            if (shouldDeactivate)
                                await _fcmTokenDal.DeactivateTokenAsync(plainToken);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[FCM.BadgeSync] token send error for user {UserId}", userId);
                    }
                }

                _logger.LogInformation("[FCM.BadgeSync] UserId={UserId}, Success={Success}/{Total}, Badge={Badge}",
                    userId, successCount, tokens.Count, badgeTotal);
                return successCount > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SendBadgeOnlySyncAsync failed for user {UserId}", userId);
                return false;
            }
        }

        public async Task<bool> RegisterFcmTokenAsync(Guid userId, string fcmToken, string? deviceId = null, string? platform = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(fcmToken))
                {
                    _logger.LogWarning($"Empty FCM token provided for user {userId}");
                    return false;
                }

                // Check if token already exists
                var existing = await _fcmTokenDal.GetByTokenAsync(fcmToken);
                if (existing != null)
                {
                    // Update existing token
                    existing.UserId = userId;
                    existing.FcmToken = fcmToken;
                    existing.FcmTokenHash = ComputeTokenHash(fcmToken);
                    existing.FcmTokenEncrypted = EncryptToken(fcmToken);
                    existing.IsActive = true;
                    existing.UpdatedAt = DateTime.UtcNow;
                    if (!string.IsNullOrWhiteSpace(deviceId))
                        existing.DeviceId = deviceId;
                    if (!string.IsNullOrWhiteSpace(platform))
                        existing.Platform = platform;
                    await _fcmTokenDal.Update(existing);
                    _logger.LogInformation($"FCM token updated for user {userId} | Token: {MaskToken(fcmToken)}");
                    return true;
                }

                // Create new token
                var newToken = new UserFcmToken
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    FcmToken = fcmToken,
                    FcmTokenHash = ComputeTokenHash(fcmToken),
                    FcmTokenEncrypted = EncryptToken(fcmToken),
                    DeviceId = deviceId,
                    Platform = platform,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await _fcmTokenDal.Add(newToken);
                _logger.LogInformation($"FCM token registered for user {userId} | Token: {MaskToken(fcmToken)}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error registering FCM token for user {userId}");
                return false;
            }
        }

        public async Task<bool> UnregisterFcmTokenAsync(Guid userId, string fcmToken)
        {
            try
            {
                var token = await _fcmTokenDal.GetByTokenAsync(fcmToken);
                if (token != null && token.UserId == userId)
                {
                    await _fcmTokenDal.DeactivateTokenAsync(fcmToken);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error unregistering FCM token for user {userId}");
                return false;
            }
        }
    }
}

