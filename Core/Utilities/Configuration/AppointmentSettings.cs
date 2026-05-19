namespace Core.Utilities.Configuration
{
    public class AppointmentSettings
    {
        /// <summary>Bekleyen (Pending) randevu adımları için süre (dakika). Varsayılan: 10.</summary>
        public int PendingTimeoutMinutes { get; set; } = 10;

        /// <summary>
        /// Randevu mesafe limiti (km).
        /// 0 veya negatif değer = LİMİTSİZ (uzaklık kontrolü hiç yapılmaz).
        /// Pozitif bir değer (örn. 10.0) verildiğinde Haversine ile mesafe doğrulanır.
        /// Default: 0 = limitsiz.
        /// </summary>
        public double MaxDistanceKm { get; set; } = 0;
        public int SlotMinutes { get; set; } = 60;

        /// <summary>
        /// Randevu bitiş saatinden itibaren kaç dakika içinde tamamlanmazsa otomatik tamamlanır.
        /// Default: 30 dakika
        /// </summary>
        public int AutoCompleteAfterMinutes { get; set; } = 30;

        /// <summary>
        /// 3'lü sistem (Customer -> FreeBarber -> Store) için özel ayarlar
        /// </summary>
        public StoreSelectionSettings StoreSelection { get; set; } = new();
    }

    /// <summary>
    /// 3'lü randevu sistemi (StoreSelection) için süre ayarları
    /// </summary>
    public class StoreSelectionSettings
    {
        /// <summary>
        /// Toplam süre (dakika) - Randevu oluşturulduğundan itibaren geçerli
        /// Default: 30 dakika
        /// </summary>
        public int TotalMinutes { get; set; } = 30;

        /// <summary>
        /// Dükkan onay süresi (dakika) - Her dükkan seçiminde başlar
        /// Default: 10 dakika
        /// </summary>
        public int StoreStepMinutes { get; set; } = 10;
    }

    public class BackgroundServicesSettings
    {
        public int AppointmentTimeoutWorkerIntervalSeconds { get; set; } = 300; // 5 dakika (300 saniye) - appsettings.json ile uyumlu
    }
}

