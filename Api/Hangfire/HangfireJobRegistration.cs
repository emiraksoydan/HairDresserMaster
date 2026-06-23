using Api.BackgroundServices;
using Core.Utilities.Configuration;
using Hangfire;

namespace Api.Hangfire
{
    public static class HangfireJobRegistration
    {
        public const string AppointmentTimeoutJobId = "appointment-timeout-cycle";
        public const string SubscriptionReminderJobId = "subscription-reminder-cycle";

        public static void RegisterRecurringJobs(HangfireSettings settings, BackgroundServicesSettings backgroundSettings)
        {
            var appointmentMinutes = settings.AppointmentTimeoutIntervalMinutes > 0
                ? settings.AppointmentTimeoutIntervalMinutes
                : Math.Max(1, (int)Math.Ceiling(backgroundSettings.AppointmentTimeoutWorkerIntervalSeconds / 60.0));

            var subscriptionMinutes = Math.Max(1, settings.SubscriptionReminderIntervalMinutes);

            RecurringJob.AddOrUpdate<AppointmentTimeoutProcessor>(
                AppointmentTimeoutJobId,
                processor => processor.RunCycleAsync(CancellationToken.None),
                Cron.MinuteInterval(appointmentMinutes));

            RecurringJob.AddOrUpdate<SubscriptionReminderProcessor>(
                SubscriptionReminderJobId,
                processor => processor.RunCycleAsync(CancellationToken.None),
                Cron.MinuteInterval(subscriptionMinutes));
        }
    }
}
