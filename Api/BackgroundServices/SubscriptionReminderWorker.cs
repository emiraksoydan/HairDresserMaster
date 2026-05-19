using Business.Abstract;
using Business.Resources;
using Core.Utilities.Helpers;
using DataAccess.Concrete;
using Entities.Concrete.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Api.BackgroundServices
{
    /// <summary>
    /// Reader pattern (RP4): Abonelik bitiş hatırlatma worker'ı.
    ///
    /// Görevleri:
    /// - 7 gün kala → SubscriptionExpiringSoon push notification
    /// - 1 gün kala → SubscriptionExpiringTomorrow push notification
    /// - Bittiğinde → SubscriptionExpired push notification
    ///
    /// Kurallar:
    /// - Sadece `Subscription:GateEnabled = true` ise çalışır. False ise sleep (gate kapalıysa
    ///   kimse abonelik beklemiyor → boş bildirim göndermeyiz).
    /// - Sadece FreeBarber ve BarberStore kullanıcıları için (Customer'lar abone olmaz).
    /// - Sadece Türkiye saati 09:00–22:00 arası gönder (gece bildirim göndermeyiz).
    /// - Idempotent: Notifications tablosunda aynı türde son 23 saat içinde kayıt varsa atlar.
    /// - 30 dakikada bir cycle çalışır → bir günde 48 cycle, ama her kayıt için günde maksimum 1 push.
    /// </summary>
    public class SubscriptionReminderWorker(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<SubscriptionReminderWorker> logger
    ) : BackgroundService
    {
        private static readonly TimeSpan CycleInterval = TimeSpan.FromMinutes(30);
        private const int QuietHoursStart = 9;  // TR saati — bu saatten önce gönderme
        private const int QuietHoursEnd = 22;   // TR saati — bu saatten sonra gönderme

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // İlk başlangıçta küçük bir gecikme — uygulama tam ayağa kalksın
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var gateEnabled = configuration.GetValue<bool>("Subscription:GateEnabled", false);
                    if (!gateEnabled)
                    {
                        // Gate kapalı → kimse abone olmak zorunda değil, hatırlatma anlamsız.
                        logger.LogDebug("SubscriptionReminderWorker: Gate disabled, skipping cycle.");
                    }
                    else
                    {
                        var nowTr = TimeZoneHelper.ToTurkeyTime(DateTime.UtcNow);
                        if (nowTr.Hour < QuietHoursStart || nowTr.Hour >= QuietHoursEnd)
                        {
                            logger.LogDebug("SubscriptionReminderWorker: Quiet hours ({Hour}:00 TR), skipping cycle.", nowTr.Hour);
                        }
                        else
                        {
                            await ProcessCycleAsync(stoppingToken);
                        }
                    }

                    await Task.Delay(CycleInterval, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "SubscriptionReminderWorker cycle failed. Retrying in 60 seconds.");
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }
                }
            }
        }

        private async Task ProcessCycleAsync(CancellationToken token)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
            var notifySvc = scope.ServiceProvider.GetRequiredService<INotificationService>();

            var nowUtc = DateTime.UtcNow;
            // 23 saatlik idempotency penceresi — her cycle 30dk olduğundan günde 1 kez tetiklenir.
            var dedupeWindowStart = nowUtc.AddHours(-23);

            await ProcessReminderTierAsync(
                db, notifySvc,
                tierLabel: "7-day",
                type: NotificationType.SubscriptionExpiringSoon,
                minDaysLeft: 7, maxDaysLeft: 8,  // 7 < daysLeft <= 8 (yani tam 7 gün penceresi)
                dedupeWindowStart: dedupeWindowStart,
                title: Messages.SubscriptionPushTitle7DaysLeft,
                bodyTemplate: Messages.SubscriptionPushBody7DaysLeft,
                token: token);

            await ProcessReminderTierAsync(
                db, notifySvc,
                tierLabel: "1-day",
                type: NotificationType.SubscriptionExpiringTomorrow,
                minDaysLeft: 1, maxDaysLeft: 2,  // 1 < daysLeft <= 2
                dedupeWindowStart: dedupeWindowStart,
                title: Messages.SubscriptionPushTitle1DayLeft,
                bodyTemplate: Messages.SubscriptionPushBody1DayLeft,
                token: token);

            await ProcessExpiredAsync(
                db, notifySvc,
                dedupeWindowStart: dedupeWindowStart,
                token: token);
        }

        private async Task ProcessReminderTierAsync(
            DatabaseContext db,
            INotificationService notifySvc,
            string tierLabel,
            NotificationType type,
            int minDaysLeft,
            int maxDaysLeft,
            DateTime dedupeWindowStart,
            string title,
            string bodyTemplate,
            CancellationToken token)
        {
            var nowUtc = DateTime.UtcNow;
            var minEnd = nowUtc.AddDays(minDaysLeft);
            var maxEnd = nowUtc.AddDays(maxDaysLeft);

            // Aday kullanıcılar: subscription pencerede + iptal etmemiş + abonelik tipi
            var candidates = await db.Users
                .Where(u =>
                    !u.IsBanned &&
                    (u.UserType == UserType.FreeBarber || u.UserType == UserType.BarberStore) &&
                    u.SubscriptionEndDate != null &&
                    u.SubscriptionEndDate >= minEnd &&
                    u.SubscriptionEndDate < maxEnd)
                .Select(u => new { u.Id, u.SubscriptionEndDate, u.SubscriptionAutoRenew, u.SubscriptionCancelAtPeriodEnd })
                .ToListAsync(token);

            if (candidates.Count == 0)
            {
                logger.LogDebug("SubscriptionReminderWorker [{Tier}]: No candidates.", tierLabel);
                return;
            }

            // Idempotency: hangi kullanıcıya bu cycle'da zaten aynı türde push gönderildi?
            var candidateIds = candidates.Select(c => c.Id).ToList();
            var alreadySentIds = await db.Notifications
                .Where(n => candidateIds.Contains(n.UserId) &&
                            n.Type == type &&
                            n.CreatedAt >= dedupeWindowStart)
                .Select(n => n.UserId)
                .Distinct()
                .ToListAsync(token);
            var alreadySentSet = new HashSet<Guid>(alreadySentIds);

            int sentCount = 0;
            foreach (var c in candidates)
            {
                if (alreadySentSet.Contains(c.Id)) continue;
                try
                {
                    var daysLeft = Math.Max(0, (int)(c.SubscriptionEndDate!.Value - nowUtc).TotalDays + 1);
                    var payload = new
                    {
                        type = type.ToString(),
                        subscriptionEndDate = c.SubscriptionEndDate,
                        daysLeft,
                        autoRenew = c.SubscriptionAutoRenew,
                        cancelAtPeriodEnd = c.SubscriptionCancelAtPeriodEnd
                    };
                    await notifySvc.CreateAndPushAsync(
                        userId: c.Id,
                        type: type,
                        appointmentId: null,
                        title: title,
                        payload: payload,
                        body: bodyTemplate);
                    sentCount++;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex,
                        "SubscriptionReminderWorker [{Tier}]: Failed to send to userId={UserId}",
                        tierLabel, c.Id);
                }
            }

            if (sentCount > 0)
            {
                logger.LogInformation(
                    "SubscriptionReminderWorker [{Tier}]: Sent {Count}/{Total} reminders.",
                    tierLabel, sentCount, candidates.Count);
            }
        }

        private async Task ProcessExpiredAsync(
            DatabaseContext db,
            INotificationService notifySvc,
            DateTime dedupeWindowStart,
            CancellationToken token)
        {
            var nowUtc = DateTime.UtcNow;
            // Bugün biten ya da yakın geçmişte biten (max 24 saat içinde) abonelikler
            var expiredCutoffStart = nowUtc.AddDays(-1);
            var candidates = await db.Users
                .Where(u =>
                    !u.IsBanned &&
                    (u.UserType == UserType.FreeBarber || u.UserType == UserType.BarberStore) &&
                    u.SubscriptionEndDate != null &&
                    u.SubscriptionEndDate <= nowUtc &&
                    u.SubscriptionEndDate >= expiredCutoffStart)
                .Select(u => new { u.Id, u.SubscriptionEndDate })
                .ToListAsync(token);

            if (candidates.Count == 0)
            {
                logger.LogDebug("SubscriptionReminderWorker [expired]: No candidates.");
                return;
            }

            var candidateIds = candidates.Select(c => c.Id).ToList();
            var alreadySentIds = await db.Notifications
                .Where(n => candidateIds.Contains(n.UserId) &&
                            n.Type == NotificationType.SubscriptionExpired &&
                            n.CreatedAt >= dedupeWindowStart)
                .Select(n => n.UserId)
                .Distinct()
                .ToListAsync(token);
            var alreadySentSet = new HashSet<Guid>(alreadySentIds);

            int sentCount = 0;
            foreach (var c in candidates)
            {
                if (alreadySentSet.Contains(c.Id)) continue;
                try
                {
                    var payload = new
                    {
                        type = NotificationType.SubscriptionExpired.ToString(),
                        subscriptionEndDate = c.SubscriptionEndDate
                    };
                    await notifySvc.CreateAndPushAsync(
                        userId: c.Id,
                        type: NotificationType.SubscriptionExpired,
                        appointmentId: null,
                        title: Messages.SubscriptionPushTitleExpired,
                        payload: payload,
                        body: Messages.SubscriptionPushBodyExpired);
                    sentCount++;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex,
                        "SubscriptionReminderWorker [expired]: Failed to send to userId={UserId}", c.Id);
                }
            }

            if (sentCount > 0)
            {
                logger.LogInformation(
                    "SubscriptionReminderWorker [expired]: Sent {Count}/{Total} notifications.",
                    sentCount, candidates.Count);
            }
        }
    }
}
