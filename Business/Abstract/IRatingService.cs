using Core.Utilities.Results;
using Entities.Concrete.Dto;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Business.Abstract
{
    public interface IRatingService
    {
        Task<IDataResult<RatingGetDto>> CreateRatingAsync(Guid userId, CreateRatingDto dto);
        Task<IDataResult<bool>> DeleteRatingAsync(Guid userId, Guid ratingId);
        Task<IDataResult<RatingGetDto>> GetRatingByIdAsync(Guid ratingId);
        /// <summary>
        /// Target'a yapılan değerlendirmeler (opsiyonel pagination).
        /// `beforeUtc` = son yüklenen rating'in CreatedAt'i; `limit` = sayfa boyutu.
        /// Parametresiz çağrı: eski davranış (tüm liste).
        /// </summary>
        Task<IDataResult<List<RatingGetDto>>> GetRatingsByTargetAsync(Guid targetId, DateTime? beforeUtc = null, Guid? beforeId = null, int? limit = null);
        Task<IDataResult<RatingGetDto>> GetMyRatingForAppointmentAsync(Guid userId, Guid appointmentId, Guid targetId);
        Task<IDataResult<List<RatingGetDto>>> GetAllRatingsForAdminAsync();
    }
}
