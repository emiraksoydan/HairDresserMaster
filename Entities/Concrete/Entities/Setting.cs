using System;
using Entities.Abstract;

namespace Entities.Concrete.Entities
{
    public class Setting : IEntity
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public User User { get; set; }
        public bool ShowImageAnimation { get; set; } = true; // Resim animasyonlarını göster/gizle (varsayılan: true)
        public bool ShowPriceAnimation { get; set; } = true; // Kazanç/fiyat sayı sayacı animasyonu (varsayılan: true)
        public bool EnableNotificationSound { get; set; } = true; // Bildirim sesleri açık/kapalı
        public bool SocialNotifyPostEngagement { get; set; } = true;
        public bool SocialNotifyComments { get; set; } = true;
        public bool SocialNotifyFollowers { get; set; } = true;
        public bool SocialNotifyMentions { get; set; } = true;
        public bool SocialNotifyStoryEngagement { get; set; } = true;
        // NotificationSoundUrl kaldırıldı - artık backend'deki varsayılan ses dosyası kullanılıyor (wwwroot/sounds/notification.mp3)
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}

