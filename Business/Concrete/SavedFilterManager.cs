using Business.Abstract;
using Business.BusinessAspect.Autofac;
using Business.Resources;
using Core.Aspect.Autofac.Logging;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;

namespace Business.Concrete
{
    public class SavedFilterManager : ISavedFilterService
    {
        private readonly ISavedFilterDal _savedFilterDal;

        public SavedFilterManager(ISavedFilterDal savedFilterDal)
        {
            _savedFilterDal = savedFilterDal;
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        public async Task<IDataResult<List<SavedFilterGetDto>>> GetMyFiltersAsync(Guid userId)
        {
            var filters = await _savedFilterDal.GetByUserIdAsync(userId);
            var dtos = filters.Select(f => new SavedFilterGetDto
            {
                Id = f.Id,
                Name = f.Name,
                FilterCriteriaJson = f.FilterCriteriaJson,
                CreatedAt = f.CreatedAt,
            }).ToList();
            return new SuccessDataResult<List<SavedFilterGetDto>>(dtos);
        }

        [SecuredOperation("Admin")]
        [LogAspect]
        public async Task<IDataResult<List<SavedFilterGetDto>>> GetAllSavedFiltersForAdminAsync()
        {
            var filters = await _savedFilterDal.GetAll();
            var dtos = filters.Select(f => new SavedFilterGetDto
            {
                Id = f.Id,
                Name = f.Name,
                FilterCriteriaJson = f.FilterCriteriaJson,
                CreatedAt = f.CreatedAt,
            }).ToList();

            return new SuccessDataResult<List<SavedFilterGetDto>>(dtos);
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        public async Task<IDataResult<SavedFilterGetDto>> CreateAsync(Guid userId, SavedFilterCreateDto dto)
        {
            var trimmedName = dto.Name.Trim();
            var existingByName = await _savedFilterDal.Get(f => f.UserId == userId && f.Name == trimmedName);
            if (existingByName != null)
                return new ErrorDataResult<SavedFilterGetDto>(Messages.SavedFilterNameAlreadyExists);

            var existingByCriteria = await _savedFilterDal.Get(f => f.UserId == userId && f.FilterCriteriaJson == dto.FilterCriteriaJson);
            if (existingByCriteria != null)
                return new ErrorDataResult<SavedFilterGetDto>(Messages.SavedFilterCriteriaAlreadyExists);

            var entity = new SavedFilter
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Name = dto.Name.Trim(),
                FilterCriteriaJson = dto.FilterCriteriaJson,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };

            await _savedFilterDal.Add(entity);

            return new SuccessDataResult<SavedFilterGetDto>(new SavedFilterGetDto
            {
                Id = entity.Id,
                Name = entity.Name,
                FilterCriteriaJson = entity.FilterCriteriaJson,
                CreatedAt = entity.CreatedAt,
            }, Messages.SavedFilterCreatedSuccess);
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        public async Task<IDataResult<SavedFilterGetDto>> UpdateAsync(Guid userId, SavedFilterUpdateDto dto)
        {
            var entity = await _savedFilterDal.Get(f => f.Id == dto.Id);
            if (entity == null)
                return new ErrorDataResult<SavedFilterGetDto>(Messages.SavedFilterNotFound);

            if (entity.UserId != userId)
                return new ErrorDataResult<SavedFilterGetDto>(Messages.SavedFilterNotOwner);

            var trimmedUpdateName = dto.Name.Trim();
            if (trimmedUpdateName != entity.Name)
            {
                var existingByName = await _savedFilterDal.Get(f => f.UserId == userId && f.Name == trimmedUpdateName);
                if (existingByName != null)
                    return new ErrorDataResult<SavedFilterGetDto>(Messages.SavedFilterNameAlreadyExists);
            }

            if (dto.FilterCriteriaJson != entity.FilterCriteriaJson)
            {
                var existingByCriteria = await _savedFilterDal.Get(f => f.UserId == userId && f.FilterCriteriaJson == dto.FilterCriteriaJson && f.Id != dto.Id);
                if (existingByCriteria != null)
                    return new ErrorDataResult<SavedFilterGetDto>(Messages.SavedFilterCriteriaAlreadyExists);
            }

            entity.Name = trimmedUpdateName;
            entity.FilterCriteriaJson = dto.FilterCriteriaJson;
            entity.UpdatedAt = DateTime.UtcNow;

            await _savedFilterDal.Update(entity);

            return new SuccessDataResult<SavedFilterGetDto>(new SavedFilterGetDto
            {
                Id = entity.Id,
                Name = entity.Name,
                FilterCriteriaJson = entity.FilterCriteriaJson,
                CreatedAt = entity.CreatedAt,
            }, Messages.SavedFilterUpdatedSuccess);
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        public async Task<IDataResult<bool>> DeleteAsync(Guid userId, Guid filterId)
        {
            var entity = await _savedFilterDal.Get(f => f.Id == filterId);
            if (entity == null)
                return new ErrorDataResult<bool>(Messages.SavedFilterNotFound);

            if (entity.UserId != userId)
                return new ErrorDataResult<bool>(Messages.SavedFilterNotOwner);

            await _savedFilterDal.Remove(entity);
            return new SuccessDataResult<bool>(true, Messages.SavedFilterDeletedSuccess);
        }
    }
}
