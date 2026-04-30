using Core.Utilities.Results;
using Entities.Concrete.Dto;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Business.Abstract
{
    public interface IFavoriteService
    {
        Task<IDataResult<ToggleFavoriteResponseDto>> ToggleFavoriteAsync(Guid userId, ToggleFavoriteDto dto);
        Task<IDataResult<bool>> IsFavoriteAsync(Guid userId, Guid targetId);
        /// <summary>
        /// Favoriler listesi (opsiyonel pagination).
        /// `beforeUtc` = son yüklenen favorinin CreatedAt'i, `limit` = sayfa boyutu.
        /// Parametresiz çağrı: eski davranış (tüm liste).
        /// </summary>
        Task<IDataResult<List<FavoriteGetDto>>> GetMyFavoritesAsync(Guid userId, DateTime? beforeUtc = null, Guid? beforeId = null, int? limit = null);
        Task<IDataResult<bool>> RemoveFavoriteAsync(Guid userId, Guid targetId);
        Task<IDataResult<List<FavoriteGetDto>>> GetAllFavoritesForAdminAsync();
    }
}
