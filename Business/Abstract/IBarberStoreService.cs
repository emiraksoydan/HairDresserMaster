using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Core.Utilities.Results;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;

namespace Business.Abstract
{
    public interface IBarberStoreService 
    {
        Task<IDataResult<Guid>> Add(BarberStoreCreateDto barberStoreCreateDto,Guid currentUserId);
        Task<IResult> Update(BarberStoreUpdateDto updateDto,Guid currentUserId);
        Task<IResult> DeleteAsync(Guid storeId, Guid currentUserId);
        Task<IDataResult<BarberStoreDetail>> GetByIdAsync(Guid id);
        Task<IDataResult<List<BarberStoreMineDto>>> GetByCurrentUserAsync(Guid currentUserId);
        Task<IDataResult<List<BarberStoreGetDto>>> GetNearbyStoresAsync(double lat, double lon, double distance, Guid? currentUserId = null, int limit = 100);
        
        // Filtreleme ve arama
        Task<IDataResult<List<BarberStoreGetDto>>> GetFilteredStoresAsync(FilterRequestDto filter, int limit = 100, int offset = 0);

        Task<IDataResult<BarberStoreMineDto>> GetBarberStoreForUsers(Guid storeId);

        /// <summary>Tüm dükkanları getir (yalnızca Admin).</summary>
        Task<IDataResult<List<BarberStoreGetDto>>> GetAllForAdminAsync();

        /// <summary>Belirtilen mağaza için kazanç verilerini döndürür.</summary>
        Task<IDataResult<EarningsDto>> GetEarningsAsync(Guid storeId, Guid currentUserId, DateTime startDate, DateTime endDate);

        /// <summary>
        /// Sahibi olduğu birden fazla mağazanın kazançlarını tek <see cref="EarningsDto"/> içinde birleştirir.
        /// </summary>
        Task<IDataResult<EarningsDto>> GetAggregatedEarningsAsync(IReadOnlyList<Guid> storeIds, Guid currentUserId, DateTime startDate, DateTime endDate);

        /// <summary>
        /// Hesap silme akışında kullanılır. Kullanıcıya ait tüm dükkanları ve bağlı verilerini siler.
        /// SecuredOperation ve randevu kontrolü atlanır.
        /// </summary>
        Task<IResult> DeleteByUserIdAsync(Guid userId);
    }
}
