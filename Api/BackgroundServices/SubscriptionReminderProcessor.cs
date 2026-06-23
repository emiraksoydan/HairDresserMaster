using Business.Abstract;
using Business.Resources;
using Core.Utilities.Helpers;
using DataAccess.Concrete;
using Entities.Concrete.Enums;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Api.BackgroundServices
{
    /// <summary>
    /// Abonelik bitiş hatırlatma döngüsü (Hangfire recurring job).
    /// </summary>
    public class SubscriptionReminderProcessor(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<SubscriptionReminderProcessor> logger
    )
    {
        private static readonly TimeSpan CycleInterval = TimeSpan.FromMinutes(30);
        private const int QuietHoursStart = 9;
        private const int QuietHoursEnd = 22;

        [DisableConcurrentExecution(timeoutInSeconds: 600)]
        [AutomaticRetry(Attempts = 2, DelaysInSeconds = new[] { 120, 300 })]
        public async Task RunCycleAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var gateEnabled = configuration.GetValue<bool>("Subscription:GateEnabled", false);
                if (!gateEnabled)
                {
                    logger.LogDebug("SubscriptionReminderProcessor: Gate disabled, skipping cycle.");
                    return;
                }

                var nowTr = TimeZoneHelper.ToTurkeyTime(DateTime.UtcNow);
                if (nowTr.Hour < QuietHoursStart || nowTr.Hour >= QuietHoursEnd)
                {
                    logger.LogDebug("SubscriptionReminderProcessor: Quiet hours ({Hour}:00 TR), skipping cycle.", nowTr.Hour);
                    return;
                }

                await ProcessCycleAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // shutdown
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "SubscriptionReminderProcessor cycle failed.");
                throw;
            }
        }

        private async Task ProcessCycleAsync(CancellationToken token)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
            var notifySvc = scope.ServiceProvider.GetRequiredService<INotificationService>();

            var nowUtc = DateTime.UtcNow;
            var dedupeWindowStart = nowUtc.AddHours(-23);

            await ProcessReminderTierAsync(
                db, notifySvc,
                tierLabel: "7-day",
                type: NotificationType.SubscriptionExpiringSoon,
                minDaysLeft: 7, maxDaysLeft: 8,
                dedupeWindowStart: dedupeWindowStart,
                title: Messages.SubscriptionPushTitle7DaysLeft,
                bodyTemplate: Messages.SubscriptionPushBody7DaysLeft,
                token: token);

            await ProcessReminderTierAsync(
                db, notifySvc,
                tierLabel: "1-day",
                type: NotificationType.SubscriptionExpiringTomorrow,
                minDaysLeft: 1, maxDaysLeft: 2,
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
                logger.LogDebug("SubscriptionReminderProcessor [{Tier}]: No candidates.", tierLabel);
                return;
            }

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
                        "SubscriptionReminderProcessor [{Tier}]: Failed to send to userId={UserId}",
                        tierLabel, c.Id);
                }
            }

            if (sentCount > 0)
            {
                logger.LogInformation(
                    "SubscriptionReminderProcessor [{Tier}]: Sent {Count}/{Total} reminders.",
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
                logger.LogDebug("SubscriptionReminderProcessor [expired]: No candidates.");
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
                        "SubscriptionReminderProcessor [expired]: Failed to send to userId={UserId}", c.Id);
                }
            }

            if (sentCount > 0)
            {
                logger.LogInformation(
                    "SubscriptionReminderProcessor [expired]: Sent {Count}/{Total} notifications.",
                    sentCount, candidates.Count);
            }
        }
    }
}
