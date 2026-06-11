using Business.Resources;
using DataAccess.Abstract;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;

namespace Api.Filters
{
    public class UserStatusFilter(IUserDal userDal, IMemoryCache cache, IConfiguration configuration) : IAsyncActionFilter
    {
        private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            // AllowAnonymous endpoint'leri atla
            var endpoint = context.HttpContext.GetEndpoint();
            if (endpoint?.Metadata?.GetMetadata<IAllowAnonymous>() != null)
            {
                await next();
                return;
            }

            var user = context.HttpContext.User;
            if (user.Identity?.IsAuthenticated != true)
            {
                await next();
                return;
            }

            var userIdStr = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdStr, out var userId))
            {
                await next();
                return;
            }

            // DB sorgusunu kısa süreli cache ile sarmalıyoruz (her istek DB'yi çarpmıyor)
            var cacheKey = $"user_status_{userId}";
            if (!cache.TryGetValue(cacheKey, out UserStatusCache? status))
            {
                var dbUser = await userDal.Get(u => u.Id == userId);
                status = dbUser == null ? null : new UserStatusCache
                {
                    IsBanned = dbUser.IsBanned,
                    BanReason = dbUser.BanReason,
                    UserType = dbUser.UserType.ToString(),
                    // Trial konsepti kaldırıldı (Madde 8/Phase B); TrialEndDate artık okunmuyor.
                    SubscriptionEndDate = dbUser.SubscriptionEndDate
                };
                cache.Set(cacheKey, status, CacheDuration);
            }

            if (status == null)
            {
                await next();
                return;
            }

            // Ban kontrolü
            if (status.IsBanned)
            {
                var message = string.IsNullOrWhiteSpace(status.BanReason)
                    ? Messages.UserBanned
                    : string.Format(Messages.UserBannedWithReason, status.BanReason);

                context.Result = new ObjectResult(new { success = false, message, banned = true }) { StatusCode = 403 };
                return;
            }

            // Subscription gate feature flag — `appsettings.json::Subscription:GateEnabled`.
            // false (default) → subscription kontrolü tamamen atlanır; tüm özellikler herkese açık.
            // true → sadece belirlenen özellikler (mesajlaşma / randevu / rating / AI) abonelik gerektirir.
            //
            // Ücretsiz kalan özellikler (abonelik olmadan erişilebilir):
            //   - 1 panel oluşturma/yönetimi (FreeBarber)
            //   - 1 dükkan oluşturma/yönetimi (BarberStore)
            //   - Profil görünürlüğü, müşteri keşfi
            //   - /api/subscription (kendi abonelik yönetimi)
            var gateEnabled = configuration.GetValue("Subscription:GateEnabled", false);
            if (gateEnabled && status.UserType is "FreeBarber" or "BarberStore")
            {
                var subscriptionActive = status.SubscriptionEndDate.HasValue && status.SubscriptionEndDate.Value > DateTime.UtcNow;

                if (!subscriptionActive)
                {
                    var path = context.HttpContext.Request.Path.Value?.ToLower() ?? "";
                    if (RequiresSubscription(path))
                    {
                        context.Result = new ObjectResult(new { success = false, message = Messages.SubscriptionExpired }) { StatusCode = 403 };
                        return;
                    }
                }
            }

            await next();
        }

        /// <summary>
        /// Abonelik gerektiren endpoint path'lerini kontrol eder.
        /// Mesajlaşma, randevu, rating oluşturma ve yapay zeka asistanı abonelik gerektirir.
        /// Panel / dükkan yönetimi, profil, keşif → abonelik GEREKTIRMEZ (ücretsiz özellikler).
        /// </summary>
        private static bool RequiresSubscription(string path)
        {
            // Mesajlaşma — tüm chat işlemleri
            if (path.StartsWith("/api/chat")) return true;

            // Randevu süreçleri — tüm randevu işlemleri (oluşturma, onaylama, iptal vb.)
            if (path.StartsWith("/api/appointment")) return true;

            // Rating oluşturma — sadece create; rating okuma herkese açık
            if (path == "/api/rating/create") return true;

            // Yapay Zeka Asistanı — tüm AI işlemleri
            if (path.StartsWith("/api/ai")) return true;

            return false;
        }

        private sealed class UserStatusCache
        {
            public bool IsBanned { get; init; }
            public string? BanReason { get; init; }
            public string UserType { get; init; } = string.Empty;
            public DateTime? SubscriptionEndDate { get; init; }
        }
    }
}
