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
        // NotificationSoundUrl kaldırıldı - kullanıcı ayarlardan seçemez, backend'deki varsayılan ses kullanılır
    }
}

