using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Business.Resources;
using DataAccess.Abstract;
using Entities.Concrete.Enums;
using EntityUser = Entities.Concrete.Entities.User;
using Google.Apis.AndroidPublisher.v3;
using Google.Apis.AndroidPublisher.v3.Data;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Microsoft.IdentityModel.Tokens;

namespace Api.Services
{
    /// <summary>
    /// Mobil IAP (App Store + Google Play) doğrulama ve kullanıcı abonelik güncelleme.
    /// </summary>
    public class IapMobileSubscriptionService(
        IConfiguration configuration,
        IUserDal userDal,
        IHttpClientFactory httpClientFactory,
        ILogger<IapMobileSubscriptionService> logger)
    {
        private readonly IConfiguration _configuration = configuration;
        private readonly IUserDal _userDal = userDal;
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
        private readonly ILogger<IapMobileSubscriptionService> _logger = logger;

        /// <summary>Apple App Store Server API ile işlem bilgisini çeker; signedTransactionInfo JWS payload'unu parse eder.</summary>
        public async Task<IapVerifyOutcome> VerifyAppleAndApplyAsync(Guid userId, string transactionId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(transactionId))
                return IapVerifyOutcome.BadRequest(Messages.IapTransactionIdRequired);

            var bundleExpected = _configuration["Iap:Apple:BundleId"];
            if (string.IsNullOrWhiteSpace(bundleExpected))
            {
                _logger.LogError("IAP Apple: BundleId yapılandırılmamış");
                return IapVerifyOutcome.ServerError();
            }

            var jwt = CreateAppleApiJwt();
            if (jwt == null)
                return IapVerifyOutcome.ServerError();

            var urls = new[]
            {
                $"https://api.storekit.itunes.apple.com/inApps/v1/transactions/{Uri.EscapeDataString(transactionId)}",
                $"https://api.storekit-sandbox.itunes.apple.com/inApps/v1/transactions/{Uri.EscapeDataString(transactionId)}",
            };

            string? signedInfo = null;
            foreach (var url in urls)
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(25);
                using var resp = await client.SendAsync(req, ct);
                if (!resp.IsSuccessStatusCode) continue;
                await using var stream = await resp.Content.ReadAsStreamAsync(ct);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
                if (doc.RootElement.TryGetProperty("signedTransactionInfo", out var st))
                {
                    signedInfo = st.GetString();
                    break;
                }
            }

            if (string.IsNullOrEmpty(signedInfo))
            {
                _logger.LogWarning("IAP Apple: transaction bulunamadı veya yanıt beklenen formatta değil: {Tid}", transactionId);
                return IapVerifyOutcome.BadRequest(Messages.IapInvalidOrUnknownTransaction);
            }

