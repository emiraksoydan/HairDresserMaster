using System;
using System.Collections.Generic;

namespace Business.Abstract
{
    /// <summary>
    /// SignalR bağlantılarına dayalı anlık (in-memory) çevrimiçi kullanıcı takibi.
    /// Aynı kullanıcının birden fazla bağlantısı (çoklu cihaz/sekme) referans sayımıyla yönetilir;
    /// kullanıcı yalnızca tüm bağlantıları kapanınca çevrimdışı sayılır.
    /// Tek instance (singleton) olarak kaydedilmelidir.
    /// </summary>
    public interface IPresenceTracker
    {
        /// <summary>Yeni bağlantı eklendi. Bu kullanıcının ilk bağlantısıysa true döner.</summary>
        bool Connected(Guid userId);

        /// <summary>Bir bağlantı kapandı. Bu kullanıcının son bağlantısıysa true döner.</summary>
        bool Disconnected(Guid userId);

        /// <summary>Şu an çevrimiçi olan benzersiz kullanıcı kimlikleri.</summary>
        IReadOnlyCollection<Guid> GetOnlineUserIds();

        /// <summary>Çevrimiçi benzersiz kullanıcı sayısı.</summary>
        int OnlineCount();
    }
}
