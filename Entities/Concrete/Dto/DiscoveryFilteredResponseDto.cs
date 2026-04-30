using Entities.Abstract;

namespace Entities.Concrete.Dto
{
    /// <summary>Müşteri keşif paneli: tek round-trip ile iki liste.</summary>
    public class DiscoveryFilteredResponseDto : IDto
    {
        public List<BarberStoreGetDto> Stores { get; set; } = new();
        public List<FreeBarberGetDto> FreeBarbers { get; set; } = new();
    }
}
