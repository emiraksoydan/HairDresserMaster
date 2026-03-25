using Business.Abstract;
using Business.Resources;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Mapster;

namespace Business.Concrete
{
    public class ServiceOfferingManager(
        IServiceOfferingDal serviceOfferingDal,
        IBarberStoreDal barberStoreDal,
        IFreeBarberDal freeBarberDal) : IServiceOfferingService
    {
        public async Task<IResult> AddRangeAsync(List<ServiceOffering> list)
        {
            await serviceOfferingDal.AddRange(list);
            return new SuccessResult();
        }

        public async Task<IResult> UpdateRange(List<ServiceOfferingUpdateDto> serviceOfferingUpdateDto, Guid currentUserId)
        {
            if (serviceOfferingUpdateDto == null || serviceOfferingUpdateDto.Count == 0)
                return new SuccessResult("Hizmet bulunamadı.");

            var ownerEntityId = serviceOfferingUpdateDto[0].OwnerId;
            if (!ownerEntityId.HasValue || ownerEntityId.Value == Guid.Empty)
                return new ErrorResult("Hizmet sahibi belirtilmelidir.");

            var ownerCheck = await VerifyUserOwnsServiceOfferingOwnerEntityAsync(ownerEntityId.Value, currentUserId);
            if (!ownerCheck.Success)
                return ownerCheck;

            var storeId = ownerEntityId.Value;

            var existing = await serviceOfferingDal.GetAll(x => x.OwnerId == storeId);

            var dtoIds = serviceOfferingUpdateDto.Where(d => d.Id.HasValue && d.Id.Value != Guid.Empty).Select(d => d.Id!.Value).ToList();

            var updateDtos = serviceOfferingUpdateDto.Where(d => d.Id.HasValue && d.Id.Value != Guid.Empty).ToList();

            var newDtos = serviceOfferingUpdateDto.Where(d => !d.Id.HasValue || d.Id.Value == Guid.Empty).ToList();

            var toDelete = existing.Where(e => !dtoIds.Contains(e.Id)).ToList();

            if (updateDtos.Any())
            {
                var dict = existing.ToDictionary(x => x.Id);

                foreach (var dto in updateDtos)
                {
                    if (!dto.Id.HasValue) continue;

                    if (!dict.TryGetValue(dto.Id.Value, out var entity))
                        continue;

                    if (entity.OwnerId != storeId)
                        return new ErrorResult(Messages.UnauthorizedOperation);

                    dto.Adapt(entity);
                }

                await serviceOfferingDal.UpdateRange(existing.Where(e => dtoIds.Contains(e.Id)).ToList());
            }
            if (newDtos.Any())
            {
                var newEntities = newDtos.Adapt<List<ServiceOffering>>();
                foreach (var e in newEntities)
                {
                    if (e.Id == Guid.Empty)
                        e.Id = Guid.NewGuid();
                    e.OwnerId = storeId;
                    e.CreatedAt = DateTime.UtcNow;
                }
                await serviceOfferingDal.AddRange(newEntities);
            }
            if (toDelete.Any())
            {
                await serviceOfferingDal.DeleteAll(toDelete);
            }
            return new SuccessResult("Hizmetler güncellendi.");
        }

        public async Task<IDataResult<List<ServiceOfferingAdminGetDto>>> GetAllForAdminAsync()
        {
            var offers = await serviceOfferingDal.GetAll();
            var dto = offers
                .OrderBy(o => o.OwnerId)
                .ThenBy(o => o.ServiceName)
                .Select(o => new ServiceOfferingAdminGetDto
                {
                    Id = o.Id,
                    OwnerId = o.OwnerId,
                    Price = o.Price,
                    ServiceName = o.ServiceName ?? string.Empty
                })
                .ToList();
            return new SuccessDataResult<List<ServiceOfferingAdminGetDto>>(dto);
        }

        private async Task<IResult> VerifyUserOwnsServiceOfferingOwnerEntityAsync(Guid ownerEntityId, Guid currentUserId)
        {
            var store = await barberStoreDal.Get(s => s.Id == ownerEntityId);
            if (store != null)
                return store.BarberStoreOwnerId == currentUserId
                    ? new SuccessResult()
                    : new ErrorResult(Messages.UnauthorizedOperation);

            var fb = await freeBarberDal.Get(f => f.Id == ownerEntityId);
            if (fb != null)
                return fb.FreeBarberUserId == currentUserId
                    ? new SuccessResult()
                    : new ErrorResult(Messages.UnauthorizedOperation);

            return new ErrorResult("Hizmet sahibi bulunamadı.");
        }
    }
}
