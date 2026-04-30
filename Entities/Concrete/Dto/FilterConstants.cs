namespace Entities.Concrete.Dto
{
    /// <summary>Backend / FE ile aynı varsayılan yarıçap (km).</summary>
    public static class FilterConstants
    {
        public const double DefaultDistanceKm = 50.0;

        /// <summary>
        /// Keşif &quot;sınırsız&quot; seçimi ile uyumlu eşik (km). Bu değer ve üzerinde
        /// mesafe kutusu uygulanmaz; yoksa büyük yarıçapta boylam aralığı hatalı daralır.
        /// </summary>
        public const double DiscoveryUnlimitedRadiusSentinelKm = 20000.0;

        /// <summary>Kayıtlı filtre JSON şeması (criteria sarmalayıcı ile uyumlu).</summary>
        public const int CurrentFilterSchemaVersion = 1;
    }
}
