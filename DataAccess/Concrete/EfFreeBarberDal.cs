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
    public class EfFreeBarberDal : EfEntityRepositoryBase<FreeBarber, DatabaseContext>, IFreeBarberDal
    {
        private readonly DatabaseContext _context;
        public EfFreeBarberDal(DatabaseContext context) : base(context)
        {
            _context = context;
        }

        public async Task<FreeBarberMinePanelDto> GetFreeBarberForUsers(Guid freeBarberId)
        {
            // Frontend bildirim / harita akışı targetId olarak FreeBarbers.Id veya FreeBarberUserId (UserId) ile gelebilir.
            var freeBarber = await _context.FreeBarbers
              .AsNoTracking()
              .Where(b => b.Id == freeBarberId || b.FreeBarberUserId == freeBarberId)
              .Select(s => new
              {
                  s.Id,
                  s.FreeBarberUserId,
                  s.Latitude,
                  s.Longitude,
                  s.Type,
                  s.FirstName,
                  s.LastName,
                  s.BarberCertificateImageId,
                  s.BeautySalonCertificateImageId,
                  s.IsAvailable,
              })
              .FirstOrDefaultAsync();

            if (freeBarber is null)
                return new FreeBarberMinePanelDto();

            // Rating - Artık TargetId User ID
            var avgRating = await _context.Ratings
            .AsNoTracking()
            .Where(r => r.TargetId == freeBarber.FreeBarberUserId)
            .Select(r => (double?)r.Score)
            .AverageAsync() ?? 0.0;

            var reviewCount = await _context.Ratings
                .AsNoTracking()
                .CountAsync(r => r.TargetId == freeBarber.FreeBarberUserId);

            // Favorite count (sadece aktif favoriler) - Artık User ID'ler arasında
            var favoriteCount = await _context.Favorites
                .AsNoTracking()
                .CountAsync(f => f.FavoritedToId == freeBarber.FreeBarberUserId && f.IsActive);


            var images = await _context.Images
                .AsNoTracking()
                .Where(i => i.ImageOwnerId == freeBarber.Id && i.OwnerType == ImageOwnerType.FreeBarber && i.Id != freeBarber.BarberCertificateImageId && i.Id != freeBarber.BeautySalonCertificateImageId)
                .Select(i => new ImageGetDto
                {
                    Id = i.Id,
                    ImageUrl = i.ImageUrl
                })
                .ToListAsync();

            var offerings = await _context.ServiceOfferings
                .AsNoTracking()
                .Where(o => o.OwnerId == freeBarber.Id)
                .Select(o => new ServiceOfferingGetDto
                {
                    Id = o.Id,
                    ServiceName = o.ServiceName,
                    Price = o.Price
                })
                .ToListAsync();

            return new FreeBarberMinePanelDto
            {
                Id = freeBarber.Id,
                FreeBarberUserId = freeBarber.FreeBarberUserId,
                Type = freeBarber.Type,
                FullName = freeBarber.FirstName + " " + freeBarber.LastName,
                IsAvailable = freeBarber.IsAvailable,
                Latitude = freeBarber.Latitude,
                Longitude = freeBarber.Longitude,
                ImageList = images,
                Offerings = offerings,
                FavoriteCount = favoriteCount,
                Rating = avgRating,
                ReviewCount = reviewCount,
                BeautySalonCertificateImageId = freeBarber.BeautySalonCertificateImageId,
            };
        }

        public async Task<FreeBarberMinePanelDto> GetMyPanel(Guid currentUserId)
        {
            var freeBarber = await _context.FreeBarbers
               .AsNoTracking()
               .Where(b => b.FreeBarberUserId == currentUserId)
               .Join(_context.Users, fb => fb.FreeBarberUserId, u => u.Id, (fb, u) => new
               {
                   fb.Id,
                   fb.FreeBarberUserId,
                   fb.Latitude,
                   fb.Longitude,
                   fb.Type,
                   fb.FirstName,
                   fb.LastName,
                   fb.BarberCertificateImageId,
                   fb.BeautySalonCertificateImageId,
                   fb.IsAvailable,
                   u.CustomerNumber,
               })
               .FirstOrDefaultAsync();

            if (freeBarber is null)
                return new FreeBarberMinePanelDto();

            // Rating - Artık TargetId User ID
            var avgRating = await _context.Ratings
            .AsNoTracking()
            .Where(r => r.TargetId == freeBarber.FreeBarberUserId)
            .Select(r => (double?)r.Score)
            .AverageAsync() ?? 0.0;

            var reviewCount = await _context.Ratings
                .AsNoTracking()
                .CountAsync(r => r.TargetId == freeBarber.FreeBarberUserId);

            // Favorite count (sadece aktif favoriler) - Artık User ID'ler arasında
            var favoriteCount = await _context.Favorites
                .AsNoTracking()
                .CountAsync(f => f.FavoritedToId == freeBarber.FreeBarberUserId && f.IsActive);


            var images = await _context.Images
                .AsNoTracking()
                .Where(i => i.ImageOwnerId == freeBarber.Id && i.OwnerType == ImageOwnerType.FreeBarber && i.Id != freeBarber.BarberCertificateImageId && i.Id != freeBarber.BeautySalonCertificateImageId)
                .Select(i => new ImageGetDto
                {
                    Id = i.Id,
                    ImageUrl = i.ImageUrl
                })
                .ToListAsync();

            var offerings = await _context.ServiceOfferings
                .AsNoTracking()
                .Where(o => o.OwnerId == freeBarber.Id)
                .Select(o => new ServiceOfferingGetDto
                {
                    Id = o.Id,
                    ServiceName = o.ServiceName,
                    Price = o.Price
                })
                .ToListAsync();

            return new FreeBarberMinePanelDto
            {
                Id = freeBarber.Id,
                FreeBarberUserId = freeBarber.FreeBarberUserId,
                Type = freeBarber.Type,
                FullName = freeBarber.FirstName + " " + freeBarber.LastName,
                CustomerNumber = freeBarber.CustomerNumber,
                IsAvailable = freeBarber.IsAvailable,
                ImageList = images,
                Offerings = offerings,
                FavoriteCount = favoriteCount,
                Rating = avgRating,
                ReviewCount = reviewCount,
                Latitude = freeBarber.Latitude,
                Longitude = freeBarber.Longitude,
                BeautySalonCertificateImageId = freeBarber.BeautySalonCertificateImageId,
            };
        }

        public async Task<List<FreeBarberGetDto>> GetNearbyFreeBarberAsync(
            double lat,
            double lon,
            double radiusKm = 10,
            Guid? currentUserId = null,
            int limit = 100,
            IReadOnlyCollection<Guid>? blockedUserIds = null)
        {
            limit = Math.Clamp(limit, 1, 200);
            var nowLocal = TimeZoneHelper.ToTurkeyTime(DateTime.UtcNow);
            var (minLat, maxLat, minLon, maxLon) = GeoBounds.BoxKm(lat, lon, radiusKm);

            IQueryable<FreeBarber> baseQuery = _context.FreeBarbers
                .AsNoTracking()
                .Where(s => s.Latitude >= minLat && s.Latitude <= maxLat
                         && s.Longitude >= minLon && s.Longitude <= maxLon);

            // Blocked filter DB'de — eski in-memory FilterBlockedUsersAsync yerine.
            if (blockedUserIds != null && blockedUserIds.Count > 0)
            {
                var blocked = blockedUserIds.ToList();
                baseQuery = baseQuery.Where(s => !blocked.Contains(s.FreeBarberUserId));
            }

            var freeBarbers = await baseQuery
                .Select(s => new
                {
                    s.Id,
                    s.Latitude,
                    s.Longitude,
                    s.Type,
                    s.FirstName,
                    s.LastName,
                    s.FreeBarberUserId,
                    s.IsAvailable,
                    s.BarberCertificateImageId,
                    s.BeautySalonCertificateImageId

                })
                .ToListAsync();

            if (!freeBarbers.Any())
                return new List<FreeBarberGetDto>();
            var freeBarberIds = freeBarbers.Select(s => s.Id).ToList();
            // Rating - TargetId = FreeBarber User ID (FreeBarber'ın rating'i)
            var freeBarberOwnerIds = freeBarbers.Select(s => s.FreeBarberUserId).Distinct().ToList();
            var ratingStats = await _context.Ratings
                .AsNoTracking()
                .Where(r => freeBarberOwnerIds.Contains(r.TargetId))
                .GroupBy(r => r.TargetId)
                .Select(g => new
                {
                    OwnerUserId = g.Key,
                    AvgRating = g.Average(x => (double)x.Score),
                    ReviewCount = g.Count()

                })
                .ToListAsync();

            var ratingDict = ratingStats
                .ToDictionary(x => x.OwnerUserId, x => new { x.AvgRating, x.ReviewCount });
            
            // Favorite count (sadece aktif favoriler) - FavoritedToId = FreeBarber User ID (FreeBarber'ın favori sayısı)
            var favoriteStats = await _context.Favorites
                .AsNoTracking()
                .Where(f => freeBarberOwnerIds.Contains(f.FavoritedToId) && f.IsActive)
                .GroupBy(f => f.FavoritedToId)
                .Select(g => new
                {
                    OwnerUserId = g.Key,
                    FavoriteCount = g.Count(f => f.IsActive)
                })
                .ToListAsync();

            var favoriteDict = favoriteStats
                .ToDictionary(x => x.OwnerUserId, x => x.FavoriteCount);

            // User IsFavorited bilgisi
            var isFavoritedDict = new Dictionary<Guid, bool>();
            if (currentUserId.HasValue)
            {
                var userFavs = await _context.Favorites
                    .AsNoTracking()
                    .Where(f => f.FavoritedFromId == currentUserId.Value && freeBarberOwnerIds.Contains(f.FavoritedToId) && f.IsActive)
                    .Select(f => f.FavoritedToId)
                    .ToListAsync();
                
                isFavoritedDict = userFavs.ToDictionary(x => x, x => true);
            }

            var offeringGroups = await _context.ServiceOfferings
                .AsNoTracking()
                .Where(o => freeBarberIds.Contains(o.OwnerId))
                .GroupBy(o => o.OwnerId)
                .Select(g => new
                {
                    OwnerId = g.Key,
                    Offerings = g
                        .Select(o => new ServiceOfferingGetDto
                        {
                            Id = o.Id,
                            ServiceName = o.ServiceName,
                            Price = o.Price
                        })
                        .ToList()
                })
                .ToListAsync();
            var offeringDict = offeringGroups
                .ToDictionary(x => x.OwnerId, x => x.Offerings);

            var imageGroups = await _context.Images
                .AsNoTracking()
                .Where(i => i.OwnerType == ImageOwnerType.FreeBarber
                         && freeBarberIds.Contains(i.ImageOwnerId))
                .GroupBy(i => i.ImageOwnerId)
                .Select(g => new
                {
                    OwnerId = g.Key,
                    Images = g.Select(i => new ImageGetDto
                    {
                        Id = i.Id,
                        ImageUrl = i.ImageUrl
                    }).ToList()
                })
                .ToListAsync();
            var imageDict = imageGroups
                .ToDictionary(x => x.OwnerId, x => x.Images);
            var result = freeBarbers
                .Select(s =>
                {
                    var distance = Geo.DistanceKm(lat, lon, s.Latitude, s.Longitude);
                    if (distance > radiusKm) return null;

                    ratingDict.TryGetValue(s.FreeBarberUserId, out var ratingInfo); // Artık owner User ID'ye göre
                    favoriteDict.TryGetValue(s.FreeBarberUserId, out var favCount); // Artık owner User ID'ye göre
                    offeringDict.TryGetValue(s.Id, out var offerings);
                    imageDict.TryGetValue(s.Id, out var images);

                    var avgRating = ratingInfo?.AvgRating ?? 0;
                    var reviewCount = ratingInfo?.ReviewCount ?? 0;
                    var favoriteCount = favCount;
                    var isFavorited = isFavoritedDict.GetValueOrDefault(s.FreeBarberUserId, false);

                    return new FreeBarberGetDto
                    {
                        Id = s.Id,
                        FreeBarberUserId = s.FreeBarberUserId,
                        IsAvailable = s.IsAvailable,
                        ImageList = images?.Where(img => img.Id != s.BarberCertificateImageId && img.Id != s.BeautySalonCertificateImageId).ToList() ?? new List<ImageGetDto>(),
                        Type = s.Type,
                        Latitude = s.Latitude,
                        Longitude = s.Longitude,
                        FullName = s.FirstName + " " + s.LastName,
                        FavoriteCount = favoriteCount,
                        ReviewCount = reviewCount,
                        Rating = Math.Round(avgRating, 2),
                        Offerings = offerings ?? new List<ServiceOfferingGetDto>(),
                        DistanceKm = Math.Round(distance, 3),
                        IsFavorited = isFavorited,
                        BeautySalonCertificateImageId = s.BeautySalonCertificateImageId
                    };
                })
                .Where(dto => dto != null)
                .OrderBy(dto => dto!.DistanceKm)
                .ThenByDescending(dto => dto!.Rating)
                .Take(limit)
                .ToList()!;

            return result;
        }

        public async Task<FreeBarberMinePanelDetailDto> GetPanelDetailById(Guid panelId)
        {
            var freeBarber = await _context.FreeBarbers
               .AsNoTracking()
               .Where(b => b.Id == panelId)
               .Select(s => new
               {
                   s.Id,
                   s.FreeBarberUserId,
                   s.Type,
                   s.FirstName,
                   s.LastName,
                   s.BarberCertificateImageId,
                   s.BeautySalonCertificateImageId,
                   s.IsAvailable,
                   s.Latitude,
                   s.Longitude,

               })
               .FirstOrDefaultAsync();

            if (freeBarber is null)
                return new FreeBarberMinePanelDetailDto();

            var images = await _context.Images
                .AsNoTracking()
                .Where(i => i.ImageOwnerId == freeBarber.Id && i.OwnerType == ImageOwnerType.FreeBarber && i.Id != freeBarber.BarberCertificateImageId && i.Id != freeBarber.BeautySalonCertificateImageId)
                .Select(i => new ImageGetDto
                {
                    Id = i.Id,
                    ImageUrl = i.ImageUrl
                })
                .ToListAsync();

            var offerings = await _context.ServiceOfferings
                .AsNoTracking()
                .Where(o => o.OwnerId == freeBarber.Id)
                .Select(o => new ServiceOfferingGetDto
                {
                    Id = o.Id,
                    ServiceName = o.ServiceName,
                    Price = o.Price
                })
                .ToListAsync();

            // Fetch barber certificate image if exists
            ImageGetDto certificateImageDto = null;
            if (freeBarber.BarberCertificateImageId.HasValue)
            {
                var certImage = await _context.Images
                    .AsNoTracking()
                    .FirstOrDefaultAsync(i => i.Id == freeBarber.BarberCertificateImageId.Value);

                if (certImage != null)
                {
                    certificateImageDto = new ImageGetDto
                    {
                        Id = certImage.Id,
                        ImageUrl = certImage.ImageUrl,
                    };
                }
            }

            // Fetch beauty salon certificate image if exists
            ImageGetDto beautySalonCertificateImageDto = null;
            if (freeBarber.BeautySalonCertificateImageId.HasValue)
            {
                var beautyCertImage = await _context.Images
                    .AsNoTracking()
                    .FirstOrDefaultAsync(i => i.Id == freeBarber.BeautySalonCertificateImageId.Value);

                if (beautyCertImage != null)
                {
                    beautySalonCertificateImageDto = new ImageGetDto
                    {
                        Id = beautyCertImage.Id,
                        ImageUrl = beautyCertImage.ImageUrl,
                    };
                }
            }

            return new FreeBarberMinePanelDetailDto
            {
                Id = freeBarber.Id,
                FreeBarberUserId = freeBarber.FreeBarberUserId,
                Type = freeBarber.Type,
                FirstName = freeBarber.FirstName,
                LastName = freeBarber.LastName,
                IsAvailable = freeBarber.IsAvailable,
                BarberCertificateImageId = freeBarber.BarberCertificateImageId,
                BarberCertificateImage = certificateImageDto,
                BeautySalonCertificateImageId = freeBarber.BeautySalonCertificateImageId,
                BeautySalonCertificateImage = beautySalonCertificateImageDto,
                ImageList = images,
                Offerings = offerings,
                Latitude = freeBarber.Latitude,
                Longitude = freeBarber.Longitude,
            };
        }

        public async Task<bool> TryLockAsync(Guid freeBarberUserId)
        {
            var rows = await _context.FreeBarbers
                .Where(x => x.FreeBarberUserId == freeBarberUserId && x.IsAvailable)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.IsAvailable, false)
                    .SetProperty(x => x.UpdatedAt, DateTime.UtcNow));
            return rows > 0;
        }

        public async Task<List<FreeBarberGetDto>> GetFilteredFreeBarbersAsync(
            FilterRequestDto filter,
            int limit = 100,
            int offset = 0,
            IReadOnlyCollection<Guid>? blockedUserIds = null)
        {
            // ---------------------------------------------------------------------------
            // GERÇEK DB PAGINATION (EfBarberStoreDal.GetFilteredStoresAsync ile simetrik).
            //
            // Store tarafından farkı: Rating/Favorite keys panel Id değil FreeBarberUserId;
            // PriceSort alanı paneldeki offering'lerin MIN'i (subquery).
            //
            // Availability SQL tarafında IsAvailable olarak uygulanır; ek post-filter yok → sayfa tam dolu
            // gelir (WHERE tamamen SQL'e indi).
            // ---------------------------------------------------------------------------

            limit = Math.Clamp(limit, 1, 200);
            if (offset < 0) offset = 0;

            // 0. Kendi panel Id'si (IsOwnPanel + block muafiyeti)
            Guid? ownFreeBarberPanelId = null;
            if (filter.CurrentUserId.HasValue)
            {
                ownFreeBarberPanelId = await _context.FreeBarbers
                    .AsNoTracking()
                    .Where(fb => fb.FreeBarberUserId == filter.CurrentUserId.Value)
                    .Select(fb => fb.Id)
                    .FirstOrDefaultAsync();
            }

            IQueryable<FreeBarber> query = _context.FreeBarbers.AsNoTracking();

            // 1. Konum — sınırsız (sentinel) seçildiğinde kutu uygulanmaz.
            if (filter.ShouldApplyDiscoveryGeoBox())
            {
                var distance = filter.GetEffectiveDistanceKm();
                var (minLat, maxLat, minLon, maxLon) = GeoBounds.BoxKm(
                    filter.Latitude.Value, filter.Longitude.Value, distance);
                query = query.Where(fb =>
                    (ownFreeBarberPanelId.HasValue && fb.Id == ownFreeBarberPanelId.Value) ||
                    (fb.Latitude >= minLat && fb.Latitude <= maxLat &&
                     fb.Longitude >= minLon && fb.Longitude <= maxLon));
            }

            // 2. İsim
            if (!string.IsNullOrWhiteSpace(filter.SearchQuery))
            {
                var searchLower = filter.SearchQuery.ToLower();
                query = query.Where(fb =>
                    (ownFreeBarberPanelId.HasValue && fb.Id == ownFreeBarberPanelId.Value) ||
                    (fb.FirstName + " " + fb.LastName).ToLower().Contains(searchLower));
            }

            // 3. Kategori
            if (filter.MainCategory.HasValue)
            {
                var mc = filter.MainCategory.Value;
                query = query.Where(fb =>
                    (ownFreeBarberPanelId.HasValue && fb.Id == ownFreeBarberPanelId.Value) ||
                    fb.Type == mc);
            }

            // 4. Müsaitlik (Availability: Ready / NotReady; Any veya null = tümü)
            if (filter.Availability.HasValue && filter.Availability.Value != AvailabilityFilter.Any)
            {
                bool wantReady = filter.Availability.Value == AvailabilityFilter.Ready;
                query = query.Where(fb =>
                    (ownFreeBarberPanelId.HasValue && fb.Id == ownFreeBarberPanelId.Value) ||
                    fb.IsAvailable == wantReady);
            }

            // 5. Hizmet filtresi (offerings + packages EXISTS)
            if (filter.ServiceIds != null && filter.ServiceIds.Any())
            {
                var serviceIds = filter.ServiceIds.ToList();
                var categoryNames = await ServiceFilterCategoryHelper.GetCategoryNamesByServiceIdsAsync(
                    _context, serviceIds);

                query = query.Where(fb =>
                    (ownFreeBarberPanelId.HasValue && fb.Id == ownFreeBarberPanelId.Value) ||
                    _context.ServiceOfferings.Any(o =>
                        o.OwnerId == fb.Id &&
                        (serviceIds.Contains(o.Id) || categoryNames.Contains(o.ServiceName))) ||
                    _context.ServicePackages.Any(p =>
                        p.OwnerId == fb.Id &&
                        p.Items.Any(i => categoryNames.Contains(i.ServiceName))));
            }

            // 6. Fiyat aralığı — paneldeki offering'lerin MIN'i. Eski semantik korunur:
            //    "en az bir offering'i varsa en düşük fiyat aralık içinde olmalı".
            if (filter.MinPrice.HasValue)
            {
                var minP = filter.MinPrice.Value;
                query = query.Where(fb =>
                    (ownFreeBarberPanelId.HasValue && fb.Id == ownFreeBarberPanelId.Value) ||
                    _context.ServiceOfferings.Where(o => o.OwnerId == fb.Id)
                        .Min(o => (decimal?)o.Price) >= minP);
            }
            if (filter.MaxPrice.HasValue)
            {
                var maxP = filter.MaxPrice.Value;
                query = query.Where(fb =>
                    (ownFreeBarberPanelId.HasValue && fb.Id == ownFreeBarberPanelId.Value) ||
                    _context.ServiceOfferings.Where(o => o.OwnerId == fb.Id)
                        .Min(o => (decimal?)o.Price) <= maxP);
            }

            // 7. FavoritesOnly — FreeBarberUserId key
            if (filter.FavoritesOnly == true && filter.CurrentUserId.HasValue)
            {
                var favUserId = filter.CurrentUserId.Value;
                query = query.Where(fb =>
                    (ownFreeBarberPanelId.HasValue && fb.Id == ownFreeBarberPanelId.Value) ||
                    _context.Favorites.Any(f =>
                        f.FavoritedFromId == favUserId &&
                        f.FavoritedToId == fb.FreeBarberUserId &&
                        f.IsActive));
            }

            // 8. MinRating — FreeBarberUserId hedefli rating subquery
            if (filter.MinRating.HasValue && filter.MinRating.Value > 0)
            {
                double minRating = filter.MinRating.Value;
                query = query.Where(fb =>
                    (ownFreeBarberPanelId.HasValue && fb.Id == ownFreeBarberPanelId.Value) ||
                    (_context.Ratings
                        .Where(r => r.TargetId == fb.FreeBarberUserId)
                        .Select(r => (double?)r.Score)
                        .Average() ?? 0.0) >= minRating);
            }

            // 9. Blocked — kendi panel bloktan muaf
            if (blockedUserIds != null && blockedUserIds.Count > 0)
            {
                var blocked = blockedUserIds.ToList();
                query = query.Where(fb =>
                    (ownFreeBarberPanelId.HasValue && fb.Id == ownFreeBarberPanelId.Value) ||
                    !blocked.Contains(fb.FreeBarberUserId));
            }

            // 10. Sıralama
            IOrderedQueryable<FreeBarber> ordered;
            if (!string.IsNullOrWhiteSpace(filter.PriceSort) && filter.PriceSort != "none")
            {
                // Min offering price subquery; offering'siz paneller en sona/en öne dağılır
                // (NULL sıralama veritabanına bağlı, PostgreSQL: NULLs LAST by default DESC).
                if (filter.PriceSort == "asc")
                {
                    ordered = query.OrderBy(fb =>
                        _context.ServiceOfferings.Where(o => o.OwnerId == fb.Id)
                            .Min(o => (decimal?)o.Price) ?? decimal.MaxValue);
                }
                else
                {
                    ordered = query.OrderByDescending(fb =>
                        _context.ServiceOfferings.Where(o => o.OwnerId == fb.Id)
                            .Min(o => (decimal?)o.Price) ?? 0m);
                }
            }
            else
            {
                ordered = query.OrderByDescending(fb =>
                    _context.Ratings
                        .Where(r => r.TargetId == fb.FreeBarberUserId)
                        .Select(r => (double?)r.Score)
                        .Average() ?? 0.0);
            }
            ordered = ordered.ThenByDescending(fb => fb.Id);

            // 11. DB pagination
            var pagedPanels = await ordered
                .Skip(offset)
                .Take(limit)
                .Select(fb => new
                {
                    fb.Id,
                    fb.FreeBarberUserId,
                    fb.FirstName,
                    fb.LastName,
                    fb.Latitude,
                    fb.Longitude,
                    fb.Type,
                    fb.IsAvailable,
                    fb.BarberCertificateImageId,
                    fb.BeautySalonCertificateImageId
                })
                .ToListAsync();

            if (pagedPanels.Count == 0)
                return new List<FreeBarberGetDto>();

            var freeBarberIds = pagedPanels.Select(fb => fb.Id).ToList();
            var freeBarberUserIds = pagedPanels.Select(fb => fb.FreeBarberUserId).Distinct().ToList();

            // 12. Enrichment (yalnızca sayfadaki paneller için)
            var offerings = await _context.ServiceOfferings
                .AsNoTracking()
                .Where(o => freeBarberIds.Contains(o.OwnerId))
                .Select(o => new
                {
                    o.OwnerId,
                    Offering = new ServiceOfferingGetDto
                    {
                        Id = o.Id,
                        ServiceName = o.ServiceName,
                        Price = o.Price
                    }
                })
                .ToListAsync();
            var offeringsDict = offerings
                .GroupBy(o => o.OwnerId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Offering).ToList());

            var ratingStats = await _context.Ratings
                .AsNoTracking()
                .Where(r => freeBarberUserIds.Contains(r.TargetId))
                .GroupBy(r => r.TargetId)
                .Select(g => new
                {
                    FreeBarber = g.Key,
                    AvgRating = g.Average(x => (double)x.Score),
                    ReviewCount = g.Count()
                })
                .ToListAsync();
            var ratingDict = ratingStats.ToDictionary(x => x.FreeBarber, x => new { x.AvgRating, x.ReviewCount });

            var favoriteStats = await _context.Favorites
                .AsNoTracking()
                .Where(f => freeBarberUserIds.Contains(f.FavoritedToId) && f.IsActive)
                .GroupBy(f => f.FavoritedToId)
                .Select(g => new { FreeBarber = g.Key, FavoriteCount = g.Count() })
                .ToListAsync();
            var favoriteDict = favoriteStats.ToDictionary(x => x.FreeBarber, x => x.FavoriteCount);

            var isFavoritedDict = new Dictionary<Guid, bool>();
            if (filter.CurrentUserId.HasValue)
            {
                var fromId = filter.CurrentUserId.Value;
                var userFavs = await _context.Favorites
                    .AsNoTracking()
                    .Where(f => f.FavoritedFromId == fromId && freeBarberUserIds.Contains(f.FavoritedToId))
                    .Select(f => new { f.FavoritedToId, f.IsActive })
                    .ToListAsync();
                isFavoritedDict = userFavs.ToDictionary(x => x.FavoritedToId, x => x.IsActive);
            }

            var images = await _context.Images
                .AsNoTracking()
                .Where(i => i.OwnerType == ImageOwnerType.FreeBarber && freeBarberIds.Contains(i.ImageOwnerId))
                .Select(i => new
                {
                    i.ImageOwnerId,
                    Image = new ImageGetDto { Id = i.Id, ImageUrl = i.ImageUrl }
                })
                .ToListAsync();
            var imagesDict = images
                .GroupBy(i => i.ImageOwnerId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Image).ToList());

            var userNumberList = await _context.Users
                .AsNoTracking()
                .Where(u => freeBarberUserIds.Contains(u.Id))
                .Select(u => new { u.Id, u.CustomerNumber })
                .ToListAsync();
            var userNumberDict = userNumberList.ToDictionary(u => u.Id, u => u.CustomerNumber);

            // 13. DTO assembly (sıralama IQueryable'dan gelir)
            var result = pagedPanels.Select(fb =>
            {
                var rating = ratingDict.GetValueOrDefault(fb.FreeBarberUserId);
                var fbOfferings = offeringsDict.GetValueOrDefault(fb.Id, new List<ServiceOfferingGetDto>());
                bool isOwnPanel = ownFreeBarberPanelId.HasValue && fb.Id == ownFreeBarberPanelId.Value;

                return new FreeBarberGetDto
                {
                    Id = fb.Id,
                    FreeBarberUserId = fb.FreeBarberUserId,
                    FullName = $"{fb.FirstName} {fb.LastName}",
                    Latitude = fb.Latitude,
                    Longitude = fb.Longitude,
                    Type = fb.Type,
                    IsAvailable = fb.IsAvailable,
                    Rating = rating != null ? Math.Round(rating.AvgRating, 2) : 0,
                    ReviewCount = rating?.ReviewCount ?? 0,
                    FavoriteCount = favoriteDict.GetValueOrDefault(fb.FreeBarberUserId, 0),
                    IsFavorited = isFavoritedDict.GetValueOrDefault(fb.FreeBarberUserId, false),
                    Offerings = fbOfferings,
                    ImageList = imagesDict.GetValueOrDefault(fb.Id, new List<ImageGetDto>())
                        .Where(img => img.Id != fb.BarberCertificateImageId && img.Id != fb.BeautySalonCertificateImageId)
                        .ToList(),
                    IsOwnPanel = isOwnPanel,
                    BeautySalonCertificateImageId = fb.BeautySalonCertificateImageId,
                    CustomerNumber = userNumberDict.GetValueOrDefault(fb.FreeBarberUserId)
                };
            }).ToList();

            return result;
        }

        public async Task<EarningsDto> GetEarningsAsync(Guid freeBarberUserId, DateTime startDate, DateTime endDate)
        {
            var todayUtc = DateTime.UtcNow.Date;
            var startDateUtc = DateTime.SpecifyKind(startDate.Date, DateTimeKind.Utc);
            var endDateInclusive = DateTime.SpecifyKind(endDate.Date.AddDays(1), DateTimeKind.Utc);
            var previousStartDays = (endDate.Date - startDate.Date).TotalDays + 1;
            var previousStart = DateTime.SpecifyKind(startDate.Date.AddDays(-previousStartDays), DateTimeKind.Utc);

            // Tamamlanan randevular
            var appointments = await _context.Appointments
                .AsNoTracking()
                .Include(a => a.ServiceOfferings)
                .Where(a => a.FreeBarberUserId == freeBarberUserId
                         && a.Status == AppointmentStatus.Completed
                         && a.CompletedAt.HasValue
                         && a.CompletedAt.Value >= startDateUtc
                         && a.CompletedAt.Value < endDateInclusive)
                .ToListAsync();

            // Önceki dönem
            var previousAppointments = await _context.Appointments
                .AsNoTracking()
                .Include(a => a.ServiceOfferings)
                .Where(a => a.FreeBarberUserId == freeBarberUserId
                         && a.Status == AppointmentStatus.Completed
                         && a.CompletedAt.HasValue
                         && a.CompletedAt.Value >= previousStart
                         && a.CompletedAt.Value < startDateUtc)
                .ToListAsync();

            // İlgili dükkanların fiyatlandırma bilgisi
            var storeIds = appointments
                .Where(a => a.StoreId.HasValue)
                .Select(a => a.StoreId!.Value)
                .Distinct()
                .ToList();

            var storePricingMap = await _context.BarberStores
                .AsNoTracking()
                .Where(s => storeIds.Contains(s.Id))
                .Select(s => new { s.Id, s.PricingType, s.PricingValue })
                .ToDictionaryAsync(s => s.Id, s => new { s.PricingType, s.PricingValue });

            decimal CalcEarning(Appointment appt)
            {
                var servicesTotal = appt.ServiceOfferings.Sum(s => s.Price);
                if (appt.StoreId == null || !storePricingMap.ContainsKey(appt.StoreId.Value))
                    return servicesTotal; // Doğrudan müşteri → tam tutar
                var pricing = storePricingMap[appt.StoreId.Value];
                if (pricing.PricingType == PricingType.Percent)
                    return servicesTotal * (1 - (decimal)(pricing.PricingValue / 100.0));
                // Kira: toplam - ödenen kira
                if (appt.StartTime.HasValue && appt.EndTime.HasValue)
                {
                    var hours = (decimal)(appt.EndTime.Value - appt.StartTime.Value).TotalHours;
                    var rent = hours * (decimal)pricing.PricingValue;
                    return Math.Max(0, servicesTotal - rent);
                }
                return servicesTotal;
            }

            decimal total = 0;
            decimal daily = 0;
            var byDay = new Dictionary<string, decimal>();

            foreach (var appt in appointments)
            {
                var earning = CalcEarning(appt);
                total += earning;
                var dateKey = appt.CompletedAt!.Value.Date.ToString("yyyy-MM-dd");
                if (!byDay.ContainsKey(dateKey)) byDay[dateKey] = 0;
                byDay[dateKey] += earning;
                if (appt.CompletedAt.Value.Date == todayUtc)
                    daily += earning;
            }

            decimal previousTotal = previousAppointments.Sum(CalcEarning);
            double changePct = previousTotal == 0
                ? (total > 0 ? 100.0 : 0.0)
                : (double)((total - previousTotal) / previousTotal * 100);

            var breakdown = byDay
                .OrderBy(x => x.Key)
                .Select(x => new DailyEarningDto { Date = x.Key, Amount = x.Value })
                .ToList();

            return new EarningsDto
            {
                TotalEarnings = total,
                DailyEarnings = daily,
                PreviousPeriodEarnings = previousTotal,
                ChangePercent = Math.Round(changePct, 1),
                DailyBreakdown = breakdown
            };
        }
    }
}

