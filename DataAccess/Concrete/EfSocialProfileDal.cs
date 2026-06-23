using Core.DataAccess.EntityFramework;
using Core.Utilities.Helpers;
using DataAccess.Abstract;
using DataAccess.Helpers;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Concrete
{
    public class EfSocialProfileDal : EfEntityRepositoryBase<SocialProfile, DatabaseContext>, ISocialProfileDal
    {
        private readonly DatabaseContext _context;

        public EfSocialProfileDal(DatabaseContext context) : base(context)
        {
            _context = context;
        }

        public async Task<SocialProfile?> GetByOwnerAsync(SocialProfileOwnerType ownerType, Guid ownerId)
        {
            return await _context.SocialProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.OwnerType == ownerType && p.OwnerId == ownerId);
        }

        public async Task<SocialProfile?> GetByUsernameAsync(string username)
        {
            var normalized = username.Trim().ToLowerInvariant();
            return await _context.SocialProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Username == normalized);
        }

        public async Task<bool> UsernameExistsAsync(string username)
        {
            var normalized = username.Trim().ToLowerInvariant();
            return await _context.SocialProfiles.AnyAsync(p => p.Username == normalized);
        }

        public async Task<List<SocialProfile>> GetByUserIdAsync(Guid userId)
        {
            return await _context.SocialProfiles
                .AsNoTracking()
                .Where(p => p.UserId == userId && p.Status == SocialContentStatus.Active)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();
        }

        public async Task<SocialProfileStatsDto> GetStatsAsync(Guid profileId, IReadOnlyList<Guid>? viewerProfileIds)
        {
            var postCount = await _context.SocialPosts
                .CountAsync(p =>
                    p.ProfileId == profileId &&
                    p.Status == SocialContentStatus.Active &&
                    p.Type != SocialPostType.Reel);
            var followerCount = await _context.SocialFollows
                .CountAsync(f => f.FollowingProfileId == profileId);
            var followingCount = await _context.SocialFollows
                .CountAsync(f => f.FollowerProfileId == profileId);

            var isFollowing = false;
            if (viewerProfileIds is { Count: > 0 })
            {
                isFollowing = await _context.SocialFollows.AnyAsync(f =>
                    viewerProfileIds.Contains(f.FollowerProfileId) &&
                    f.FollowingProfileId == profileId);
            }

            return new SocialProfileStatsDto
            {
                PostCount = postCount,
                FollowerCount = followerCount,
                FollowingCount = followingCount,
                IsFollowing = isFollowing,
            };
        }

        public async Task<List<(SocialProfile Profile, double? DistanceKm)>> SearchProfilesAsync(
            string? usernameQuery,
            double? latitude,
            double? longitude,
            double radiusKm,
            IReadOnlyCollection<Guid> blockedUserIds,
            Guid? excludeUserId,
            int limit,
            AvailabilityFilter? availability = null,
            IReadOnlyList<Guid>? serviceIds = null)
        {
            limit = Math.Clamp(limit, 1, 50);
            var hasQuery = !string.IsNullOrWhiteSpace(usernameQuery);
            var hasCoords = latitude.HasValue && longitude.HasValue;
            var applyGeoBox = hasCoords && radiusKm > 0 && radiusKm < FilterConstants.DiscoveryUnlimitedRadiusSentinelKm;
            var hasAvailabilityFilter = availability.HasValue && availability.Value != AvailabilityFilter.Any;
            var hasServiceFilter = serviceIds != null && serviceIds.Count > 0;

            if (!hasQuery && !hasCoords && !hasAvailabilityFilter && !hasServiceFilter)
                return new List<(SocialProfile, double?)>();

            var q = _context.SocialProfiles
                .AsNoTracking()
                .Where(p => p.Status == SocialContentStatus.Active);

            if (blockedUserIds.Count > 0)
                q = q.Where(p => !blockedUserIds.Contains(p.UserId));

            if (excludeUserId.HasValue)
                q = q.Where(p => p.UserId != excludeUserId.Value);

            if (hasQuery)
            {
                var normalized = usernameQuery!.Trim().ToLowerInvariant();
                q = q.Where(p => EF.Functions.ILike(p.Username, $"%{normalized}%"));
            }

            if (hasAvailabilityFilter)
            {
                q = q.Where(p => p.OwnerType != SocialProfileOwnerType.Customer);

                if (availability == AvailabilityFilter.Ready)
                {
                    q = q.Where(p =>
                        p.OwnerType != SocialProfileOwnerType.FreeBarber ||
                        _context.FreeBarbers.Any(fb => fb.Id == p.OwnerId && fb.IsAvailable));
                }
                else if (availability == AvailabilityFilter.NotReady)
                {
                    q = q.Where(p =>
                        p.OwnerType != SocialProfileOwnerType.FreeBarber ||
                        _context.FreeBarbers.Any(fb => fb.Id == p.OwnerId && !fb.IsAvailable));
                }
            }

            if (serviceIds != null && serviceIds.Count > 0)
            {
                var ids = serviceIds.ToList();
                var categoryNames = await ServiceFilterCategoryHelper.GetCategoryNamesByServiceIdsAsync(_context, ids);

                q = q.Where(p =>
                    (p.OwnerType == SocialProfileOwnerType.FreeBarber &&
                        _context.FreeBarbers.Any(fb => fb.Id == p.OwnerId &&
                            (_context.ServiceOfferings.Any(o =>
                                o.OwnerId == fb.Id &&
                                (ids.Contains(o.Id) || categoryNames.Contains(o.ServiceName))) ||
                             _context.ServicePackages.Any(pkg =>
                                pkg.OwnerId == fb.Id &&
                                pkg.Items.Any(i => categoryNames.Contains(i.ServiceName)))))) ||
                    (p.OwnerType == SocialProfileOwnerType.BarberStore &&
                        _context.BarberStores.Any(s => s.Id == p.OwnerId &&
                            (_context.ServiceOfferings.Any(o =>
                                o.OwnerId == s.Id &&
                                (ids.Contains(o.Id) || categoryNames.Contains(o.ServiceName))) ||
                             _context.ServicePackages.Any(pkg =>
                                pkg.OwnerId == s.Id &&
                                pkg.Items.Any(i => categoryNames.Contains(i.ServiceName)))))));
            }

            var fetchLimit = hasAvailabilityFilter ? Math.Min(limit * 4, 200) : Math.Min(limit * (applyGeoBox ? 4 : 1), 200);

            List<SocialProfile> candidates;
            if (applyGeoBox)
            {
                var lat = latitude!.Value;
                var lon = longitude!.Value;
                var (minLat, maxLat, minLon, maxLon) = GeoBounds.BoxKm(lat, lon, radiusKm);

                candidates = await q
                    .Where(p =>
                        p.Latitude != null &&
                        p.Longitude != null &&
                        p.Latitude >= minLat &&
                        p.Latitude <= maxLat &&
                        p.Longitude >= minLon &&
                        p.Longitude <= maxLon)
                    .Take(fetchLimit)
                    .ToListAsync();

                var withDistance = candidates
                    .Select(p =>
                    {
                        var dist = Geo.DistanceKm(lat, lon, p.Latitude!.Value, p.Longitude!.Value);
                        return (Profile: p, DistanceKm: (double?)dist);
                    })
                    .Where(x => x.DistanceKm <= radiusKm)
                    .OrderBy(x => x.DistanceKm)
                    .ThenBy(x => x.Profile.Username)
                    .ToList();

                return await ApplyStoreAvailabilityFilterAsync(withDistance, availability, limit);
            }

            if (hasCoords)
            {
                var lat = latitude!.Value;
                var lon = longitude!.Value;

                candidates = await q
                    .OrderBy(p => p.Username)
                    .Take(fetchLimit)
                    .ToListAsync();

                var withDistance = candidates
                    .Select(p =>
                    {
                        double? dist = null;
                        if (p.Latitude.HasValue && p.Longitude.HasValue)
                            dist = Geo.DistanceKm(lat, lon, p.Latitude.Value, p.Longitude.Value);
                        return (Profile: p, DistanceKm: dist);
                    })
                    .OrderBy(x => x.DistanceKm ?? double.MaxValue)
                    .ThenBy(x => x.Profile.Username)
                    .ToList();

                return await ApplyStoreAvailabilityFilterAsync(withDistance, availability, limit);
            }

            candidates = await q
                .OrderBy(p => p.Username)
                .Take(fetchLimit)
                .ToListAsync();

            var rows = candidates.Select(p => (p, (double?)null)).ToList();
            return await ApplyStoreAvailabilityFilterAsync(rows, availability, limit);
        }

        private async Task<List<(SocialProfile Profile, double? DistanceKm)>> ApplyStoreAvailabilityFilterAsync(
            List<(SocialProfile Profile, double? DistanceKm)> rows,
            AvailabilityFilter? availability,
            int limit)
        {
            if (!availability.HasValue || availability.Value == AvailabilityFilter.Any)
                return rows.Take(limit).ToList();

            var storeOwnerIds = rows
                .Where(x => x.Profile.OwnerType == SocialProfileOwnerType.BarberStore)
                .Select(x => x.Profile.OwnerId)
                .Distinct()
                .ToList();

            if (storeOwnerIds.Count == 0)
                return rows.Take(limit).ToList();

            var nowLocal = TimeZoneHelper.ToTurkeyTime(DateTime.UtcNow);
            var hoursRows = await _context.WorkingHours
                .AsNoTracking()
                .Where(h => storeOwnerIds.Contains(h.OwnerId))
                .ToListAsync();
            var hoursByStore = hoursRows
                .GroupBy(h => h.OwnerId)
                .ToDictionary(g => g.Key, g => g.ToList());

            bool wantOpen = availability == AvailabilityFilter.Ready;
            var filtered = rows.Where(x =>
            {
                if (x.Profile.OwnerType != SocialProfileOwnerType.BarberStore)
                    return true;

                var hours = hoursByStore.GetValueOrDefault(x.Profile.OwnerId, new List<WorkingHour>());
                var isOpen = hours.Count > 0 && OpenControl.IsOpenNow(hours, nowLocal);
                return isOpen == wantOpen;
            }).ToList();

            return filtered.Take(limit).ToList();
        }

        public async Task<bool> HasActiveStoryAsync(Guid profileId)
        {
            var now = DateTime.UtcNow;
            return await _context.SocialStories.AnyAsync(s =>
                s.ProfileId == profileId &&
                s.Status == SocialContentStatus.Active &&
                s.ExpiresAt > now);
        }

        public async Task<int> GetTotalPostViewsAsync(Guid profileId)
        {
            return await _context.SocialPosts
                .AsNoTracking()
                .Where(p => p.ProfileId == profileId && p.Status == SocialContentStatus.Active)
                .SumAsync(p => p.ViewCount);
        }

        public async Task<int> GetHighlightCountAsync(Guid profileId)
        {
            return await _context.SocialStoryHighlights
                .AsNoTracking()
                .CountAsync(h => h.ProfileId == profileId && h.Status == SocialContentStatus.Active);
        }

        public async Task<int> GetReelCountAsync(Guid profileId)
        {
            return await _context.SocialPosts
                .AsNoTracking()
                .CountAsync(p =>
                    p.ProfileId == profileId &&
                    p.Status == SocialContentStatus.Active &&
                    p.Type == SocialPostType.Reel);
        }
    }
}
