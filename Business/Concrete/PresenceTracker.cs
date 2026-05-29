using System;
using System.Collections.Generic;
using System.Linq;
using Business.Abstract;

namespace Business.Concrete
{
    /// <summary>
    /// Bellek içi çevrimiçi kullanıcı takibi. Kullanıcı başına aktif bağlantı sayısını tutar.
    /// Thread-safe; tek instance (singleton) olarak kaydedilmelidir.
    /// </summary>
    public class PresenceTracker : IPresenceTracker
    {
        private readonly Dictionary<Guid, int> _connections = new();
        private readonly object _lock = new();

        public bool Connected(Guid userId)
        {
            if (userId == Guid.Empty) return false;
            lock (_lock)
            {
                if (_connections.TryGetValue(userId, out var count))
                {
                    _connections[userId] = count + 1;
                    return false; // zaten çevrimiçiydi
                }

                _connections[userId] = 1;
                return true; // ilk bağlantı
            }
        }

        public bool Disconnected(Guid userId)
        {
            if (userId == Guid.Empty) return false;
            lock (_lock)
            {
                if (!_connections.TryGetValue(userId, out var count))
                    return false;

                if (count <= 1)
                {
                    _connections.Remove(userId);
                    return true; // son bağlantı kapandı -> çevrimdışı
                }

                _connections[userId] = count - 1;
                return false;
            }
        }

        public IReadOnlyCollection<Guid> GetOnlineUserIds()
        {
            lock (_lock)
            {
                return _connections.Keys.ToList();
            }
        }

        public int OnlineCount()
        {
            lock (_lock)
            {
                return _connections.Count;
            }
        }
    }
}
