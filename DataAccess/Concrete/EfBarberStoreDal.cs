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
    public class EfBarberStoreDal : EfEntityRepositoryBase<BarberStore, DatabaseContext>, IBarberStoreDal
    {
        private readonly DatabaseContext _context;
        public EfBarberStoreDal(DatabaseContext context) : base(context)
        {
            _context = context;
        }

        public async Task<BarberStoreMineDto> GetBarberStoreForUsers(Guid storeId)
        {
            var nowLocal = TimeZoneHelper.ToTurkeyTime(DateTime.UtcNow);

            // 1) Store
            var store = await _context.BarberStores
                .AsNoTracking()
                .Where(s => s.Id == storeId)
                .Select(s => new
                {
                    s.Id,
                    s.StoreName,
                    s.Type,
                    s.AddressDescription,
                    s.PricingValue,
                    s.PricingType,
                    s.BarberStoreOwnerId, // Owner User ID'yi de al
                })
                .FirstOrDefaultAsync();

            if (store == null)
                return new BarberStoreMineDto();

            // 2) Rating + review count - TargetId = Store ID (her dükkanın kendi rating'i)
            var ratingInfo = await _context.Ratings
                .AsNoTracking()
                .Where(r => r.TargetId == store.Id)
                .GroupBy(r => r.TargetId)
                .Select(g => new
                {
                    AvgRating = g.Average(x => (double)x.Score),
                    ReviewCount = g.Count()
                })
                .FirstOrDefaultAsync();

            // 3) Favorite count (sadece aktif favoriler) - FavoritedToId = Store ID (her dükkanın kendi favori sayısı)
            var favoriteCount = await _context.Favorites
                .AsNoTracking()
                .CountAsync(f => f.FavoritedToId == store.Id && f.IsActive);

            // 4) Offerings
            var offerings = await _context.ServiceOfferings
                .AsNoTracking()
                .Where(o => o.OwnerId == store.Id)
                .Select(o => new ServiceOfferingGetDto
                {
                    Id = o.Id,
                    ServiceName = o.ServiceName,
                    Price = o.Price
                })
                .ToListAsync();

            // 5) Working hours
            var hours = await _context.WorkingHours
                .AsNoTracking()
                .Where(w => w.OwnerId == store.Id)
                .ToListAsync();

            // 6) Images
            var images = await _context.Images
                .AsNoTracking()
                .Where(i => i.OwnerType == ImageOwnerType.Store && i.ImageOwnerId == store.Id)
                .Select(i => new ImageGetDto
                {
                    Id = i.Id,
                    ImageUrl = i.ImageUrl
                })
                .ToListAsync();

            var isOpenNow = OpenControl.IsOpenNow(hours, nowLocal);

            return new BarberStoreMineDto
            {
                Id = store.Id,
                StoreName = store.StoreName,
                ImageList = images,
                Type = store.Type,
                Rating = Math.Round(ratingInfo?.AvgRating ?? 0, 2),
                ReviewCount = ratingInfo?.ReviewCount ?? 0,
                FavoriteCount = favoriteCount,
                IsOpenNow = isOpenNow,
                ServiceOfferings = offerings,
                AddressDescription = store.AddressDescription,
                PricingType = store.PricingType.ToString(),
                PricingValue = store.PricingValue,
            };
        }


        public async Task<BarberStoreDetail> GetByIdStore(Guid storeId)
        {
            var nowLocal = TimeZoneHelper.ToTurkeyTime(DateTime.UtcNow);
            var today = nowLocal.DayOfWeek;   // 0–6
            var nowTime = nowLocal.TimeOfDay;      // TimeSpan

            // 2) Store'u çek
            var store = await _context.BarberStores
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == storeId);

            if (store is null)
                return new BarberStoreDetail();

            // 3) Store'a ait çalışma saatlerini çek
            var hours = await _context.WorkingHours
                .AsNoTracking()
                .Where(w => w.OwnerId == storeId /* && w.TargetType == ... (varsa) */)
                .OrderBy(w => w.DayOfWeek)
                .ThenBy(w => w.StartTime)
                .ToListAsync();

            // 4) Şu an açık mı?
            var isOpenNow = hours.Any(h =>
                !h.IsClosed &&
                h.DayOfWeek == today &&
                h.StartTime <= nowTime &&
                nowTime < h.EndTime
            );

            // 5) WorkingHours'u DTO'ya map et
            var workingHourDtos = hours.Select(h => new WorkingHourDto
            {
                Id = h.Id,
                OwnerId = h.OwnerId,
                DayOfWeek = h.DayOfWeek,
                IsClosed = h.IsClosed,
                StartTime = h.StartTime,
                EndTime = h.EndTime
            }).ToList();

            var images = await _context.Images.AsNoTracking().Where(i => i.ImageOwnerId == storeId && i.OwnerType == ImageOwnerType.Store).ToListAsync();

            var imageDtos = images.Select(i => new ImageGetDto
            {
                Id = i.Id,
                ImageUrl = i.ImageUrl,
            }).ToList();

            var serviceOfferings = await _context.ServiceOfferings.AsNoTracking().Where(i => i.OwnerId == storeId).ToListAsync();
            var serviceOfferingsDto = serviceOfferings.Select(s => new ServiceOfferingGetDto
            {
                Id = s.Id,
                Price = s.Price,
                ServiceName = s.ServiceName
            }).ToList();

            // ✅ N+1 FIX: Manuel barberleri önce çek
            var manuelBarbers = await _context.ManuelBarbers
          .AsNoTracking()
          .Where(b => b.StoreId == storeId)
          .ToListAsync();

            var barberIds = manuelBarbers.Select(b => b.Id).ToList();

            // ✅ N+1 FIX: Tüm rating'leri tek sorguda çek ve grupla
            var barberRatings = await _context.Ratings
                .AsNoTracking()
                .Where(r => barberIds.Contains(r.TargetId))
                .GroupBy(r => r.TargetId)
                .Select(g => new
                {
                    BarberId = g.Key,
                    AvgRating = g.Average(x => (double)x.Score)
                })
                .ToDictionaryAsync(x => x.BarberId, x => x.AvgRating);

            // ✅ N+1 FIX: Tüm image'ları tek sorguda çek
            var barberImages = await _context.Images
                .AsNoTracking()
                .Where(i => barberIds.Contains(i.ImageOwnerId))
                .GroupBy(i => i.ImageOwnerId)
                .Select(g => new
                {
                    OwnerId = g.Key,
                    ImageUrl = g.First().ImageUrl
                })
                .ToDictionaryAsync(x => x.OwnerId, x => x.ImageUrl);

            // ✅ Memory'de birleştir (N+1 yok, sadece dictionary lookup!)
            var manuelBarberDtos = manuelBarbers
                .Select(b => new ManuelBarberDto
                {
                    Id = b.Id,
                    FullName = b.FullName,
                    Rating = barberRatings.ContainsKey(b.Id) ? barberRatings[b.Id] : 0,
                    ProfileImageUrl = barberImages.ContainsKey(b.Id) ? barberImages[b.Id] : null
                })
                .ToList();


            var chairs = await _context.BarberChairs
             .AsNoTracking()
             .Where(ch => ch.StoreId == storeId)
             .ToListAsync();

            var barberStoreChairsDto = chairs
          .Select(ch => new BarberChairDto
          {
              Id = ch.Id,
              ManuelBarberId = ch.ManuelBarberId, // null olabilir
              Name = ch.Name,
          })
          .ToList();

            // Tax document image'ı çek
            ImageGetDto taxDocumentImageDto = null;
            if (store.TaxDocumentImageId.HasValue)
            {
                var taxImage = await _context.Images
                    .AsNoTracking()
                    .FirstOrDefaultAsync(i => i.Id == store.TaxDocumentImageId.Value);

                if (taxImage != null)
                {
                    taxDocumentImageDto = new ImageGetDto
                    {
                        Id = taxImage.Id,
                        ImageUrl = taxImage.ImageUrl,
                    };
                }
            }

            // 6) BarberStoreDetail DTO'sunu doldur
            var dto = new BarberStoreDetail
            {
                Id = store.Id,
                StoreName = store.StoreName,
                Latitude = store.Latitude,
                Longitude = store.Longitude,
                Type = store.Type.ToString(),
                PricingType = store.PricingType.ToString(),
                PricingValue = store.PricingValue,
                IsOpenNow = isOpenNow,
                WorkingHours = workingHourDtos,
                AddressDescription = store.AddressDescription,
                ImageList = imageDtos,
                ServiceOfferings = serviceOfferingsDto,
                ManuelBarbers = manuelBarberDtos,
                BarberStoreChairs = barberStoreChairsDto,
                TaxDocumentImageId = store.TaxDocumentImageId,
                TaxDocumentImage = taxDocumentImageDto,

            };
            return dto;
        }

        public async Task<List<BarberStoreMineDto>> GetMineStores(Guid currentUserId)
        {
            var nowLocal = TimeZoneHelper.ToTurkeyTime(DateTime.UtcNow);

            // 1) Bu kullanıcıya ait dükkanları çek
            var stores = await _context.BarberStores
                .AsNoTracking()
                .Where(s => s.BarberStoreOwnerId == currentUserId)
                .Select(s => new
                {
                    s.Id,
                    s.StoreName,
                    s.StoreNo,
                    s.Type,
                    s.Latitude,
                    s.Longitude,
                    s.AddressDescription,
                    s.BarberStoreOwnerId, // Owner User ID'yi de al
                })
                .ToListAsync();

            if (!stores.Any())
                return new List<BarberStoreMineDto>();

            var storeIds = stores.Select(s => s.Id).ToList();

            // 2) Rating & ReviewCount - TargetId = Store ID (her dükkanın kendi rating'i)
            var ratingStats = await _context.Ratings
                .AsNoTracking()
                .Where(r => storeIds.Contains(r.TargetId))
                .GroupBy(r => r.TargetId)
                .Select(g => new
                {
                    StoreId = g.Key,
                    AvgRating = g.Average(x => (double)x.Score),
                    ReviewCount = g.Count()
                })
                .ToListAsync();

            var ratingDict = ratingStats.ToDictionary(x => x.StoreId, x => new
            {
                x.AvgRating,
                x.ReviewCount
            });

            // 3) Favoriler - FavoritedToId = Store ID (her dükkanın kendi favori sayısı)
            var favoriteStats = await _context.Favorites
                .AsNoTracking()
                .Where(f => storeIds.Contains(f.FavoritedToId) && f.IsActive)
                .GroupBy(f => f.FavoritedToId)
                .Select(g => new
                {
                    StoreId = g.Key,
                    FavoriteCount = g.Count()
                })
                .ToListAsync();

            var favoriteDict = favoriteStats.ToDictionary(x => x.StoreId, x => x.FavoriteCount);

            // 4) Hizmetler
            var offeringGroups = await _context.ServiceOfferings
                .AsNoTracking()
                .Where(o => storeIds.Contains(o.OwnerId))
                .GroupBy(o => o.OwnerId)
                .Select(g => new
                {
                    OwnerId = g.Key,
                    Offerings = g.Select(o => new ServiceOfferingGetDto
                    {
                        Id = o.Id,
                        ServiceName = o.ServiceName,
                        Price = o.Price
                    }).ToList()
                })
                .ToListAsync();

            var offeringDict = offeringGroups.ToDictionary(x => x.OwnerId, x => x.Offerings);

            // 5) Çalışma saatleri
            var hourGroups = await _context.WorkingHours
                .AsNoTracking()
                .Where(w => storeIds.Contains(w.OwnerId))
                .GroupBy(w => w.OwnerId)
                .Select(g => new
                {
                    OwnerId = g.Key,
                    Hours = g.ToList()    // WorkingHour entity listesi
                })
                .ToListAsync();

            var hoursDict = hourGroups.ToDictionary(x => x.OwnerId, x => x.Hours);

            // 6) Görseller
            var imageGroups = await _context.Images
                .AsNoTracking()
                .Where(i => i.OwnerType == ImageOwnerType.Store
                         && storeIds.Contains(i.ImageOwnerId))
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

            var imageDict = imageGroups.ToDictionary(x => x.OwnerId, x => x.Images);

            // 7) Hepsini BarberStoreMineDto'ya projekte et
            var result = stores
                .Select(s =>
                {
                    ratingDict.TryGetValue(s.Id, out var ratingInfo); // Her store'un kendi rating'i
                    favoriteDict.TryGetValue(s.Id, out var favCount); // Her store'un kendi favori sayısı
                    offeringDict.TryGetValue(s.Id, out var offerings);
                    hoursDict.TryGetValue(s.Id, out var hours);
                    imageDict.TryGetValue(s.Id, out var images); // Her store'un kendi fotoğrafları

                    var avgRating = ratingInfo?.AvgRating ?? 0;
                    var reviewCount = ratingInfo?.ReviewCount ?? 0;
                    var favoriteCount = favCount;

                    var isOpenNow = hours != null
                        ? OpenControl.IsOpenNow(hours, nowLocal)
                        : false;

                    return new BarberStoreMineDto
                    {
                        Id = s.Id,
                        StoreName = s.StoreName, // Her store'un kendi ismi
                        StoreNo = s.StoreNo,
                        ImageList = images ?? new List<ImageGetDto>(), // Her store'un kendi fotoğrafları
                        Type = s.Type,
                        Rating = Math.Round(avgRating, 2), // Her store'un kendi rating'i
                        FavoriteCount = favoriteCount, // Her store'un kendi favori sayısı
                        ReviewCount = reviewCount, // Her store'un kendi review sayısı
                        IsOpenNow = isOpenNow,
                        ServiceOfferings = offerings ?? new List<ServiceOfferingGetDto>(),
                        Latitude = s.Latitude,
                        Longitude = s.Longitude,
                        AddressDescription = s.AddressDescription,
                    };
                })
                .ToList();

            return result;
        }

        public async Task<List<BarberStoreGetDto>> GetNearbyStoresAsync(
            double lat,
            double lon,
            double radiusKm = 10,
            Guid? currentUserId = null,
            int limit = 100,
            IReadOnlyCollection<Guid>? blockedUserIds = null)
        {
            // Hard cap: yoğun bölgelerde geo-box 500+ dükkan döndürebiliyor. Server-side clamp,
            // istemci tarafı ne gönderirse göndersin memory/wire patlamasını önler.
            limit = Math.Clamp(limit, 1, 200);
            var nowLocal = TimeZoneHelper.ToTurkeyTime(DateTime.UtcNow);
            var (minLat, maxLat, minLon, maxLon) = GeoBounds.BoxKm(lat, lon, radiusKm);

            IQueryable<BarberStore> baseQuery = _context.BarberStores
                .AsNoTracking()
                .Where(s => s.Latitude >= minLat && s.Latitude <= maxLat
                         && s.Longitude >= minLon && s.Longitude <= maxLon);

            // Blocked user filter DB'de — eski in-memory FilterBlockedStoresAsync yerine.
            if (blockedUserIds != null && blockedUserIds.Count > 0)
            {
                var blocked = blockedUserIds.ToList();
                baseQuery = baseQuery.Where(s => !blocked.Contains(s.BarberStoreOwnerId));
            }

            var stores = await baseQuery
                .Select(s => new
                {
                    s.Id,
                    s.StoreName,
                    s.Latitude,
                    s.Longitude,
                    s.PricingType,
                    s.PricingValue,
                    s.Type,
                    s.AddressDescription,
                    s.BarberStoreOwnerId // Owner User ID'yi de al
                })
                .ToListAsync();

            if (!stores.Any())
                return new List<BarberStoreGetDto>();
            var storeIds = stores.Select(s => s.Id).ToList();
            // Rating - TargetId = Store ID (her dükkanın kendi rating'i)
            var ratingStats = await _context.Ratings
                .AsNoTracking()
                .Where(r => storeIds.Contains(r.TargetId))
                .GroupBy(r => r.TargetId)
                .Select(g => new
                {
                    StoreId = g.Key,
                    AvgRating = g.Average(x => (double)x.Score),
                    ReviewCount = g.Count()
                })
                .ToListAsync();

            var ratingDict = ratingStats
                .ToDictionary(x => x.StoreId, x => new { x.AvgRating, x.ReviewCount });
            
            // Favorite - FavoritedToId = Store ID (her dükkanın kendi favori sayısı)
            var favoriteStats = await _context.Favorites
                .AsNoTracking()
                .Where(f => storeIds.Contains(f.FavoritedToId) && f.IsActive)
                .GroupBy(f => f.FavoritedToId)
                .Select(g => new
                {
                    StoreId = g.Key,
                    FavoriteCount = g.Count(f => f.IsActive)
                })
                .ToListAsync();

            var favoriteDict = favoriteStats
                .ToDictionary(x => x.StoreId, x => x.FavoriteCount);

            // User IsFavorited bilgisi
            var isFavoritedDict = new Dictionary<Guid, bool>();
            if (currentUserId.HasValue)
            {
                var userFavs = await _context.Favorites
                    .AsNoTracking()
                    .Where(f => f.FavoritedFromId == currentUserId.Value && storeIds.Contains(f.FavoritedToId) && f.IsActive)
                    .Select(f => f.FavoritedToId)
                    .ToListAsync();
                
                isFavoritedDict = userFavs.ToDictionary(x => x, x => true);
            }

            var offeringGroups = await _context.ServiceOfferings
                .AsNoTracking()
                .Where(o => storeIds.Contains(o.OwnerId))
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
            var hourGroups = await _context.WorkingHours
                .AsNoTracking()
                .Where(w => storeIds.Contains(w.OwnerId))
                .GroupBy(w => w.OwnerId)
                .Select(g => new
                {
                    OwnerId = g.Key,
                    Hours = g.ToList()
                })
                .ToListAsync();
            var hoursDict = hourGroups
                .ToDictionary(x => x.OwnerId, x => x.Hours);
            var imageGroups = await _context.Images
                .AsNoTracking()
                .Where(i => i.OwnerType == ImageOwnerType.Store
                         && storeIds.Contains(i.ImageOwnerId))
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
            var result = stores
                .Select(s =>
                {
                    var distance = Geo.DistanceKm(lat, lon, s.Latitude, s.Longitude);
                    if (distance > radiusKm) return null;

                    ratingDict.TryGetValue(s.Id, out var ratingInfo); // Her store'un kendi rating'i
                    favoriteDict.TryGetValue(s.Id, out var favCount); // Her store'un kendi favori sayısı
                    offeringDict.TryGetValue(s.Id, out var offerings);
                    hoursDict.TryGetValue(s.Id, out var hours);
                    imageDict.TryGetValue(s.Id, out var images);

                    var avgRating = ratingInfo?.AvgRating ?? 0;
                    var reviewCount = ratingInfo?.ReviewCount ?? 0;
                    var favoriteCount = favCount;
                    var isFavorited = isFavoritedDict.GetValueOrDefault(s.Id, false);

                    var isOpenNow = hours != null
                        ? OpenControl.IsOpenNow(hours, nowLocal)
                        : false;

                    return new BarberStoreGetDto
                    {
                        Id = s.Id,
                        BarberStoreOwnerId = s.BarberStoreOwnerId, // Kendi dükkanına tıklandığında güncelleme sheet'i açmak için gerekli
                        StoreName = s.StoreName,
                        ImageList = images ?? new List<ImageGetDto>(),
                        PricingType = s.PricingType.ToString(),
                        PricingValue = s.PricingValue,
                        Type = s.Type,
                        IsOpenNow = isOpenNow,
                        Latitude = s.Latitude,
                        Longitude = s.Longitude,
                        AddressDescription = s.AddressDescription,
                        FavoriteCount = favoriteCount,
                        ReviewCount = reviewCount,
                        Rating = Math.Round(avgRating, 2),
                        ServiceOfferings = offerings ?? new List<ServiceOfferingGetDto>(),
                        DistanceKm = Math.Round(distance, 3),
                        IsFavorited = isFavorited
                    };
                })
                .Where(dto => dto != null)
                .OrderBy(dto => dto!.DistanceKm)
                .ThenByDescending(dto => dto!.Rating)
                .Take(limit)
                .ToList()!;

            return result;
        }

        public async Task<List<BarberStoreGetDto>> GetFilteredStoresAsync(
            FilterRequestDto filter,
            int limit = 100,
            int offset = 0,
            IReadOnlyCollection<Guid>? blockedUserIds = null)
        {
            // ---------------------------------------------------------------------------
            // GERÇEK DB PAGINATION
            //
            // Eski akış: tüm filtrelenmiş satırları çekip in-memory Skip/Take uyguluyordu;
            // şehirde binlerce dükkan varsa bu hem bellek patlaması hem de gereksiz I/O idi.
            //
            // Yeni akış:
            //   1) Tüm WHERE filter'ları (geo, kategori, fiyat, hizmet, favori, min-rating,
            //      block) IQueryable üzerinde EF subquery'leri ile uygulanır.
            //   2) Sıralama IQueryable üzerinde; PriceSort → PricingValue, default → Rating
            //      subquery (Average). Id tie-breaker.
            //   3) Skip/Take DB'de.
            //   4) Enrichment (ratings/favorites/offerings/images/hours) yalnızca sayfanın
            //      store Id'leri için yüklenir → join başına ~limit+küçük sabit satır.
            //
            // Post-filter notu:
            //   IsOpenNow tutulan WorkingHours + zaman hesabı EF'te SQL'e çevrilemez.
            //   IsOpenNow filter aktifse overfetch faktörü ile fazladan satır çekilip
            //   in-memory eleme sonrası `Take(limit)` uygulanır (sayfa büyük oranla dolu
            //   gelir, garantisi yoktur).
            // ---------------------------------------------------------------------------

            limit = Math.Clamp(limit, 1, 200);
            if (offset < 0) offset = 0;
            var nowLocal = TimeZoneHelper.ToTurkeyTime(DateTime.UtcNow);

            // 0. Kullanıcının kendi dükkanlarının ID'lerini al (IsOwnStore + block filter istisnası)
            var ownStoreIds = new List<Guid>();
            var ownOwnerUserId = filter.CurrentUserId;
            if (ownOwnerUserId.HasValue)
            {
                ownStoreIds = await _context.BarberStores
                    .AsNoTracking()
                    .Where(s => s.BarberStoreOwnerId == ownOwnerUserId.Value)
                    .Select(s => s.Id)
                    .ToListAsync();
            }

            IQueryable<BarberStore> query = _context.BarberStores.AsNoTracking();

            // 1. Konum filtresi (geo-box). Kendi dükkanları konum filtresinden muaf.
            // Sınırsız yarıçap (sentinel): kutu uygulanmaz — tüm liste diğer kriterlerle gelir.
            if (filter.ShouldApplyDiscoveryGeoBox())
            {
                var distance = filter.GetEffectiveDistanceKm();
                var (minLat, maxLat, minLon, maxLon) = GeoBounds.BoxKm(
                    filter.Latitude.Value, filter.Longitude.Value, distance);
                query = query.Where(s =>
                    ownStoreIds.Contains(s.Id) ||
                    (s.Latitude >= minLat && s.Latitude <= maxLat &&
                     s.Longitude >= minLon && s.Longitude <= maxLon));
            }

            // 2. İsim araması
            if (!string.IsNullOrWhiteSpace(filter.SearchQuery))
            {
                var searchLower = filter.SearchQuery.ToLower();
                query = query.Where(s =>
                    ownStoreIds.Contains(s.Id) ||
                    s.StoreName.ToLower().Contains(searchLower));
            }

            // 3. Ana kategori filtresi
            if (filter.MainCategory.HasValue)
            {
                var mc = filter.MainCategory.Value;
                query = query.Where(s =>
                    ownStoreIds.Contains(s.Id) ||
                    s.Type == mc);
            }

            // 4. Pricing Type filtresi
            if (!string.IsNullOrWhiteSpace(filter.PricingType) && filter.PricingType != "all")
            {
                if (filter.PricingType == "rent")
                    query = query.Where(s =>
                        ownStoreIds.Contains(s.Id) ||
                        s.PricingType == PricingType.Rent);
                else if (filter.PricingType == "percent")
                    query = query.Where(s =>
                        ownStoreIds.Contains(s.Id) ||
                        s.PricingType == PricingType.Percent);
            }

            // 5. Fiyat aralığı (PricingValue alanı)
            if (filter.MinPrice.HasValue)
            {
                var minP = (double)filter.MinPrice.Value;
                query = query.Where(s =>
                    ownStoreIds.Contains(s.Id) ||
                    s.PricingValue >= minP);
            }
            if (filter.MaxPrice.HasValue)
            {
                var maxP = (double)filter.MaxPrice.Value;
                query = query.Where(s =>
                    ownStoreIds.Contains(s.Id) ||
                    s.PricingValue <= maxP);
            }

            // 6. Hizmet filtresi (tekil offerings + paket itemları)
            if (filter.ServiceIds != null && filter.ServiceIds.Any())
            {
                var serviceIds = filter.ServiceIds.ToList();
                var categoryNames = await ServiceFilterCategoryHelper.GetCategoryNamesByServiceIdsAsync(
                    _context, serviceIds);

                query = query.Where(s =>
                    ownStoreIds.Contains(s.Id) ||
                    _context.ServiceOfferings.Any(o =>
                        o.OwnerId == s.Id &&
                        (serviceIds.Contains(o.Id) || categoryNames.Contains(o.ServiceName))) ||
                    _context.ServicePackages.Any(p =>
                        p.OwnerId == s.Id &&
                        p.Items.Any(i => categoryNames.Contains(i.ServiceName))));
            }

            // 7. FavoritesOnly (kullanıcının favorileri)
            if (filter.FavoritesOnly == true && filter.CurrentUserId.HasValue)
            {
                var favUserId = filter.CurrentUserId.Value;
                query = query.Where(s =>
                    ownStoreIds.Contains(s.Id) ||
                    _context.Favorites.Any(f =>
                        f.FavoritedFromId == favUserId &&
                        f.FavoritedToId == s.Id &&
                        f.IsActive));
            }

            // 8. MinRating — subquery Average. Henüz oy yoksa 0 döner.
            if (filter.MinRating.HasValue && filter.MinRating.Value > 0)
            {
                double minRating = filter.MinRating.Value;
                query = query.Where(s =>
                    ownStoreIds.Contains(s.Id) ||
                    (_context.Ratings
                        .Where(r => r.TargetId == s.Id)
                        .Select(r => (double?)r.Score)
                        .Average() ?? 0.0) >= minRating);
            }

            // 9. Blocked filter — kendi dükkanları blok listesi dışı kalır.
            if (blockedUserIds != null && blockedUserIds.Count > 0)
            {
                var blocked = blockedUserIds.ToList();
                query = query.Where(s =>
                    ownStoreIds.Contains(s.Id) ||
                    !blocked.Contains(s.BarberStoreOwnerId));
            }

            // 10. Sıralama (IQueryable → SQL ORDER BY)
            IOrderedQueryable<BarberStore> ordered;
            if (!string.IsNullOrWhiteSpace(filter.PriceSort) && filter.PriceSort != "none")
            {
                ordered = filter.PriceSort == "asc"
                    ? query.OrderBy(s => s.PricingValue)
                    : query.OrderByDescending(s => s.PricingValue);
            }
            else
            {
                ordered = query.OrderByDescending(s =>
                    _context.Ratings
                        .Where(r => r.TargetId == s.Id)
                        .Select(r => (double?)r.Score)
                        .Average() ?? 0.0);
            }
            ordered = ordered.ThenByDescending(s => s.Id); // tie-breaker

            // 11. Overfetch: Availability (açık/kapalı) filtresi varsa ihtiyatla 3x satır al; sonrasında
            //     in-memory IsOpenNow eleme + Take(limit). Çoğu durumda sayfa dolu gelir.
            bool hasIsOpenFilter = filter.Availability.HasValue && filter.Availability.Value != AvailabilityFilter.Any;
            int takeCount = hasIsOpenFilter ? limit * 3 : limit;

            var pagedStores = await ordered
                .Skip(offset)
                .Take(takeCount)
                .Select(s => new
                {
                    s.Id,
                    s.StoreName,
                    s.StoreNo,
                    s.Latitude,
                    s.Longitude,
                    s.PricingType,
                    s.PricingValue,
                    s.Type,
                    s.AddressDescription,
                    s.BarberStoreOwnerId
                })
                .ToListAsync();

            if (pagedStores.Count == 0)
                return new List<BarberStoreGetDto>();

            var storeIds = pagedStores.Select(s => s.Id).ToList();

            // 12. Enrichment — yalnızca sayfadaki store'lar için. Her join O(sayfa_boyu).
            var ratingStats = await _context.Ratings
                .AsNoTracking()
                .Where(r => storeIds.Contains(r.TargetId))
                .GroupBy(r => r.TargetId)
                .Select(g => new
                {
                    StoreId = g.Key,
                    AvgRating = g.Average(x => (double)x.Score),
                    ReviewCount = g.Count()
                })
                .ToListAsync();
            var ratingDict = ratingStats.ToDictionary(x => x.StoreId, x => new { x.AvgRating, x.ReviewCount });

            var favoriteStats = await _context.Favorites
                .AsNoTracking()
                .Where(f => storeIds.Contains(f.FavoritedToId) && f.IsActive)
                .GroupBy(f => f.FavoritedToId)
                .Select(g => new { StoreId = g.Key, FavoriteCount = g.Count() })
                .ToListAsync();
            var favoriteDict = favoriteStats.ToDictionary(x => x.StoreId, x => x.FavoriteCount);

            var isFavoritedDict = new Dictionary<Guid, bool>();
            if (filter.CurrentUserId.HasValue)
            {
                var fromId = filter.CurrentUserId.Value;
                var userFavs = await _context.Favorites
                    .AsNoTracking()
                    .Where(f => f.FavoritedFromId == fromId && storeIds.Contains(f.FavoritedToId))
                    .Select(f => new { f.FavoritedToId, f.IsActive })
                    .ToListAsync();
                isFavoritedDict = userFavs.ToDictionary(x => x.FavoritedToId, x => x.IsActive);
            }

            var offerings = await _context.ServiceOfferings
                .AsNoTracking()
                .Where(o => storeIds.Contains(o.OwnerId))
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

            var hours = await _context.WorkingHours
                .AsNoTracking()
                .Where(w => storeIds.Contains(w.OwnerId))
                .ToListAsync();
            var hoursDict = hours.GroupBy(h => h.OwnerId).ToDictionary(g => g.Key, g => g.ToList());

            var images = await _context.Images
                .AsNoTracking()
                .Where(i => i.OwnerType == ImageOwnerType.Store && storeIds.Contains(i.ImageOwnerId))
                .Select(i => new
                {
                    i.ImageOwnerId,
                    Image = new ImageGetDto { Id = i.Id, ImageUrl = i.ImageUrl }
                })
                .ToListAsync();
            var imagesDict = images
                .GroupBy(i => i.ImageOwnerId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Image).ToList());

            // 13. DTO assembly — sıra IQueryable'dan gelen sırada korunur.
            var result = pagedStores.Select(s =>
            {
                var storeHours = hoursDict.GetValueOrDefault(s.Id, new List<WorkingHour>());
                var rating = ratingDict.GetValueOrDefault(s.Id);
                bool isOwnStore = ownStoreIds.Contains(s.Id);

                return new BarberStoreGetDto
                {
                    Id = s.Id,
                    BarberStoreOwnerId = s.BarberStoreOwnerId,
                    StoreName = s.StoreName,
                    Latitude = s.Latitude,
                    Longitude = s.Longitude,
                    PricingType = s.PricingType.ToString(),
                    PricingValue = s.PricingValue,
                    Type = s.Type,
                    AddressDescription = s.AddressDescription,
                    Rating = rating != null ? Math.Round(rating.AvgRating, 2) : 0,
                    ReviewCount = rating?.ReviewCount ?? 0,
                    FavoriteCount = favoriteDict.GetValueOrDefault(s.Id, 0),
                    IsFavorited = isFavoritedDict.GetValueOrDefault(s.Id, false),
                    IsOpenNow = OpenControl.IsOpenNow(storeHours, nowLocal),
                    Offerings = offeringsDict.GetValueOrDefault(s.Id, new List<ServiceOfferingGetDto>()),
                    ServiceOfferings = offeringsDict.GetValueOrDefault(s.Id, new List<ServiceOfferingGetDto>()),
                    ImageList = imagesDict.GetValueOrDefault(s.Id, new List<ImageGetDto>()),
                    IsOwnStore = isOwnStore,
                    StoreNo = s.StoreNo
                };
            }).ToList();

            // 14. Post-filter: IsOpenNow (SQL'e indirilemedi). Son limit tutucu.
            if (hasIsOpenFilter)
            {
                bool wantOpen = filter.Availability == AvailabilityFilter.Ready;
                result = result.Where(s => s.IsOwnStore || s.IsOpenNow == wantOpen).ToList();
                if (result.Count > limit)
                    result = result.Take(limit).ToList();
            }

            return result;
        }

        public async Task<List<BarberStoreGetDto>> GetAllForAdminAsync()
        {
            var nowLocal = TimeZoneHelper.ToTurkeyTime(DateTime.UtcNow);

            var stores = await _context.BarberStores
                .AsNoTracking()
                .Select(s => new
                {
                    s.Id,
                    s.StoreName,
                    s.StoreNo,
                    s.Latitude,
                    s.Longitude,
                    s.PricingType,
                    s.PricingValue,
                    s.Type,
                    s.AddressDescription,
                    s.BarberStoreOwnerId
                })
                .ToListAsync();

            if (!stores.Any())
                return new List<BarberStoreGetDto>();

            var storeIds = stores.Select(s => s.Id).ToList();

            // Rating stats (TargetId = StoreId)
            var ratingStats = await _context.Ratings
                .AsNoTracking()
                .Where(r => storeIds.Contains(r.TargetId))
                .GroupBy(r => r.TargetId)
                .Select(g => new
                {
                    StoreId = g.Key,
                    AvgRating = g.Average(x => (double)x.Score),
                    ReviewCount = g.Count()
                })
                .ToListAsync();

            var ratingDict = ratingStats.ToDictionary(x => x.StoreId, x => new { x.AvgRating, x.ReviewCount });

            // Offerings
            var offeringGroups = await _context.ServiceOfferings
                .AsNoTracking()
                .Where(o => storeIds.Contains(o.OwnerId))
                .GroupBy(o => o.OwnerId)
                .Select(g => new
                {
                    OwnerId = g.Key,
                    Offerings = g.Select(o => new ServiceOfferingGetDto
                    {
                        Id = o.Id,
                        ServiceName = o.ServiceName,
                        Price = o.Price
                    }).ToList()
                })
                .ToListAsync();

            var offeringDict = offeringGroups.ToDictionary(x => x.OwnerId, x => x.Offerings);

            // Working hours
            var hourGroups = await _context.WorkingHours
                .AsNoTracking()
                .Where(w => storeIds.Contains(w.OwnerId))
                .GroupBy(w => w.OwnerId)
                .Select(g => new
                {
                    OwnerId = g.Key,
                    Hours = g.ToList()
                })
                .ToListAsync();

            var hoursDict = hourGroups.ToDictionary(x => x.OwnerId, x => x.Hours);

            // Images (store images)
            var imageGroups = await _context.Images
                .AsNoTracking()
                .Where(i => i.OwnerType == ImageOwnerType.Store && storeIds.Contains(i.ImageOwnerId))
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

            var imageDict = imageGroups.ToDictionary(x => x.OwnerId, x => x.Images);

            var result = stores.Select(s =>
            {
                ratingDict.TryGetValue(s.Id, out var ratingInfo);
                offeringDict.TryGetValue(s.Id, out var offerings);
                hoursDict.TryGetValue(s.Id, out var hours);
                imageDict.TryGetValue(s.Id, out var images);

                var isOpenNow = hours != null
                    ? OpenControl.IsOpenNow(hours, nowLocal)
                    : false;

                return new BarberStoreGetDto
                {
                    Id = s.Id,
                    BarberStoreOwnerId = s.BarberStoreOwnerId,
                    StoreName = s.StoreName,
                    Latitude = s.Latitude,
                    Longitude = s.Longitude,
                    PricingType = s.PricingType.ToString(),
                    PricingValue = s.PricingValue,
                    Type = s.Type,
                    AddressDescription = s.AddressDescription,
                    IsOpenNow = isOpenNow,

                    // Admin liste için kullanıcıya göre favori/mesafe bilgisi yok
                    DistanceKm = 0,
                    FavoriteCount = 0,
                    IsFavorited = false,
                    IsOwnStore = false,
                    StoreNo = s.StoreNo,

                    Rating = ratingInfo != null ? Math.Round(ratingInfo.AvgRating, 2) : 0,
                    ReviewCount = ratingInfo?.ReviewCount ?? 0,

                    Offerings = offerings ?? new List<ServiceOfferingGetDto>(),
                    ServiceOfferings = offerings ?? new List<ServiceOfferingGetDto>(),
                    ImageList = images ?? new List<ImageGetDto>()
                };
            })
            .OrderByDescending(x => x.IsOpenNow)
            .ThenByDescending(x => x.Rating)
            .ToList();

            return result;
        }

        public async Task<EarningsDto> GetEarningsAsync(Guid storeId, DateTime startDate, DateTime endDate)
        {
            var todayUtc = DateTime.UtcNow.Date;
            var startDateUtc = DateTime.SpecifyKind(startDate.Date, DateTimeKind.Utc);
            var endDateInclusive = DateTime.SpecifyKind(endDate.Date.AddDays(1), DateTimeKind.Utc);
            var previousStartDays = (endDate.Date - startDate.Date).TotalDays + 1;
            var previousStart = DateTime.SpecifyKind(startDate.Date.AddDays(-previousStartDays), DateTimeKind.Utc);

            // Store pricing bilgisi
            var store = await _context.BarberStores
                .AsNoTracking()
                .Where(s => s.Id == storeId)
                .Select(s => new { s.PricingType, s.PricingValue })
                .FirstOrDefaultAsync();

            if (store == null) return new EarningsDto();

            // Tamamlanan randevular (hizmetlerle birlikte)
            var appointments = await _context.Appointments
                .AsNoTracking()
                .Include(a => a.ServiceOfferings)
                .Where(a => a.StoreId == storeId
                         && a.Status == AppointmentStatus.Completed
                         && a.CompletedAt.HasValue
                         && a.CompletedAt.Value >= startDateUtc
                         && a.CompletedAt.Value < endDateInclusive)
                .ToListAsync();

            // Önceki dönem randevuları
            var previousAppointments = await _context.Appointments
                .AsNoTracking()
                .Include(a => a.ServiceOfferings)
                .Where(a => a.StoreId == storeId
                         && a.Status == AppointmentStatus.Completed
                         && a.CompletedAt.HasValue
                         && a.CompletedAt.Value >= previousStart
                         && a.CompletedAt.Value < startDateUtc)
                .ToListAsync();

            decimal CalcEarning(Appointment appt)
            {
                var servicesTotal = appt.ServiceOfferings.Sum(s => s.Price);
                if (appt.FreeBarberUserId == null)
                    return servicesTotal; // Doğrudan müşteri → tam tutar
                // Serbest berber durumu
                if (store.PricingType == PricingType.Percent)
                    return servicesTotal * (decimal)(store.PricingValue / 100.0);
                // Kira
                if (appt.StartTime.HasValue && appt.EndTime.HasValue)
                {
                    var hours = (decimal)(appt.EndTime.Value - appt.StartTime.Value).TotalHours;
                    return hours * (decimal)store.PricingValue;
                }
                return 0;
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
