using System;
using Entities.Abstract;

namespace Entities.Concrete.Dto
{
    public class SettingUpdateDto : IDto
    {
        public bool ShowImageAnimation { get; set; }

        /// <summary>Null = mevcut değeri koru (eski istemciler için).</summary>
        public bool? ShowPriceAnimation { get; set; }
        /// <summary>Null = mevcut değeri koru (eski istemciler için).</summary>
        public bool? EnableNotificationSound { get; set; }
        public bool? SocialNotifyPostEngagement { get; set; }
        public bool? SocialNotifyComments { get; set; }
        public bool? SocialNotifyFollowers { get; set; }
        public bool? SocialNotifyMentions { get; set; }
        public bool? SocialNotifyStoryEngagement { get; set; }
        // NotificationSoundUrl kaldırıldı - kullanıcı ayarlardan seçemez, backend'deki varsayılan ses kullanılır
    }
}

