using Business.Abstract;
using Business.Resources;
using Business.ValidationRules.FluentValidation;
using Core.Aspect.Autofac.Validation;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;
using Microsoft.EntityFrameworkCore;

namespace Business.Concrete
{
    public class ServicePackageManager(
        IServicePackageDal servicePackageDal,
        IServiceOfferingDal serviceOfferingDal,
        IBarberStoreDal barberStoreDal,
        IFreeBarberDal freeBarberDal,
        IAppointmentServicePackageDal appointmentServicePackageDal,
        Business.Helpers.ServiceOwnerEnricher ownerEnricher) : IServicePackageService
    {
        private const int MaxPackageCount = 20;

        [ValidationAspect(typeof(ServicePackageCreateValidator))]
        public async Task<IResult> AddAsync(ServicePackageCreateDto dto, Guid currentUserId)
        {
            var ownerCheck = await VerifyOwnershipAsync(dto.OwnerId, currentUserId);
            if (!ownerCheck.Success)
                return ownerCheck;

            // Limit kontrolü
            var existingPackages = await servicePackageDal.GetAll(p => p.OwnerId == dto.OwnerId);
            if (existingPackages.Count >= MaxPackageCount)
                return new ErrorResult(Messages.ServicePackageLimitReached);

            // Hizmetlerin varlığını ve sahipliğini kontrol et
            var serviceCheck = await ValidateServiceOfferingsAsync(dto.OwnerId, dto.ServiceOfferingIds);
            if (!serviceCheck.Success)
                return serviceCheck;

            // Duplicate paket kontrolü (aynı hizmet seti)
            var duplicateCheck = await CheckDuplicatePackageAsync(dto.OwnerId, dto.ServiceOfferingIds, excludePackageId: null);
            if (!duplicateCheck.Success)
                return duplicateCheck;

            // Paket oluştur
            var services = await serviceOfferingDal.GetServiceOfferingsByIdsAsync(dto.ServiceOfferingIds);

            var package = new ServicePackage
            {
                Id = Guid.NewGuid(),
                OwnerId = dto.OwnerId,
                PackageName = dto.PackageName,
                TotalPrice = dto.TotalPrice,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Items = services.Select(s => new ServicePackageItem
                {
                    Id = Guid.NewGuid(),
                    ServiceOfferingId = s.Id,
                    ServiceName = s.ServiceName
                }).ToList()
            };

            await servicePackageDal.Add(package);
            return new SuccessResult(Messages.ServicePackageAddedSuccess);
        }

        [ValidationAspect(typeof(ServicePackageUpdateValidator))]
        public async Task<IResult> UpdateAsync(ServicePackageUpdateDto dto, Guid currentUserId)
        {
            var ownerCheck = await VerifyOwnershipAsync(dto.OwnerId, currentUserId);
            if (!ownerCheck.Success)
                return ownerCheck;

            var package = await servicePackageDal.Get(p => p.Id == dto.Id);
            if (package == null)
                return new ErrorResult(Messages.ServicePackageNotFound);

            if (package.OwnerId != dto.OwnerId)
                return new ErrorResult(Messages.UnauthorizedOperation);

            // Aktif randevu kontrolü
            var activeCheck = await CheckActiveAppointmentsAsync(dto.Id);
            if (!activeCheck.Success)
                return activeCheck;

            // Hizmet varlığı ve sahiplik kontrolü
            var serviceCheck = await ValidateServiceOfferingsAsync(dto.OwnerId, dto.ServiceOfferingIds);
            if (!serviceCheck.Success)
                return serviceCheck;

            // Duplicate kontrolü (kendi paketi hariç)
            var duplicateCheck = await CheckDuplicatePackageAsync(dto.OwnerId, dto.ServiceOfferingIds, excludePackageId: dto.Id);
            if (!duplicateCheck.Success)
                return duplicateCheck;

            var services = await serviceOfferingDal.GetServiceOfferingsByIdsAsync(dto.ServiceOfferingIds);
            var newItems = services.Select(s => new ServicePackageItem
            {
                Id = Guid.NewGuid(),
                PackageId = package.Id,
                ServiceOfferingId = s.Id,
                ServiceName = s.ServiceName
            }).ToList();

            try
            {
                await servicePackageDal.UpdatePackageWithItemsAsync(
                    dto.Id,
                    dto.PackageName,
                    dto.TotalPrice,
                    DateTime.UtcNow,
                    newItems);
                return new SuccessResult(Messages.ServicePackageUpdatedSuccess);
            }
            catch (DbUpdateConcurrencyException)
            {
                return new ErrorResult(Messages.ServicePackageModifiedByAnotherProcess);
            }
        }

        public async Task<IResult> DeleteAsync(Guid packageId, Guid currentUserId)
        {
            var package = await servicePackageDal.Get(p => p.Id == packageId);
            if (package == null)
                return new ErrorResult(Messages.ServicePackageNotFound);

            var ownerCheck = await VerifyOwnershipAsync(package.OwnerId, currentUserId);
            if (!ownerCheck.Success)
                return ownerCheck;

            var activeCheck = await CheckActiveAppointmentsAsync(packageId);
            if (!activeCheck.Success)
                return activeCheck;

            await servicePackageDal.Remove(package);
            return new SuccessResult(Messages.ServicePackageDeletedSuccess);
        }

        public async Task<IDataResult<List<ServicePackageGetDto>>> GetAllByOwnerAsync(Guid ownerId, Guid currentUserId)
        {
            // Paket listesi keşif / randevu kartlarında herkese açık katalog bilgisidir (hizmetler gibi).
            // Ekleme/güncelleme/silme için VerifyOwnershipAsync kullanılmaya devam eder.
            var existsCheck = await OwnerExistsAsync(ownerId);
            if (!existsCheck.Success)
                return new ErrorDataResult<List<ServicePackageGetDto>>(existsCheck.Message);

            var packages = await servicePackageDal.GetPackagesByOwnerIdAsync(ownerId);
            return new SuccessDataResult<List<ServicePackageGetDto>>(packages);
        }

        public async Task<IDataResult<List<ServicePackageAdminGetDto>>> GetAllForAdminAsync()
        {
            var packages = await servicePackageDal.GetAllForAdminAsync();

            var ownerInfo = await ownerEnricher.ResolveAsync(
                packages.Select(p => p.OwnerId).ToList());

            foreach (var p in packages)
            {
                if (ownerInfo.TryGetValue(p.OwnerId, out var owner))
                {
                    p.OwnerType = owner.OwnerType;
                    p.OwnerName = owner.OwnerName;
                    p.OwnerNumber = owner.OwnerNumber;
                    p.OwnerImageUrl = owner.OwnerImageUrl;
                }
            }

            return new SuccessDataResult<List<ServicePackageAdminGetDto>>(packages);
        }

        public async Task<IDataResult<List<AppointmentServicePackageDto>>> GetPackagesByAppointmentAsync(Guid appointmentId)
        {
            var packages = await servicePackageDal.GetPackagesByAppointmentIdAsync(appointmentId);
            return new SuccessDataResult<List<AppointmentServicePackageDto>>(packages);
        }

        public async Task<IResult> SyncForOwnerAsync(Guid ownerId, List<ServicePackageSyncItemDto>? packages, Guid currentUserId)
        {
            var ownerCheck = await VerifyOwnershipAsync(ownerId, currentUserId);
            if (!ownerCheck.Success)
                return ownerCheck;

            var desired = packages ?? new List<ServicePackageSyncItemDto>();
            var offerings = await serviceOfferingDal.GetAll(o => o.OwnerId == ownerId);
            var nameToId = offerings
                .GroupBy(o => o.ServiceName.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

            var existingPackages = await servicePackageDal.GetAll(p => p.OwnerId == ownerId);
            var desiredIds = new HashSet<Guid>(
                desired.Where(d => d.Id.HasValue && d.Id.Value != Guid.Empty).Select(d => d.Id!.Value));

            var resolvedDesired = new List<(ServicePackageSyncItemDto Item, List<Guid> ServiceIds)>();
            foreach (var item in desired)
            {
                var resolvedIds = await ResolveOfferingIdsAsync(ownerId, item, nameToId);
                if (!resolvedIds.Success)
                    return new ErrorResult(resolvedIds.Message);

                var serviceIds = resolvedIds.Data;
                if (serviceIds == null || serviceIds.Count == 0)
                    return new ErrorResult(Messages.ServiceOfferingRequired);

                resolvedDesired.Add((item, serviceIds));
            }

            for (var i = 0; i < resolvedDesired.Count; i++)
            {
                for (var j = i + 1; j < resolvedDesired.Count; j++)
                {
                    if (HasSameServiceSet(resolvedDesired[i].ServiceIds, resolvedDesired[j].ServiceIds))
                        return new ErrorResult(Messages.ServicePackageDuplicateServices);
                }
            }

            foreach (var existing in existingPackages)
            {
                if (desiredIds.Contains(existing.Id))
                    continue;

                var deleteResult = await DeleteAsync(existing.Id, currentUserId);
                if (!deleteResult.Success)
                    return deleteResult;
            }

            foreach (var (item, serviceIds) in resolvedDesired)
            {
                if (item.Id.HasValue && item.Id.Value != Guid.Empty &&
                    existingPackages.Any(p => p.Id == item.Id.Value))
                {
                    var updateResult = await UpdateAsync(new ServicePackageUpdateDto
                    {
                        Id = item.Id.Value,
                        OwnerId = ownerId,
                        PackageName = item.PackageName,
                        TotalPrice = item.TotalPrice,
                        ServiceOfferingIds = serviceIds,
                    }, currentUserId);
                    if (!updateResult.Success)
                        return updateResult;
                }
                else
                {
                    var addResult = await AddAsync(new ServicePackageCreateDto
                    {
                        OwnerId = ownerId,
                        PackageName = item.PackageName,
                        TotalPrice = item.TotalPrice,
                        ServiceOfferingIds = serviceIds,
                    }, currentUserId);
                    if (!addResult.Success)
                        return addResult;
                }
            }

            return new SuccessResult();
        }

        private async Task<IDataResult<List<Guid>>> ResolveOfferingIdsAsync(
            Guid ownerId,
            ServicePackageSyncItemDto item,
            Dictionary<string, Guid> nameToId)
        {
            var ids = new List<Guid>();
            if (item.ServiceOfferingIds != null)
            {
                foreach (var id in item.ServiceOfferingIds.Where(x => x != Guid.Empty))
                {
                    if (!ids.Contains(id))
                        ids.Add(id);
                }
            }

            if (item.ServiceNames != null)
            {
                foreach (var rawName in item.ServiceNames)
                {
                    var name = rawName?.Trim();
                    if (string.IsNullOrEmpty(name))
                        continue;

                    if (!nameToId.TryGetValue(name, out var offeringId))
                        return new ErrorDataResult<List<Guid>>(Messages.ServicePackageServiceNotFound);

                    if (!ids.Contains(offeringId))
                        ids.Add(offeringId);
                }
            }

            if (ids.Count == 0)
                return new ErrorDataResult<List<Guid>>(Messages.ServiceOfferingRequired);

            var validation = await ValidateServiceOfferingsAsync(ownerId, ids);
            if (!validation.Success)
                return new ErrorDataResult<List<Guid>>(validation.Message);

            return new SuccessDataResult<List<Guid>>(ids);
        }

        // --- Yardımcı Metodlar ---

        private async Task<IResult> OwnerExistsAsync(Guid ownerId)
        {
            var store = await barberStoreDal.Get(s => s.Id == ownerId);
            if (store != null)
                return new SuccessResult();

            var fb = await freeBarberDal.Get(f => f.Id == ownerId);
            if (fb != null)
                return new SuccessResult();

            return new ErrorResult(Messages.StoreNotFound);
        }

        private async Task<IResult> VerifyOwnershipAsync(Guid ownerId, Guid currentUserId)
        {
            var store = await barberStoreDal.Get(s => s.Id == ownerId);
            if (store != null)
                return store.BarberStoreOwnerId == currentUserId
                    ? new SuccessResult()
                    : new ErrorResult(Messages.UnauthorizedOperation);

            var fb = await freeBarberDal.Get(f => f.Id == ownerId);
            if (fb != null)
                return fb.FreeBarberUserId == currentUserId
                    ? new SuccessResult()
                    : new ErrorResult(Messages.UnauthorizedOperation);

            return new ErrorResult(Messages.StoreNotFound);
        }

        private async Task<IResult> ValidateServiceOfferingsAsync(Guid ownerId, List<Guid> serviceIds)
        {
            if (serviceIds == null || serviceIds.Count == 0)
                return new ErrorResult(Messages.ServiceOfferingRequired);

            var services = await serviceOfferingDal.GetServiceOfferingsByIdsAsync(serviceIds);
            if (services.Count != serviceIds.Count)
                return new ErrorResult(Messages.ServicePackageServiceNotFound);

            // Tüm hizmetler bu owner'a ait mi?
            var ownerMismatch = services.Any(s => s.OwnerId != ownerId);
            if (ownerMismatch)
                return new ErrorResult(Messages.ServiceOfferingOwnerMismatch);

            return new SuccessResult();
        }

        private async Task<IResult> CheckActiveAppointmentsAsync(Guid packageId)
        {
            var hasActive = await servicePackageDal.HasActiveAppointmentWithPackageAsync(packageId);
            if (hasActive)
                return new ErrorResult(Messages.ServicePackageHasActiveAppointments);

            return new SuccessResult();
        }

        private async Task<IResult> CheckDuplicatePackageAsync(Guid ownerId, List<Guid> serviceIds, Guid? excludePackageId)
        {
            var ownerPackages = await servicePackageDal.GetAll(p => p.OwnerId == ownerId);
            var offerings = await serviceOfferingDal.GetAll(o => o.OwnerId == ownerId);
            var nameByOfferingId = offerings
                .GroupBy(o => o.Id)
                .ToDictionary(g => g.Key, g => g.First().ServiceName?.Trim() ?? string.Empty);

            var sortedNewIds = serviceIds.OrderBy(x => x).ToList();
            var newNames = NormalizeServiceNames(sortedNewIds, nameByOfferingId);

            foreach (var pkg in ownerPackages)
            {
                if (excludePackageId.HasValue && pkg.Id == excludePackageId.Value)
                    continue;

                var pkgWithItems = await servicePackageDal.GetWithItemsAsync(pkg.Id);
                if (pkgWithItems == null) continue;

                var sortedExistingIds = pkgWithItems.Items
                    .Select(i => i.ServiceOfferingId)
                    .OrderBy(x => x)
                    .ToList();

                if (sortedNewIds.SequenceEqual(sortedExistingIds))
                    return new ErrorResult(Messages.ServicePackageDuplicateServices);

                var existingNames = NormalizeItemServiceNames(pkgWithItems.Items);
                if (newNames.Count > 0 && existingNames.Count > 0 &&
                    newNames.SequenceEqual(existingNames, StringComparer.OrdinalIgnoreCase))
                    return new ErrorResult(Messages.ServicePackageDuplicateServices);
            }

            return new SuccessResult();
        }

        private static bool HasSameServiceSet(List<Guid> left, List<Guid> right)
        {
            var a = left.OrderBy(x => x).ToList();
            var b = right.OrderBy(x => x).ToList();
            return a.SequenceEqual(b);
        }

        private static List<string> NormalizeServiceNames(
            IReadOnlyList<Guid> serviceIds,
            IReadOnlyDictionary<Guid, string> nameByOfferingId)
        {
            return serviceIds
                .Select(id => nameByOfferingId.TryGetValue(id, out var name) ? name : string.Empty)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static List<string> NormalizeItemServiceNames(IEnumerable<ServicePackageItem> items)
        {
            return items
                .Select(i => i.ServiceName?.Trim() ?? string.Empty)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
