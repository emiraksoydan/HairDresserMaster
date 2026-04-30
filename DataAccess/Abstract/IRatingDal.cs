using Core.DataAccess;
using Entities.Concrete.Entities;
using System;
using System.Threading.Tasks;

namespace DataAccess.Abstract
{
    public interface IRatingDal : IEntityRepository<Rating>
    {
        Task<Rating> GetByAppointmentAndTargetAsync(Guid appointmentId, Guid targetId, Guid ratedFromId);
        Task<bool> ExistsAsync(Guid appointmentId, Guid targetId, Guid ratedFromId);

        /// <summary>
        /// Target için yapılan değerlendirmeleri CreatedAt DESC sıralar. `beforeUtc` ve `limit`
        /// sağlanırsa cursor pagination; aksi halde tüm liste döner.
        /// </summary>
        Task<List<Rating>> GetByTargetPagedAsync(Guid targetId, DateTime? beforeUtc, Guid? beforeId, int? limit);
    }
}
