using Entities.Concrete.Dto;

namespace Business.Helpers
{
    public static class SocialDiscoverFilterHelper
    {
        public static bool ShouldApplyGeoBox(double radiusKm) =>
            radiusKm > 0 && radiusKm < FilterConstants.DiscoveryUnlimitedRadiusSentinelKm;

        public static double NormalizeRadiusKm(double radiusKm)
        {
            if (radiusKm >= FilterConstants.DiscoveryUnlimitedRadiusSentinelKm)
                return FilterConstants.DiscoveryUnlimitedRadiusSentinelKm;
            if (radiusKm <= 0)
                return 0;
            return Math.Clamp(radiusKm, 1, 500);
        }
    }
}
