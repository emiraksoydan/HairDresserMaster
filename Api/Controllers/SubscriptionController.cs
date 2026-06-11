using Api.Services;
using Business.Abstract;
using Business.Resources;
using DataAccess.Abstract;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Api.Controllers
{
    [Route("api/[controller]")]
    public class SubscriptionController(
        IUserDal userDal,
        DataAccess.Abstract.IBarberStoreDal barberStoreDal,
        IConfiguration configuration,
        IapMobileSubscriptionService iapMobile,
        ILogger<SubscriptionController> logger) : BaseApiController
    {
        /// <summary>
        /// Mevcut kullanıcının abonelik durumunu döner.
        /// Aboneliği bitmiş kullanıcılar da bu endpoint'e erişebilir (UserStatusFilter izin verir).
        /// Trial konsepti kullanıcı isteği üzerine kaldırıldı (Madde 8/Phase B);
        /// status yalnızca 'Active' / 'Expired' / 'Banned' döner.
        /// </summary>
        [HttpGet("status")]
        public async Task<IActionResult> GetStatus()
        {
            var user = await userDal.Get(u => u.Id == CurrentUserId);
            if (user == null) return NotFound();

            var now = DateTime.UtcNow;
            var subscriptionActive = user.SubscriptionEndDate.HasValue && user.SubscriptionEndDate.Value > now;

            // Gate kapalı (Subscription:GateEnabled = false) ise tüm kullanıcılara
            // 'Active' dön → frontend useSubscriptionGuard her aksiyonu açar.
            var gateEnabled = configuration.GetValue("Subscription:GateEnabled", false);

            // Abonelik sistemi yalnızca FreeBarber ve BarberStore hesaplarını etkiler.
            // Customer tipi kullanıcılar hiçbir zaman abonelik gerektirmez → her zaman Active döner.
            var isSubscriptionUser = user.UserType == Entities.Concrete.Enums.UserType.FreeBarber
                                  || user.UserType == Entities.Concrete.Enums.UserType.BarberStore;

            string status;
            if (user.IsBanned)
                status = "Banned";
            else if (!gateEnabled || subscriptionActive || !isSubscriptionUser)
                status = "Active";
            else
                status = "Expired";

            return Ok(new
            {
                success = true,
                data = new
                {
                    status,
                    gateEnabled,
                    // trialEndDate / trialDaysLeft alanları kullanıcı isteği üzerine kaldırıldı
                    // (Madde 8 / Phase B). Eski client'lar 'undefined' okuyup sorunsuz davranır;
                    // tip tarafında zaten optional.
                    subscriptionEndDate = user.SubscriptionEndDate,
                    isBanned = user.IsBanned,
                    banReason = user.BanReason,
                    autoRenew = user.SubscriptionAutoRenew,
                    cancelAtPeriodEnd = user.SubscriptionCancelAtPeriodEnd,
                    subscriptionDaysLeft = subscriptionActive ? (int)(user.SubscriptionEndDate!.Value - now).TotalDays : 0
                }
            });
        }

        /// <summary>
        /// Dinamik fiyatlandırma bilgisi (UI için).
        /// Frontend pricing sayfasında "X TL/ay" gibi gösterimleri otomatik üretir;
        /// appsettings'te fiyat değişirse client güncellemesi gerekmez.
        /// </summary>
        [HttpGet("pricing")]
        public async Task<IActionResult> GetPricing()
        {
            // FreeBarber: 200 TL/ay, BarberStore: 500 TL/ay (düz fiyat)
            // BarberStore'lara 1 dükkan ücretsiz; 2. ve sonrası için abonelik zorunlu.
            const int freeBarberMonthlyPrice = 200;
            const int barberStoreMonthlyPrice = 500;
            const string currency = "TL";

            int? currentStoreCount = null;
            try
            {
                var user = await userDal.Get(u => u.Id == CurrentUserId);
                if (user != null)
                {
                    var stores = await barberStoreDal.GetAll(s => s.BarberStoreOwnerId == user.Id);
                    currentStoreCount = stores?.Count ?? 0;
                }
            }
            catch
            {
            }

            return Ok(new
            {
                success = true,
                data = new
                {
                    currency,
                    freeBarber = new { monthlyPrice = freeBarberMonthlyPrice },
                    barberStore = new
                    {
                        monthlyPrice = barberStoreMonthlyPrice,
                        freeStoreCount = 1,
                        currentStoreCount
                    }
                }
            });
        }

        public sealed record AppleIapVerifyRequest(string TransactionId);

        public sealed record GoogleIapVerifyRequest(string ProductId, string PurchaseToken);

        /// <summary>
        /// iOS App Store — StoreKit satın alımından sonra işlem kimliği ile sunucu doğrulaması.
        /// </summary>
        [HttpPost("iap/apple")]
        public async Task<IActionResult> VerifyAppleIap([FromBody] AppleIapVerifyRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.TransactionId))
                return BadRequest(new { success = false, message = Messages.IapTransactionIdRequired });
            var outcome = await iapMobile.VerifyAppleAndApplyAsync(CurrentUserId, req.TransactionId.Trim());
            return StatusCode(outcome.HttpStatus, outcome.Body);
        }

        /// <summary>
        /// Google Play Billing — abonelik productId + purchaseToken ile doğrulama.
        /// </summary>
        [HttpPost("iap/google")]
        public async Task<IActionResult> VerifyGoogleIap([FromBody] GoogleIapVerifyRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.ProductId) || string.IsNullOrWhiteSpace(req.PurchaseToken))
                return BadRequest(new { success = false, message = Messages.IapProductIdAndPurchaseTokenRequired });
            var outcome = await iapMobile.VerifyGoogleAndApplyAsync(CurrentUserId, req.ProductId.Trim(), req.PurchaseToken.Trim());
            return StatusCode(outcome.HttpStatus, outcome.Body);
        }

        // ─── WEBHOOKS ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Apple App Store Server Notifications v2 webhook.
        /// App Store Connect → App Information → App Store Server Notifications URL'sine bu endpoint girilmeli.
        /// Apple, abonelik yenileme / iptal / bitiş olaylarını buraya JWS ile gönderir.
        /// </summary>
        [HttpPost("webhook/apple")]
        [AllowAnonymous]
        public async Task<IActionResult> AppleWebhook()
        {
            using var reader = new System.IO.StreamReader(Request.Body);
            var rawBody = await reader.ReadToEndAsync();

            if (string.IsNullOrWhiteSpace(rawBody))
                return Ok(); // Apple 200 bekler

            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(rawBody);
                if (!doc.RootElement.TryGetProperty("signedPayload", out var signedEl))
                    return Ok();

                var signedPayload = signedEl.GetString() ?? "";
                var outcome = await iapMobile.HandleAppleWebhookAsync(signedPayload);
                logger.LogInformation("Apple webhook işlendi: {Status}", outcome);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Apple webhook işlenirken hata oluştu");
            }

            // Apple her durumda 200 bekler; hata olsa bile 200 dön
            return Ok();
        }

        /// <summary>
        /// Google Play RTDN (Real-time Developer Notifications) webhook.
        /// Google Play Console → Monetization Setup → Real-time developer notifications'a bu endpoint girilmeli.
        /// Google, abonelik olaylarını Cloud Pub/Sub mesajı olarak buraya gönderir.
        /// </summary>
        [HttpPost("webhook/google")]
        [AllowAnonymous]
        public async Task<IActionResult> GoogleWebhook()
        {
            using var reader = new System.IO.StreamReader(Request.Body);
            var rawBody = await reader.ReadToEndAsync();

            if (string.IsNullOrWhiteSpace(rawBody))
                return Ok();

            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(rawBody);
                // Pub/Sub mesaj formatı: { "message": { "data": "<base64>", "messageId": "..." } }
                if (!doc.RootElement.TryGetProperty("message", out var messageEl)) return Ok();
                if (!messageEl.TryGetProperty("data", out var dataEl)) return Ok();

                var base64Data = dataEl.GetString() ?? "";
                var jsonBytes = Convert.FromBase64String(base64Data);
                var json = System.Text.Encoding.UTF8.GetString(jsonBytes);

                var outcome = await iapMobile.HandleGoogleWebhookAsync(json);
                logger.LogInformation("Google webhook işlendi: {Status}", outcome);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Google webhook işlenirken hata oluştu");
            }

            // Google Pub/Sub 200 bekler
            return Ok();
        }

    }
}