            JsonDocument payloadDoc;
            try
            {
                payloadDoc = DecodeJwtPayloadDocument(signedInfo);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "IAP Apple: JWS parse hatası");
                return IapVerifyOutcome.BadRequest(Messages.IapTransactionPayloadUnreadable);
            }

            using (payloadDoc)
            {
                var payload = payloadDoc.RootElement;

            if (!payload.TryGetProperty("bundleId", out var bidEl))
                return IapVerifyOutcome.BadRequest(Messages.IapBundleIdMissing);
            var bid = bidEl.GetString();
            if (!string.Equals(bid, bundleExpected, StringComparison.Ordinal))
            {
                _logger.LogWarning("IAP Apple: bundle uyuşmuyor expected={Expected} got={Got}", bundleExpected, bid);
                return IapVerifyOutcome.BadRequest(Messages.IapPackageIdMismatch);
            }

            if (!payload.TryGetProperty("productId", out var pidEl))
                return IapVerifyOutcome.BadRequest(Messages.IapProductIdMissing);
            var productId = pidEl.GetString() ?? "";

            if (!TryMapAppleProductToPlan(productId, out var plan))
                return IapVerifyOutcome.BadRequest(Messages.IapUnknownProductPrefix + productId);

            var user = await _userDal.Get(u => u.Id == userId);
            if (user == null)
                return IapVerifyOutcome.NotFound(Messages.UserNotFoundNoPeriod);

            if (!ValidateUserTypeForPlan(user, plan))
                return IapVerifyOutcome.BadRequest(Messages.IapPlanNotCompatibleWithAccount);

            DateTime endUtc;
            if (payload.TryGetProperty("expiresDate", out var expEl) && expEl.ValueKind == JsonValueKind.Number)
            {
                var ms = expEl.GetInt64();
                endUtc = DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime;
            }
            else
            {
                endUtc = DateTime.UtcNow.AddDays(30);
            }

            ApplySubscriptionEnd(user, endUtc);
            await _userDal.Update(user);

            _logger.LogInformation("IAP Apple uygulandı: userId={UserId} plan={Plan} end={End}", userId, plan, user.SubscriptionEndDate);
            return IapVerifyOutcome.Ok(new { subscriptionEndDate = user.SubscriptionEndDate });
            }
        }

        public async Task<IapVerifyOutcome> VerifyGoogleAndApplyAsync(Guid userId, string productId, string purchaseToken, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(productId) || string.IsNullOrWhiteSpace(purchaseToken))
                return IapVerifyOutcome.BadRequest(Messages.IapProductIdAndPurchaseTokenRequired);

            var packageName = _configuration["Iap:Google:PackageName"];
            var jsonPath = _configuration["Iap:Google:ServiceAccountJsonPath"];
            if (string.IsNullOrWhiteSpace(packageName) || string.IsNullOrWhiteSpace(jsonPath) || !File.Exists(jsonPath))
            {
                _logger.LogError("IAP Google: PackageName veya ServiceAccountJsonPath eksik/geçersiz");
                return IapVerifyOutcome.ServerError();
            }

            if (!TryMapGoogleProductToPlan(productId, out var plan))
                return IapVerifyOutcome.BadRequest(Messages.IapUnknownProductPrefix + productId);

            GoogleCredential credential;
            try
            {
                await using var stream = File.OpenRead(jsonPath);
                credential = GoogleCredential.FromStream(stream)
                    .CreateScoped(AndroidPublisherService.Scope.Androidpublisher);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "IAP Google: credential okunamadı");
                return IapVerifyOutcome.ServerError();
            }

            var service = new AndroidPublisherService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "HairDresser"
            });

            SubscriptionPurchase sub;
            try
            {
                sub = await service.Purchases.Subscriptions.Get(packageName, productId, purchaseToken).ExecuteAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "IAP Google: Play API hatası");
                return IapVerifyOutcome.BadRequest(Messages.IapGooglePlayVerificationFailed);
            }

            if (sub.PaymentState is not (1 or 2))
            {
                _logger.LogWarning("IAP Google: ödeme durumu uygun değil state={State}", sub.PaymentState);
                return IapVerifyOutcome.BadRequest(Messages.IapSubscriptionPaymentIncomplete);
            }

            var user = await _userDal.Get(u => u.Id == userId);
            if (user == null)
                return IapVerifyOutcome.NotFound(Messages.UserNotFoundNoPeriod);

            if (!ValidateUserTypeForPlan(user, plan))
                return IapVerifyOutcome.BadRequest(Messages.IapPlanNotCompatibleWithAccount);

            DateTime endUtc;
            if (sub.ExpiryTimeMillis.HasValue)
                endUtc = DateTimeOffset.FromUnixTimeMilliseconds(sub.ExpiryTimeMillis.Value).UtcDateTime;
            else
                endUtc = DateTime.UtcNow.AddDays(30);

            ApplySubscriptionEnd(user, endUtc);
            await _userDal.Update(user);

            // Google zorunlu acknowledge (ilk satın alma)
            if (sub.AcknowledgementState == 0)
            {
                try
                {
                    await service.Purchases.Subscriptions.Acknowledge(
                        new SubscriptionPurchasesAcknowledgeRequest(),
                        packageName,
                        productId,
                        purchaseToken).ExecuteAsync(ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "IAP Google: acknowledge başarısız (abonelik yine de kaydedildi)");
                }
            }

            _logger.LogInformation("IAP Google uygulandı: userId={UserId} plan={Plan} end={End}", userId, plan, user.SubscriptionEndDate);
            return IapVerifyOutcome.Ok(new { subscriptionEndDate = user.SubscriptionEndDate });
        }

        private static void ApplySubscriptionEnd(EntityUser user, DateTime endUtc)
        {
            var now = DateTime.UtcNow;
            if (user.SubscriptionEndDate.HasValue && user.SubscriptionEndDate.Value > now)
            {
                if (endUtc > user.SubscriptionEndDate.Value)
                    user.SubscriptionEndDate = endUtc;
            }
            else
            {
                user.SubscriptionEndDate = endUtc > now ? endUtc : now.AddDays(30);
            }

            user.SubscriptionAutoRenew = true;
            user.SubscriptionCancelAtPeriodEnd = false;
        }

        private static bool ValidateUserTypeForPlan(EntityUser user, string plan) =>
            plan == "FreeBarber" && user.UserType == UserType.FreeBarber
            || plan == "BarberStore" && user.UserType == UserType.BarberStore;

        private bool TryMapAppleProductToPlan(string productId, out string plan)
        {
            plan = "";
            var section = _configuration.GetSection("Iap:Products:Apple");
            foreach (var c in section.GetChildren())
            {
                if (string.Equals(c.Key, productId, StringComparison.Ordinal))
                {
                    plan = c.Value ?? "";
                    return plan is "FreeBarber" or "BarberStore";
                }
            }

            return false;
        }

        private bool TryMapGoogleProductToPlan(string productId, out string plan)
        {
            plan = "";
            var section = _configuration.GetSection("Iap:Products:Google");
            foreach (var c in section.GetChildren())
            {
                if (string.Equals(c.Key, productId, StringComparison.Ordinal))
                {
                    plan = c.Value ?? "";
                    return plan is "FreeBarber" or "BarberStore";
                }
            }

            return false;
        }

        private string? CreateAppleApiJwt()
        {
            var issuerId = _configuration["Iap:Apple:IssuerId"];
            var keyId = _configuration["Iap:Apple:KeyId"];
            var bundleId = _configuration["Iap:Apple:BundleId"];
            var pem = _configuration["Iap:Apple:PrivateKeyPem"];
            if (string.IsNullOrWhiteSpace(pem))
            {
                var path = _configuration["Iap:Apple:PrivateKeyPath"];
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                    pem = File.ReadAllText(path);
            }

            if (string.IsNullOrWhiteSpace(issuerId) || string.IsNullOrWhiteSpace(keyId) ||
                string.IsNullOrWhiteSpace(bundleId) || string.IsNullOrWhiteSpace(pem))
                return null;

            try
            {
                using var ecdsa = ECDsa.Create();
                ecdsa.ImportFromPem(pem);

                var securityKey = new ECDsaSecurityKey(ecdsa) { KeyId = keyId };

                var handler = new JwtSecurityTokenHandler();
                var descriptor = new SecurityTokenDescriptor
                {
                    Issuer = issuerId,
                    Audience = "appstoreconnect-v1",
                    Claims = new Dictionary<string, object> { ["bid"] = bundleId },
                    Expires = DateTime.UtcNow.AddMinutes(19),
                    NotBefore = DateTime.UtcNow.AddMinutes(-1),
                    SigningCredentials = new SigningCredentials(securityKey, SecurityAlgorithms.EcdsaSha256),
                };
                var token = handler.CreateToken(descriptor);
                return handler.WriteToken(token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "IAP Apple: JWT üretilemedi");
                return null;
            }
        }

        // ─── WEBHOOK İŞLEME ─────────────────────────────────────────────────────────

        /// <summary>
        /// Apple App Store Server Notification v2 webhook'unu işler.
        /// signedPayload: Apple'dan gelen JWS outer token (notificationType + data içerir).
        ///
        /// Desteklenen olay tipleri:
        ///   DID_RENEW        → abonelik yenilendi, expiresDate uzatıldı
        ///   SUBSCRIBED       → yeni abonelik (ilk satın alım zaten /iap/apple ile kaydedildi; bu yedek)
        ///   DID_FAIL_TO_RENEW→ yenileme başarısız, abonelik yakında bitecek (state değiştirmiyoruz)
        ///   EXPIRED          → abonelik sona erdi (state zaten SubscriptionEndDate geçince Expired olur)
        ///   DID_CHANGE_RENEWAL_STATUS → kullanıcı iptal/yeniden etkinleştirme yaptı
        /// </summary>
        public async Task<string> HandleAppleWebhookAsync(string signedPayload)
        {
            // 1) Outer JWS payload'unu ayrıştır
            JsonDocument outerDoc;
            try { outerDoc = DecodeJwtPayloadDocument(signedPayload); }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Apple webhook: outer JWS parse hatası");
                return "parse-error";
            }

            using (outerDoc)
            {
                var root = outerDoc.RootElement;

                if (!root.TryGetProperty("notificationType", out var ntEl)) return "missing-notificationType";
                var notificationType = ntEl.GetString() ?? "";

                // data.signedTransactionInfo ayrıştır (en güncel işlem bilgisi burada)
                if (!root.TryGetProperty("data", out var dataEl)) return "missing-data";
                if (!dataEl.TryGetProperty("signedTransactionInfo", out var stiEl)) return "missing-signedTransactionInfo";
                var signedTx = stiEl.GetString() ?? "";

                JsonDocument txDoc;
                try { txDoc = DecodeJwtPayloadDocument(signedTx); }
                catch { return "tx-parse-error"; }

                using (txDoc)
                {
                    var tx = txDoc.RootElement;

                    // appAccountToken = requestSubscription sırasında userId olarak gömüldü
                    if (!tx.TryGetProperty("appAccountToken", out var tokenEl)) return "missing-appAccountToken";
                    var accountTokenStr = tokenEl.GetString() ?? "";
                    if (!Guid.TryParse(accountTokenStr, out var userId)) return "invalid-userId";

                    var user = await _userDal.Get(u => u.Id == userId);
                    if (user == null) return "user-not-found";

                    switch (notificationType)
                    {
                        case "DID_RENEW":
                        case "SUBSCRIBED":
                        {
                            // expiresDate (ms) varsa uygula
                            if (tx.TryGetProperty("expiresDate", out var expEl) && expEl.ValueKind == JsonValueKind.Number)
                            {
                                var endUtc = DateTimeOffset.FromUnixTimeMilliseconds(expEl.GetInt64()).UtcDateTime;
                                ApplySubscriptionEnd(user, endUtc);
                                await _userDal.Update(user);
                                _logger.LogInformation("Apple webhook {Type}: userId={Id} end={End}", notificationType, userId, user.SubscriptionEndDate);
                            }
                            return "ok-renewed";
                        }
                        case "DID_CHANGE_RENEWAL_STATUS":
                        {
                            // data.signedRenewalInfo içindeki autoRenewStatus'a bak
                            if (dataEl.TryGetProperty("signedRenewalInfo", out var sriEl))
                            {
                                try
                                {
                                    using var renewalDoc = DecodeJwtPayloadDocument(sriEl.GetString() ?? "");
                                    if (renewalDoc.RootElement.TryGetProperty("autoRenewStatus", out var arsEl))
                                    {
                                        var autoRenew = arsEl.GetInt32() == 1;
                                        user.SubscriptionAutoRenew = autoRenew;
                                        user.SubscriptionCancelAtPeriodEnd = !autoRenew;
                                        await _userDal.Update(user);
                                        _logger.LogInformation("Apple webhook RENEWAL_STATUS: userId={Id} autoRenew={AR}", userId, autoRenew);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex, "Apple webhook: signedRenewalInfo parse hatası");
                                }
                            }
                            return "ok-renewal-status";
                        }
                        default:
                            _logger.LogDebug("Apple webhook: işlenmedi tipi={Type}", notificationType);
                            return $"skipped-{notificationType}";
                    }
                }
            }
        }

        /// <summary>
        /// Google Play RTDN webhook'unu işler.
        /// json: Pub/Sub mesajının data alanının base64 decode edilmiş hali.
        ///
        /// Desteklenen bildirim tipleri:
        ///   1  = RECOVERED     → yeniden etkinleştirildi
        ///   2  = RENEWED       → yenilendi
        ///   4  = PURCHASED     → yeni satın alım (yedek)
        ///   7  = RESTARTED     → yeniden başlatıldı
        ///   3  = CANCELED      → iptal edildi
        ///   5  = ON_HOLD       → ödeme bekliyor
        ///   12 = REVOKED       → iade → iptal
        ///   13 = EXPIRED       → sona erdi
        /// </summary>
        public async Task<string> HandleGoogleWebhookAsync(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("subscriptionNotification", out var notifEl))
                return "not-subscription-notification";

            if (!notifEl.TryGetProperty("notificationType", out var ntEl)) return "missing-notificationType";
            var notificationType = ntEl.GetInt32();

            if (!notifEl.TryGetProperty("purchaseToken", out var ptEl)) return "missing-purchaseToken";
            var purchaseToken = ptEl.GetString() ?? "";

            if (!notifEl.TryGetProperty("subscriptionId", out var sidEl)) return "missing-subscriptionId";
            var subscriptionId = sidEl.GetString() ?? "";

            var packageName = _configuration["Iap:Google:PackageName"];
            var jsonPath = _configuration["Iap:Google:ServiceAccountJsonPath"];
            if (string.IsNullOrWhiteSpace(packageName) || string.IsNullOrWhiteSpace(jsonPath) || !File.Exists(jsonPath))
            {
                _logger.LogError("Google webhook: yapılandırma eksik");
                return "config-error";
            }

            GoogleCredential credential;
            try
            {
                await using var stream = File.OpenRead(jsonPath);
                credential = GoogleCredential.FromStream(stream)
                    .CreateScoped(AndroidPublisherService.Scope.Androidpublisher);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Google webhook: credential okunamadı");
                return "credential-error";
            }

            var service = new AndroidPublisherService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "HairDresser"
            });

            Google.Apis.AndroidPublisher.v3.Data.SubscriptionPurchase sub;
            try
            {
                sub = await service.Purchases.Subscriptions
                    .Get(packageName, subscriptionId, purchaseToken).ExecuteAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Google webhook: Play API hatası");
                return "play-api-error";
            }

            // obfuscatedExternalAccountId = requestSubscription sırasında userId olarak gömüldü
            var accountIdStr = sub.ObfuscatedExternalAccountId ?? sub.DeveloperPayload ?? "";
            if (!Guid.TryParse(accountIdStr, out var userId))
            {
                _logger.LogWarning("Google webhook: geçerli userId bulunamadı accountId={Id}", accountIdStr);
                return "invalid-userId";
            }

            var user = await _userDal.Get(u => u.Id == userId);
            if (user == null) return "user-not-found";

            switch (notificationType)
            {
                case 1: // RECOVERED
                case 2: // RENEWED
                case 4: // PURCHASED
                case 7: // RESTARTED
                {
                    if (sub.ExpiryTimeMillis.HasValue)
                    {
                        var endUtc = DateTimeOffset.FromUnixTimeMilliseconds(sub.ExpiryTimeMillis.Value).UtcDateTime;
                        ApplySubscriptionEnd(user, endUtc);
                        user.SubscriptionAutoRenew = true;
                        user.SubscriptionCancelAtPeriodEnd = false;
                        await _userDal.Update(user);
                        _logger.LogInformation("Google webhook type={Type}: userId={Id} end={End}", notificationType, userId, user.SubscriptionEndDate);
                    }
                    return $"ok-type{notificationType}";
                }
                case 3: // CANCELED
                case 5: // ON_HOLD
                case 12: // REVOKED
                {
                    user.SubscriptionAutoRenew = false;
                    user.SubscriptionCancelAtPeriodEnd = true;
                    await _userDal.Update(user);
                    _logger.LogInformation("Google webhook type={Type} (cancel): userId={Id}", notificationType, userId);
                    return $"ok-canceled-type{notificationType}";
                }
                case 13: // EXPIRED
                {
                    // SubscriptionEndDate zaten geçmişte → status otomatik Expired olur
                    _logger.LogInformation("Google webhook EXPIRED: userId={Id}", userId);
                    return "ok-expired";
                }
                default:
                    _logger.LogDebug("Google webhook: işlenmedi tipi={Type}", notificationType);
                    return $"skipped-type{notificationType}";
            }
        }

        private static JsonDocument DecodeJwtPayloadDocument(string jwt)
        {
            var parts = jwt.Split('.');
            if (parts.Length < 2)
                throw new InvalidOperationException("invalid jwt");

            var payload = parts[1];
            var padded = payload.Replace('-', '+').Replace('_', '/');
            switch (padded.Length % 4)
            {
                case 2: padded += "=="; break;
                case 3: padded += "="; break;
            }

            var json = Encoding.UTF8.GetString(Convert.FromBase64String(padded));
            return JsonDocument.Parse(json);
        }
    }

    /// <summary>MVC controller'ın StatusCode(body) ile döndürmesi için taşıyıcı.</summary>
    public sealed record IapVerifyOutcome(int HttpStatus, object Body)
    {
        public static IapVerifyOutcome Ok(object data) => new(200, new { success = true, data });

        public static IapVerifyOutcome BadRequest(string message) =>
            new(400, new { success = false, message });

        public static IapVerifyOutcome NotFound(string message) =>
            new(404, new { success = false, message });

        public static IapVerifyOutcome ServerError(string? message = null) =>
            new(500, new { success = false, message = message ?? Messages.IapServerConfigurationIncomplete });
    }
}
