using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Concrete
{
    public class EfServicePackageDal : EfEntityRepositoryBase<ServicePackage, DatabaseContext>, IServicePackageDal
    {
        private readonly DatabaseContext _context;

        public EfServicePackageDal(DatabaseContext context) : base(context)
        {
            _context = context;
        }

        public async Task<List<ServicePackageGetDto>> GetPackagesByOwnerIdAsync(Guid ownerId)
        {
            return await _context.ServicePackages
                .Where(p => p.OwnerId == ownerId)
                .OrderBy(p => p.CreatedAt)
                .Select(p => new ServicePackageGetDto
                {
                    Id = p.Id,
                    PackageName = p.PackageName,
                    TotalPrice = p.TotalPrice,
                    Items = p.Items.Select(i => new ServicePackageItemDto
                    {
                        ServiceOfferingId = i.ServiceOfferingId,
                        ServiceName = i.ServiceName
                    }).ToList()
                })
                .ToListAsync();
        }

        public async Task<List<ServicePackageAdminGetDto>> GetAllForAdminAsync()
        {
            return await _context.ServicePackages
                .AsNoTracking()
                .OrderBy(p => p.OwnerId)
                .ThenBy(p => p.PackageName)
                .Select(p => new ServicePackageAdminGetDto
                {
                    Id = p.Id,
                    OwnerId = p.OwnerId,
                    PackageName = p.PackageName,
                    TotalPrice = p.TotalPrice,
                    ItemCount = p.Items.Count,
                    Items = p.Items.Select(i => new ServicePackageItemDto
                    {
                        ServiceOfferingId = i.ServiceOfferingId,
                        ServiceName = i.ServiceName
                    }).ToList()
                })
                .ToListAsync();
        }

        public async Task<List<AppointmentServicePackageDto>> GetPackagesByAppointmentIdAsync(Guid appointmentId)
        {
            return await _context.AppointmentServicePackages
                .Where(a => a.AppointmentId == appointmentId)
                .Select(a => new AppointmentServicePackageDto
                {
                    PackageId = a.PackageId,
                    PackageName = a.PackageName,
                    TotalPrice = a.TotalPrice,
                    ServiceNamesSnapshot = a.ServiceNamesSnapshot
                })
                .ToListAsync();
        }

        public async Task<bool> HasActiveAppointmentWithPackageAsync(Guid packageId)
        {
            var activeStatuses = new[] { AppointmentStatus.Pending, AppointmentStatus.Approved };
            return await _context.AppointmentServicePackages
                .AsNoTracking()
                .AnyAsync(asp => asp.PackageId == packageId &&
                                 activeStatuses.Contains(asp.Appointment.Status));
        }

        public async Task SyncItemServiceNamesForOfferingsAsync(Guid ownerId, List<Guid> serviceOfferingIds)
        {
            if (serviceOfferingIds == null || serviceOfferingIds.Count == 0) return;

            var distinctIds = serviceOfferingIds.Distinct().ToList();

            var nameById = await _context.ServiceOfferings
                .AsNoTracking()
                .Where(o => distinctIds.Contains(o.Id) && o.OwnerId == ownerId)
                .ToDictionaryAsync(o => o.Id, o => o.ServiceName ?? string.Empty);

            var items = await (
                from i in _context.ServicePackageItems
                join p in _context.ServicePackages on i.PackageId equals p.Id
                where distinctIds.Contains(i.ServiceOfferingId) && p.OwnerId == ownerId
                select i
            ).ToListAsync();

            foreach (var item in items)
            {
                if (nameById.TryGetValue(item.ServiceOfferingId, out var name))
                    item.ServiceName = name;
            }

            if (items.Count > 0)
                await _context.SaveChangesAsync();
        }

        public async Task<ServicePackage?> GetWithItemsAsync(Guid packageId)
        {
            return await _context.ServicePackages
                .Include(p => p.Items)
                .FirstOrDefaultAsync(p => p.Id == packageId);
        }

        public async Task UpdatePackageWithItemsAsync(
            Guid packageId,
            string packageName,
            decimal totalPrice,
            DateTime updatedAt,
            IReadOnlyList<ServicePackageItem> newItems)
        {
            var package = await _context.ServicePackages
                .FirstOrDefaultAsync(p => p.Id == packageId);

            if (package == null)
                throw new InvalidOperationException($"ServicePackage not found: {packageId}");

            var oldItems = await _context.ServicePackageItems
                .Where(i => i.PackageId == packageId)
                .ToListAsync();

            if (oldItems.Count > 0)
                _context.ServicePackageItems.RemoveRange(oldItems);

            package.PackageName = packageName;
            package.TotalPrice = totalPrice;
            package.UpdatedAt = updatedAt;

            foreach (var item in newItems)
                item.PackageId = packageId;

            await _context.ServicePackageItems.AddRangeAsync(newItems);

            await _context.SaveChangesAsync();
        }

        public async Task<List<ServicePackage>> GetPackagesByIdsWithItemsAsync(List<Guid> packageIds)
        {
            return await _context.ServicePackages
                .Include(p => p.Items)
                .Where(p => packageIds.Contains(p.Id))
                .ToListAsync();
        }

        public async Task<bool> AnyPackageItemsReferenceOfferingsAsync(Guid ownerId, List<Guid> serviceOfferingIds)
        {
            if (serviceOfferingIds == null || serviceOfferingIds.Count == 0) return false;

            var ids = serviceOfferingIds.Distinct().ToList();
            return await (
                from i in _context.ServicePackageItems
                join p in _context.ServicePackages on i.PackageId equals p.Id
                where ids.Contains(i.ServiceOfferingId) && p.OwnerId == ownerId
                select i.Id
            ).AnyAsync();
        }
    }
}
