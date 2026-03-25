
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
using DataAccess.Concrete;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Mapster;
using MapsterMapper;

namespace Business.Concrete
{
    public class BarberStoreChairManager(IBarberStoreChairDal barberStoreChairDal, IBarberStoreDal barberStoreDal, IAppointmentService appointmentService, IMapper mapper) : IBarberStoreChairService
    {
        [SecuredOperation("BarberStore")]
        [LogAspect]
        [ValidationAspect(typeof(BarberStoreChairCreateValidator))]
        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        public async Task<IResult> AddAsync(BarberChairCreateDto dto, Guid currentUserId)
        {
            if (string.IsNullOrWhiteSpace(dto.StoreId))
                return new ErrorResult("Dükkan bulunamadı.");

            if (!Guid.TryParse(dto.StoreId, out var storeId))
                return new ErrorResult("Dükkan Id formatı hatalı.");

            var store = await barberStoreDal.Get(s => s.Id == storeId);
            if (store == null)
                return new ErrorResult("Dükkan bulunamadı.");

            if (store.BarberStoreOwnerId != currentUserId)
                return new ErrorResult(Messages.UnauthorizedOperation);

            Guid? barberId = null;
            if (!string.IsNullOrWhiteSpace(dto.BarberId))
            {
                if (!Guid.TryParse(dto.BarberId, out var parsed))
                    return new ErrorResult("Berber Id formatı hatalı.");
                
                barberId = parsed;
            }

            var ruleResult = await BusinessRules.RunAsync(() => EnsureBarberNotAssignedToAnotherChairAsync(barberId, null));

            if (ruleResult != null)  
                return ruleResult;

            var barberChair = mapper.Map<BarberChair>(dto);
            barberChair.StoreId = storeId;
            barberChair.ManuelBarberId = barberId;
            if (barberChair.Id == Guid.Empty)
                barberChair.Id = Guid.NewGuid();

            await barberStoreChairDal.Add(barberChair);

            return new SuccessResult("Koltuk başarıyla oluşturuldu.");
        }

        public async Task<IResult> AddRangeAsync(List<BarberChair> list)
        {

            await barberStoreChairDal.AddRange(list);
            return new SuccessResult();
        }

        [SecuredOperation("BarberStore")]
        [LogAspect]
        [ValidationAspect(typeof(BarberStoreChairUpdateValidator))]
        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        public async Task<IResult> UpdateAsync(BarberChairUpdateDto dto, Guid currentUserId)
        {
            var barberChair = await barberStoreChairDal.Get(b => b.Id == dto.Id);
            if (barberChair == null)
                return new ErrorResult("Koltuk bulunamadı.");

            var store = await barberStoreDal.Get(s => s.Id == barberChair.StoreId);
            if (store == null)
                return new ErrorResult("Dükkan bulunamadı.");

            if (store.BarberStoreOwnerId != currentUserId)
                return new ErrorResult(Messages.UnauthorizedOperation);

            var ruleResult = await BusinessRules.RunAsync(() => EnsureBarberNotAssignedToAnotherChairAsync(dto.BarberId, dto.Id));

            if (ruleResult != null)
                return ruleResult;

            var hasBlockingAppointments = await appointmentService.AnyChairControl(barberChair.Id);
            if (hasBlockingAppointments.Data)
                return new ErrorResult("Bu koltuğa ait beklemekte olan veya aktif olan randevu işlemi vardır.");

            var updatedChair = dto.Adapt(barberChair);
            await barberStoreChairDal.Update(updatedChair);

            return new SuccessResult("Koltuk güncellendi.");
        }
        public async Task<IDataResult<bool>> AttemptBarberControl(Guid id)
        {
            var hasAttempt = await barberStoreChairDal.AnyAsync(x => x.ManuelBarberId == id);
            return new SuccessDataResult<bool>(hasAttempt);
        }

        [SecuredOperation("BarberStore")]
        [LogAspect]
        public async Task<IResult> DeleteAsync(Guid id, Guid currentUserId)
        {
            var chair = await barberStoreChairDal.Get(b => b.Id == id);

            if (chair == null)
                return new ErrorResult("Koltuk bulunamadı.");

            var store = await barberStoreDal.Get(s => s.Id == chair.StoreId);
            if (store == null)
                return new ErrorResult("Dükkan bulunamadı.");

            if (store.BarberStoreOwnerId != currentUserId)
                return new ErrorResult(Messages.UnauthorizedOperation);

            await barberStoreChairDal.Remove(chair);
            return new SuccessResult("Koltuk silindi.");
        }

        [SecuredOperation("BarberStore")]
        [LogAspect]
        public async Task<IDataResult<List<BarberChairDto>>> GetAllByStoreAsync(Guid storeId, Guid currentUserId)
        {
            var store = await barberStoreDal.Get(s => s.Id == storeId);
            if (store == null)
                return new ErrorDataResult<List<BarberChairDto>>("Dükkan bulunamadı.");

            if (store.BarberStoreOwnerId != currentUserId)
                return new ErrorDataResult<List<BarberChairDto>>(Messages.UnauthorizedOperation);

            var chairs = await barberStoreChairDal.GetAll(c => c.StoreId == storeId);
            var dto = mapper.Map<List<BarberChairDto>>(chairs);

            return new SuccessDataResult<List<BarberChairDto>>(dto);
        }

        [SecuredOperation("Admin")]
        [LogAspect]
        public async Task<IDataResult<List<BarberChairAdminDto>>> GetAllForAdminAsync()
        {
            var chairs = await barberStoreChairDal.GetAll();
            var dto = mapper.Map<List<BarberChairAdminDto>>(chairs);
            return new SuccessDataResult<List<BarberChairAdminDto>>(dto);
        }

        [SecuredOperation("BarberStore")]
        [LogAspect]
        public async Task<IDataResult<BarberChairDto>> GetById(Guid id, Guid currentUserId)
        {
            var chair = await barberStoreChairDal.Get(b => b.Id == id);
            if (chair == null)
                return new ErrorDataResult<BarberChairDto>("Koltuk bulunamadı.");

            var store = await barberStoreDal.Get(s => s.Id == chair.StoreId);
            if (store == null)
                return new ErrorDataResult<BarberChairDto>("Dükkan bulunamadı.");

            if (store.BarberStoreOwnerId != currentUserId)
                return new ErrorDataResult<BarberChairDto>(Messages.UnauthorizedOperation);

            var dto = mapper.Map<BarberChairDto>(chair);

            return new SuccessDataResult<BarberChairDto>(dto);
        }

        private async Task<IResult> EnsureBarberNotAssignedToAnotherChairAsync(Guid? barberId, Guid? currentChairId = null)
        {
          
            if (barberId is null)
                return new SuccessResult();

            var exists = await barberStoreChairDal.Get(c =>
                c.ManuelBarberId == barberId && 
                (currentChairId == null || c.Id != currentChairId) 
            );

            if (exists != null)
                return new ErrorResult("Bu berber zaten başka bir koltuğa atanmış.");

            return new SuccessResult();
        }


    }
}
