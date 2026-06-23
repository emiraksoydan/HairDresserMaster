using Business.Abstract;
using Business.Resources;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;

namespace Business.Concrete
{
    public class SettingManager : ISettingService
    {
        private readonly ISettingDal _settingDal;
        private readonly IUserDal _userDal;

        public SettingManager(ISettingDal settingDal, IUserDal userDal)
        {
            _settingDal = settingDal;
            _userDal = userDal;
        }

        public async Task<IDataResult<SettingGetDto>> GetByUserIdAsync(Guid userId)
        {
            var setting = await _settingDal.GetByUserIdAsync(userId);
            if (setting == null)
            {
                // Varsayılan ayarları oluştur
                await InitializeDefaultAsync(userId);
                setting = await _settingDal.GetByUserIdAsync(userId);
            }

            var dto = new SettingGetDto
            {
                Id = setting.Id,
                UserId = setting.UserId,
                ShowImageAnimation = setting.ShowImageAnimation,
                ShowPriceAnimation = setting.ShowPriceAnimation,
                EnableNotificationSound = setting.EnableNotificationSound,
                SocialNotifyPostEngagement = setting.SocialNotifyPostEngagement,
                SocialNotifyComments = setting.SocialNotifyComments,
                SocialNotifyFollowers = setting.SocialNotifyFollowers,
                SocialNotifyMentions = setting.SocialNotifyMentions,
                SocialNotifyStoryEngagement = setting.SocialNotifyStoryEngagement,
            };

            return new SuccessDataResult<SettingGetDto>(dto);
        }

        public async Task<IResult> UpdateAsync(Guid userId, SettingUpdateDto dto)
        {
            var setting = await _settingDal.GetByUserIdAsync(userId);
            if (setting == null)
            {
                // Varsayılan ayarları oluştur
                await InitializeDefaultAsync(userId);
                setting = await _settingDal.GetByUserIdAsync(userId);
            }

            setting.ShowImageAnimation = dto.ShowImageAnimation;
            if (dto.ShowPriceAnimation.HasValue)
                setting.ShowPriceAnimation = dto.ShowPriceAnimation.Value;
            if (dto.EnableNotificationSound.HasValue)
                setting.EnableNotificationSound = dto.EnableNotificationSound.Value;
            if (dto.SocialNotifyPostEngagement.HasValue)
                setting.SocialNotifyPostEngagement = dto.SocialNotifyPostEngagement.Value;
            if (dto.SocialNotifyComments.HasValue)
                setting.SocialNotifyComments = dto.SocialNotifyComments.Value;
            if (dto.SocialNotifyFollowers.HasValue)
                setting.SocialNotifyFollowers = dto.SocialNotifyFollowers.Value;
            if (dto.SocialNotifyMentions.HasValue)
                setting.SocialNotifyMentions = dto.SocialNotifyMentions.Value;
            if (dto.SocialNotifyStoryEngagement.HasValue)
                setting.SocialNotifyStoryEngagement = dto.SocialNotifyStoryEngagement.Value;
            setting.UpdatedAt = DateTime.UtcNow;

            await _settingDal.Update(setting);

            return new SuccessResult(Messages.SettingsUpdatedSuccess);
        }

        public async Task<IResult> InitializeDefaultAsync(Guid userId)
        {
            // Zaten varsa oluşturma
            var existing = await _settingDal.GetByUserIdAsync(userId);
            if (existing != null)
            {
                return new SuccessResult(Messages.SettingsAlreadyExist);
            }

            // FIX: UserId foreign key constraint validation
            // Settings tablosu User tablosuna foreign key ile bağlı
            // User varlığını kontrol et, yoksa error döndür
            var user = await _userDal.Get(u => u.Id == userId);
            if (user == null)
            {
                return new ErrorResult(string.Format(Messages.SettingUserNotFoundWithUserId, userId));
            }
            
            var setting = new Setting
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ShowImageAnimation = true,
                ShowPriceAnimation = true,
                EnableNotificationSound = true,
                SocialNotifyPostEngagement = true,
                SocialNotifyComments = true,
                SocialNotifyFollowers = true,
                SocialNotifyMentions = true,
                SocialNotifyStoryEngagement = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _settingDal.Add(setting);

            return new SuccessResult(Messages.SettingsDefaultsCreatedSuccess);
        }
    }
}
