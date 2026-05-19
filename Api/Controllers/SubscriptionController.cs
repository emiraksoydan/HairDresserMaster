using Api.Services;
using Business.Abstract;
using Business.Resources;
using DataAccess.Abstract;
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

            string status;
            if (user.IsBanned)
                status = "Banned";
            else if (!gateEnabled || subscriptionActive)
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
        /// Aktif aboneliği dönem sonunda sonlandıracak şekilde işaretler.
        /// </summary>
        [HttpPost("cancel")]
        public async Task<IActionResult> Cancel()
        {
            var user = await userDal.Get(u => u.Id == CurrentUserId);
            if (user == null) return NotFound(new { success = false, message = Messages.UserNotFoundNoPeriod });

            var now = DateTime.UtcNow;
            var subscriptionActive = user.SubscriptionEndDate.HasValue && user.SubscriptionEndDate.Value > now;
            if (!subscriptionActive)
            {
                return BadRequest(new { success = false, message = Messages.SubscriptionActiveNotFound });
            }

            user.SubscriptionAutoRenew = false;
            user.SubscriptionCancelAtPeriodEnd = true;
            await userDal.Update(user);

            return Ok(new { success = true, message = Messages.SubscriptionCancelAtPeriodEnd });
        }

        /// <summary>
        /// Dönem sonu iptal isteğini geri alır (auto-renew yeniden açılır).
        /// </summary>
        [HttpPost("reactivate")]
        public async Task<IActionResult> Reactivate()
        {
            var user = await userDal.Get(u => u.Id == CurrentUserId);
            if (user == null) return NotFound(new { success = false, message = Messages.UserNotFoundNoPeriod });

            var now = DateTime.UtcNow;
            var subscriptionActive = user.SubscriptionEndDate.HasValue && user.SubscriptionEndDate.Value > now;
            if (!subscriptionActive)
            {
                return BadRequest(new { success = false, message = Messages.SubscriptionNoActiveRenewRequired });
            }

            user.SubscriptionAutoRenew = true;
            user.SubscriptionCancelAtPeriodEnd = false;
            await userDal.Update(user);

            return Ok(new { success = true, message = Messages.SubscriptionReactivated });
        }

        /// <summary>
        /// Dinamik fiyatlandırma bilgisi (UI için).
        /// Frontend pricing sayfasında "X TL/ay" gibi gösterimleri otomatik üretir;
        /// appsettings'te fiyat değişirse client güncellemesi gerekmez.
        /// </summary>
        [HttpGet("pricing")]
        public async Task<IActionResult> GetPricing()
        {
            const int freeBarberPrice = 500;
            const int barberStoreBase = 1000;
            const int barberStoreBaseCount = 3;
            const int barberStoreExtra = 2000;
            const string currency = "TL";

            int? currentStoreCount = null;
            int? estimatedMonthlyPrice = null;
            try
            {
                var user = await userDal.Get(u => u.Id == CurrentUserId);
                if (user != null)
                {
                    var stores = await barberStoreDal.GetAll(s => s.BarberStoreOwnerId == user.Id);
                    currentStoreCount = stores?.Count ?? 0;
                    var extras = Math.Max(0, currentStoreCount.Value - barberStoreBaseCount);
                    estimatedMonthlyPrice = barberStoreBase + (barberStoreExtra * extras);
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
                    freeBarber = new { monthlyPrice = freeBarberPrice },
                    barberStore = new
                    {
                        baseMonthlyPrice = barberStoreBase,
                        baseStoreCount = barberStoreBaseCount,
                        extraStoreMonthlyPrice = barberStoreExtra,
                        currentStoreCount,
                        estimatedMonthlyPrice
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

    }
}
