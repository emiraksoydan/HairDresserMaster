using Entities.Concrete.Dto;

namespace DataAccess.Helpers
{
    internal static class FilterRequestExtensions
    {
        public static double GetEffectiveDistanceKm(this FilterRequestDto f) =>
            f.DistanceKm > 0 ? f.DistanceKm : FilterConstants.DefaultDistanceKm;

        /// <summary>
        /// Keşif listelerinde coğrafi kutu uygulanacak mı. Sınırsız (sentinel) mesafede kutu atlanır.
        /// </summary>
        public static bool ShouldApplyDiscoveryGeoBox(this FilterRequestDto f) =>
            f.Latitude.HasValue
            && f.Longitude.HasValue
            && f.GetEffectiveDistanceKm() < FilterConstants.DiscoveryUnlimitedRadiusSentinelKm;
    }
}
