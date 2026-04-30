using Business.Abstract;
using Core.Utilities.Results;
using Entities.Concrete.Dto;

namespace Business.Concrete
{
    public class DiscoveryManager : IDiscoveryService
    {
        private readonly IBarberStoreService _barberStoreService;
        private readonly IFreeBarberService _freeBarberService;

        public DiscoveryManager(IBarberStoreService barberStoreService, IFreeBarberService freeBarberService)
        {
            _barberStoreService = barberStoreService;
            _freeBarberService = freeBarberService;
        }

        public async Task<IDataResult<DiscoveryFilteredResponseDto>> GetFilteredDiscoveryAsync(
            FilterRequestDto filter,
            int limit = 20,
            int storeOffset = 0,
            int freeBarberOffset = 0)
        {
            var storesResult = await _barberStoreService.GetFilteredStoresAsync(filter, limit, storeOffset);
            if (!storesResult.Success)
                return new ErrorDataResult<DiscoveryFilteredResponseDto>(storesResult.Message ?? string.Empty);

            var freeBarbersResult = await _freeBarberService.GetFilteredFreeBarbersAsync(filter, limit, freeBarberOffset);
            if (!freeBarbersResult.Success)
                return new ErrorDataResult<DiscoveryFilteredResponseDto>(freeBarbersResult.Message ?? string.Empty);

            return new SuccessDataResult<DiscoveryFilteredResponseDto>(new DiscoveryFilteredResponseDto
            {
                Stores = storesResult.Data ?? new List<BarberStoreGetDto>(),
                FreeBarbers = freeBarbersResult.Data ?? new List<FreeBarberGetDto>(),
            });
        }
    }
}
