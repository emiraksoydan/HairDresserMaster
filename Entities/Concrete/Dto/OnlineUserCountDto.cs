using Entities.Abstract;

namespace Entities.Concrete.Dto
{
    /// <summary>Admin paneli için anlık çevrimiçi kullanıcı sayıları (SignalR bağlantılarına dayalı).</summary>
    public class OnlineUserCountDto : IDto
    {
        public int Total { get; set; }
        public int Customers { get; set; }
        public int FreeBarbers { get; set; }
        public int Stores { get; set; }
    }
}
