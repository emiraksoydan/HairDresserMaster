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
        IAppointmentServicePackageDal appointmentServicePackageDal) : IServicePackageService
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

            var package = await servicePackageDal.GetWithItemsAsync(dto.Id);
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

            // Mevcut item'ları temizle ve yeniden ekle
            package.PackageName = dto.PackageName;
            package.TotalPrice = dto.TotalPrice;
            package.UpdatedAt = DateTime.UtcNow;
            package.Items = services.Select(s => new ServicePackageItem
            {
                Id = Guid.NewGuid(),
                PackageId = package.Id,
                ServiceOfferingId = s.Id,
                ServiceName = s.ServiceName
            }).ToList();

            try
            {
                await servicePackageDal.Update(package);
                return new SuccessResult(Messages.ServicePackageUpdatedSuccess);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Aynı paket bu istekle yarışan başka bir işlemde değiştirilmiş/silinmiş olabilir.
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
            var ownerCheck = await VerifyOwnershipAsync(ownerId, currentUserId);
            if (!ownerCheck.Success)
                return new ErrorDataResult<List<ServicePackageGetDto>>(ownerCheck.Message);

            var packages = await servicePackageDal.GetPackagesByOwnerIdAsync(ownerId);
            return new SuccessDataResult<List<ServicePackageGetDto>>(packages);
        }

        public async Task<IDataResult<List<AppointmentServicePackageDto>>> GetPackagesByAppointmentAsync(Guid appointmentId)
        {
            var packages = await servicePackageDal.GetPackagesByAppointmentIdAsync(appointmentId);
            return new SuccessDataResult<List<AppointmentServicePackageDto>>(packages);
        }

        // --- Yardımcı Metodlar ---

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
            var sortedNew = serviceIds.OrderBy(x => x).ToList();

            foreach (var pkg in ownerPackages)
            {
                if (excludePackageId.HasValue && pkg.Id == excludePackageId.Value)
                    continue;

                var pkgWithItems = await servicePackageDal.GetWithItemsAsync(pkg.Id);
                if (pkgWithItems == null) continue;

                var sortedExisting = pkgWithItems.Items
                    .Select(i => i.ServiceOfferingId)
                    .OrderBy(x => x)
                    .ToList();

                if (sortedNew.SequenceEqual(sortedExisting))
                    return new ErrorResult(Messages.ServicePackageDuplicateServices);
            }

            return new SuccessResult();
        }
    }
}
