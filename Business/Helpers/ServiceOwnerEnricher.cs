using DataAccess.Abstract;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;

namespace Business.Helpers
{
    /// <summary>
    /// Bir ServiceOffering / ServicePackage'ın OwnerId'si BarberStore.Id veya FreeBarber.Id olabilir.
    /// Admin gridlerinde sahibi (tür + ad + 6 haneli no + görsel) göstermek için toplu (N+1 olmadan) çözümleme yapar.
    /// </summary>
    public class OwnerDisplayInfo
    {
        /// <summary>"Store" | "FreeBarber" | "Unknown"</summary>
        public string OwnerType { get; set; } = "Unknown";
        public string? OwnerName { get; set; }
        /// <summary>6 haneli numara: Store.StoreNo veya FreeBarber kullanıcısının CustomerNumber'ı.</summary>
        public string? OwnerNumber { get; set; }
        public string? OwnerImageUrl { get; set; }
    }

    public class ServiceOwnerEnricher
    {
        private readonly IBarberStoreDal _barberStoreDal;
        private readonly IFreeBarberDal _freeBarberDal;
        private readonly IUserDal _userDal;
        private readonly IImageDal _imageDal;

        public ServiceOwnerEnricher(
            IBarberStoreDal barberStoreDal,
            IFreeBarberDal freeBarberDal,
            IUserDal userDal,
            IImageDal imageDal)
        {
            _barberStoreDal = barberStoreDal;
            _freeBarberDal = freeBarberDal;
            _userDal = userDal;
            _imageDal = imageDal;
        }

        public async Task<Dictionary<Guid, OwnerDisplayInfo>> ResolveAsync(IReadOnlyCollection<Guid> ownerIds)
        {
            var result = new Dictionary<Guid, OwnerDisplayInfo>();
            if (ownerIds == null || ownerIds.Count == 0)
                return result;

            var distinctIds = ownerIds.Where(id => id != Guid.Empty).Distinct().ToList();
            if (distinctIds.Count == 0)
                return result;

            var stores = await _barberStoreDal.GetAll(s => distinctIds.Contains(s.Id));
            var freeBarbers = await _freeBarberDal.GetAll(f => distinctIds.Contains(f.Id));

            // Serbest berberlerin 6 haneli numarası kullanıcının CustomerNumber'ında tutulur.
            var fbUserIds = freeBarbers.Select(f => f.FreeBarberUserId).Distinct().ToList();
            var fbUsers = fbUserIds.Count > 0
                ? await _userDal.GetAll(u => fbUserIds.Contains(u.Id))
                : new List<User>();
            var fbNumberByUserId = fbUsers
                .GroupBy(u => u.Id)
                .ToDictionary(g => g.Key, g => g.First().CustomerNumber);

            // Her sahip için en güncel görseli toplu getir.
            var imageRequests = new List<(Guid OwnerId, ImageOwnerType OwnerType)>();
            foreach (var s in stores)
                imageRequests.Add((s.Id, ImageOwnerType.Store));
            foreach (var f in freeBarbers)
                imageRequests.Add((f.Id, ImageOwnerType.FreeBarber));

            var images = imageRequests.Count > 0
                ? await _imageDal.GetLatestImagesAsync(imageRequests)
                : new Dictionary<(Guid OwnerId, ImageOwnerType OwnerType), string?>();

            foreach (var s in stores)
            {
                images.TryGetValue((s.Id, ImageOwnerType.Store), out var img);
                result[s.Id] = new OwnerDisplayInfo
                {
                    OwnerType = "Store",
                    OwnerName = s.StoreName,
                    OwnerNumber = s.StoreNo,
                    OwnerImageUrl = img
                };
            }

            foreach (var f in freeBarbers)
            {
                images.TryGetValue((f.Id, ImageOwnerType.FreeBarber), out var img);
                fbNumberByUserId.TryGetValue(f.FreeBarberUserId, out var number);
                result[f.Id] = new OwnerDisplayInfo
                {
                    OwnerType = "FreeBarber",
                    OwnerName = $"{f.FirstName} {f.LastName}".Trim(),
                    OwnerNumber = number,
                    OwnerImageUrl = img
                };
            }

            return result;
        }
    }
}
