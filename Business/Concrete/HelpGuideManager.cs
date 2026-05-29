using Business.Abstract;
using Business.Resources;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;

namespace Business.Concrete
{
    public class HelpGuideManager : IHelpGuideService
    {
        private readonly IHelpGuideDal _helpGuideDal;

        public HelpGuideManager(IHelpGuideDal helpGuideDal)
        {
            _helpGuideDal = helpGuideDal;
        }

        public async Task<IDataResult<List<HelpGuideGetDto>>> GetByUserTypeAsync(int userType)
        {
            var guides = await _helpGuideDal.GetByUserTypeAsync(userType);
            var dtos = guides.Select(Map).ToList();
            return new SuccessDataResult<List<HelpGuideGetDto>>(dtos);
        }

        public async Task<IDataResult<List<HelpGuideGetDto>>> GetActiveByUserTypeAsync(int userType)
        {
            var guides = await _helpGuideDal.GetActiveByUserTypeAsync(userType);
            var dtos = guides.Select(Map).ToList();
            return new SuccessDataResult<List<HelpGuideGetDto>>(dtos);
        }

        // ============================================================
        // Admin CRUD
        // ============================================================
        public async Task<IDataResult<List<HelpGuideGetDto>>> GetAllForAdminAsync(int? userType)
        {
            var all = userType.HasValue
                ? await _helpGuideDal.GetAll(g => g.UserType == userType.Value)
                : await _helpGuideDal.GetAll();

            var dtos = all
                .OrderBy(g => g.UserType)
                .ThenBy(g => g.Order)
                .ThenBy(g => g.CreatedAt)
                .Select(Map)
                .ToList();
            return new SuccessDataResult<List<HelpGuideGetDto>>(dtos);
        }

        public async Task<IDataResult<HelpGuideGetDto>> CreateAsync(HelpGuideCreateDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto?.Title))
                return new ErrorDataResult<HelpGuideGetDto>(null!, Messages.HelpGuideTitleRequired);

            var entity = new HelpGuide
            {
                Id = Guid.NewGuid(),
                UserType = dto.UserType,
                Title = dto.Title.Trim(),
                Description = dto.Description?.Trim() ?? string.Empty,
                TranslationKey = dto.TranslationKey?.Trim() ?? string.Empty,
                Order = dto.Order,
                IsActive = dto.IsActive,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            await _helpGuideDal.Add(entity);
            return new SuccessDataResult<HelpGuideGetDto>(Map(entity), Messages.HelpGuideCreated);
        }

        public async Task<IResult> UpdateAsync(Guid id, HelpGuideUpdateDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto?.Title))
                return new ErrorResult(Messages.HelpGuideTitleRequired);

            var entity = await _helpGuideDal.Get(g => g.Id == id);
            if (entity == null) return new ErrorResult(Messages.HelpGuideNotFound);

            entity.UserType = dto.UserType;
            entity.Title = dto.Title.Trim();
            entity.Description = dto.Description?.Trim() ?? string.Empty;
            entity.TranslationKey = dto.TranslationKey?.Trim() ?? string.Empty;
            entity.Order = dto.Order;
            entity.IsActive = dto.IsActive;
            entity.UpdatedAt = DateTime.UtcNow;
            await _helpGuideDal.Update(entity);
            return new SuccessResult(Messages.HelpGuideUpdated);
        }

        public async Task<IResult> DeleteAsync(Guid id)
        {
            var entity = await _helpGuideDal.Get(g => g.Id == id);
            if (entity == null) return new ErrorResult(Messages.HelpGuideNotFound);
            await _helpGuideDal.Remove(entity);
            return new SuccessResult(Messages.HelpGuideDeleted);
        }

        public async Task<IResult> SetActiveAsync(Guid id, bool isActive)
        {
            var entity = await _helpGuideDal.Get(g => g.Id == id);
            if (entity == null) return new ErrorResult(Messages.HelpGuideNotFound);
            entity.IsActive = isActive;
            entity.UpdatedAt = DateTime.UtcNow;
            await _helpGuideDal.Update(entity);
            return new SuccessResult(Messages.OperationSuccess);
        }

        private static HelpGuideGetDto Map(HelpGuide g) => new()
        {
            Id = g.Id,
            UserType = g.UserType,
            Title = g.Title,
            Description = g.Description,
            TranslationKey = g.TranslationKey ?? string.Empty,
            Order = g.Order,
            IsActive = g.IsActive,
        };
    }
}
