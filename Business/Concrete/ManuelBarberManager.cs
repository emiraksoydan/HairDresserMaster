using Business.Abstract;
using Business.BusinessAspect.Autofac;
using Business.Resources;
using Business.ValidationRules.FluentValidation;
using Core.Aspect.Autofac.Logging;
using Core.Aspect.Autofac.Transaction;
using Core.Aspect.Autofac.Validation;
using Core.Utilities.Business;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;
using Mapster;
using MapsterMapper;

namespace Business.Concrete
{
    public class ManuelBarberManager(IBarberStoreDal barberStoreDal, IManuelBarberDal manuelBarberDal, IAppointmentService appointmentService, IMapper mapper, IImageService imageService, IBarberStoreChairService barberStoreChairService) : IManuelBarberService
    {
        [LogAspect]
        [ValidationAspect(typeof(ManuelBarberCreateValidator))]
        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        public async Task<IResult> AddAsync(ManuelBarberCreateDto dto, Guid currentUserId)
        {
            if (string.IsNullOrWhiteSpace(dto.StoreId) || !Guid.TryParse(dto.StoreId, out var storeId) || storeId == Guid.Empty)
                return new ErrorResult(Messages.StoreNotFound);

            var store = await barberStoreDal.Get(s => s.Id == storeId);
            if (store == null)
                return new ErrorResult(Messages.StoreNotFound);
            if (store.BarberStoreOwnerId != currentUserId)
                return new ErrorResult(Messages.UnauthorizedOperation);

            var barber = mapper.Map<ManuelBarber>(dto);
            barber.StoreId = storeId;
            if (!string.IsNullOrWhiteSpace(dto.Id) && Guid.TryParse(dto.Id, out var clientId) && clientId != Guid.Empty)
                barber.Id = clientId;
            else
                barber.Id = Guid.NewGuid();

            await manuelBarberDal.Add(barber);

            return new SuccessResult(Messages.ManuelBarberAddedSuccess);
        }
        [LogAspect]
        [ValidationAspect(typeof(ManuelBarberUpdateValidator))]
        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        public async Task<IResult> UpdateAsync(ManuelBarberUpdateDto dto, Guid currentUserId)
        {
            var barber = await manuelBarberDal.Get(b => b.Id == dto.Id);


            if (barber == null)
                return new ErrorResult(Messages.ManuelBarberNotFound);

            var store = await barberStoreDal.Get(s => s.Id == barber.StoreId);
            if (store == null)
                return new ErrorResult(Messages.StoreNotFound);
            if (store.BarberStoreOwnerId != currentUserId)
                return new ErrorResult(Messages.UnauthorizedOperation);

            var hasBlockingAppointments = await appointmentService.AnyManuelBarberControl(barber.Id);
            if (hasBlockingAppointments.Data)
                return new ErrorResult(Messages.ManuelBarberHasActiveAppointments);

            var updatedBarber = dto.Adapt(barber);
            await manuelBarberDal.Update(updatedBarber);

            return new SuccessResult(Messages.ManuelBarberUpdatedSuccess);
        }

        [LogAspect]
        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        public async Task<IResult> DeleteAsync(Guid id, Guid currentUserId)
        {
            var barber = await manuelBarberDal.Get(b => b.Id == id);

            if (barber == null)
                return new ErrorResult(Messages.ManuelBarberNotFound);

            var store = await barberStoreDal.Get(s => s.Id == barber.StoreId);
            if (store == null)
                return new ErrorResult(Messages.StoreNotFound);
            if (store.BarberStoreOwnerId != currentUserId)
                return new ErrorResult(Messages.UnauthorizedOperation);

            var ruleResult = await BusinessRules.RunAsync(() => CheckBarberHasNoBlockingAppointments(barber.Id), () => CheckBarberNotAssignedToAnyChair(barber.Id));
            if (ruleResult != null && !ruleResult.Success)
                return ruleResult;

            var mbImages = await imageService.GetImagesByOwnerAsync(barber.Id, ImageOwnerType.ManuelBarber);
            if (mbImages.Success && mbImages.Data != null)
            {
                foreach (var img in mbImages.Data)
                {
                    var del = await imageService.DeleteAsync(img.Id, currentUserId);
                    if (!del.Success)
                        return del;
                }
            }

            await manuelBarberDal.Remove(barber);

            return new SuccessResult(Messages.ManuelBarberDeletedSuccess);

        }

        public async Task<IDataResult<List<ManuelBarberDto>>> GetAllByStoreAsync(Guid storeId, Guid currentUserId)
        {
            var store = await barberStoreDal.Get(s => s.Id == storeId);
            if (store == null)
                return new ErrorDataResult<List<ManuelBarberDto>>(Messages.StoreNotFound);
            if (store.BarberStoreOwnerId != currentUserId)
                return new ErrorDataResult<List<ManuelBarberDto>>(Messages.UnauthorizedOperation);

            var list = await manuelBarberDal.GetBarberDtosByStoreIdAsync(storeId);
            return new SuccessDataResult<List<ManuelBarberDto>>(list);
        }

        [SecuredOperation("Admin")]
        [LogAspect]
        public async Task<IDataResult<List<ManuelBarberAdminGetDto>>> GetAllForAdminAsync()
        {
            var list = await manuelBarberDal.GetAllForAdminAsync();
            return new SuccessDataResult<List<ManuelBarberAdminGetDto>>(list);
        }

        public async Task<IResult> AddRangeAsync(List<ManuelBarberCreateDto> list, Guid storeId)
        {
            var manuelBarbers = list.Adapt<List<ManuelBarber>>();
            foreach (var barber in manuelBarbers)
                barber.StoreId = storeId;

            await manuelBarberDal.AddRange(manuelBarbers);
            return new SuccessResult();
        }


        // Helpers Method
        private async Task<IResult> CheckBarberHasNoBlockingAppointments(Guid barberId)
        {
            var hasBlockingAppointments = await appointmentService.AnyManuelBarberControl(barberId);
            if (hasBlockingAppointments.Data)
                return new ErrorResult(Messages.ManuelBarberHasActiveAppointments);

            return new SuccessResult();
        }

        private async Task<IResult> CheckBarberNotAssignedToAnyChair(Guid barberId)
        {
            var isAttemptChair = await barberStoreChairService.AttemptBarberControl(barberId);
            if (isAttemptChair.Data)
                return new ErrorResult(Messages.BarberAssignedToChair);

            return new SuccessResult();
        }

    
    }
}
