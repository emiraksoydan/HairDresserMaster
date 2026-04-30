using Core.Utilities.Results;
using Entities.Concrete.Dto;

namespace Business.Abstract
{
    public interface IDiscoveryService
    {
        Task<IDataResult<DiscoveryFilteredResponseDto>> GetFilteredDiscoveryAsync(
            FilterRequestDto filter,
            int limit = 20,
            int storeOffset = 0,
            int freeBarberOffset = 0);
    }
}
