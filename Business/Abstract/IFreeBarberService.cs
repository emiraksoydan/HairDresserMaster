using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Core.Utilities.Results;
using Entities.Concrete.Dto;

namespace Business.Abstract
{
    public interface IFreeBarberService
    {
        Task<IDataResult<Guid>> Add(FreeBarberCreateDto freeBarberCreateDto, Guid currentUserId);
        Task<IResult> Update(FreeBarberUpdateDto freeBarberUpdateDto, Guid currentUserId);
        Task<IResult> DeleteAsync(Guid panelId, Guid currentUserId);
        Task<IDataResult<List<FreeBarberGetDto>>> GetNearbyFreeBarberAsync(double lat, double lon, double distance, Guid? currentUserId = null, int limit = 100);
        
        // Filtreleme ve arama
        Task<IDataResult<List<FreeBarberGetDto>>> GetFilteredFreeBarbersAsync(FilterRequestDto filter, int limit = 100, int offset = 0);
        
        Task<IDataResult<FreeBarberMinePanelDto>> GetMyPanel(Guid currentUserId);
        Task<IDataResult<FreeBarberMinePanelDetailDto>> GetMyPanelDetail(Guid panelId);
        Task<IDataResult<FreeBarberMinePanelDto>> GetFreeBarberForUsers(Guid freeBarberId);
        Task<IResult> UpdateLocationAsync(UpdateLocationDto dto, Guid currentUserId);


        Task<IResult> UpdateAvailabilityAsync(bool isAvailable, Guid currentUserId);

        /// <summary>Serbest berberin kazanç verilerini döndürür.</summary>
        Task<IDataResult<EarningsDto>> GetEarningsAsync(Guid currentUserId, DateTime startDate, DateTime endDate);

        /// <summary>
        /// Hesap silme akışında kullanılır. Kullanıcıya ait paneli ve bağlı verilerini siler.
        /// SecuredOperation, IsAvailable ve randevu kontrolü atlanır.
        /// </summary>
        Task<IResult> DeleteByUserIdAsync(Guid userId);
    }
}
