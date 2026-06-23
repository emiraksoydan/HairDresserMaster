namespace Core.Utilities.Configuration
{
    public class HangfireSettings
    {
        public bool Enabled { get; set; } = true;
        public string DashboardPath { get; set; } = "/hangfire";
        public int WorkerCount { get; set; } = 2;
        public int AppointmentTimeoutIntervalMinutes { get; set; } = 4;
        public int SubscriptionReminderIntervalMinutes { get; set; } = 30;
    }
}
