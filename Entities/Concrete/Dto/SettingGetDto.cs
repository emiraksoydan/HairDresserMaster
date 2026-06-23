using System;
using Entities.Abstract;

namespace Entities.Concrete.Dto
{
    public class SettingGetDto : IDto
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public bool ShowImageAnimation { get; set; }
        public bool ShowPriceAnimation { get; set; }
        public bool EnableNotificationSound { get; set; }
        public bool SocialNotifyPostEngagement { get; set; }
        public bool SocialNotifyComments { get; set; }
        public bool SocialNotifyFollowers { get; set; }
        public bool SocialNotifyMentions { get; set; }
        public bool SocialNotifyStoryEngagement { get; set; }
        // NotificationSoundUrl kaldırıldı - artık backend'deki varsayılan ses dosyası kullanılıyor
    }
}

