using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using Entities.Concrete.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DataAccess.Concrete
{
    public class EfRatingDal : EfEntityRepositoryBase<Rating, DatabaseContext>, IRatingDal
    {
        private readonly DatabaseContext _context;

        public EfRatingDal(DatabaseContext context) : base(context)
        {
            _context = context;
        }

        public async Task<Rating> GetByAppointmentAndTargetAsync(Guid appointmentId, Guid targetId, Guid ratedFromId)
        {
            return await _context.Ratings
                .FirstOrDefaultAsync(r => 
                    r.AppointmentId == appointmentId && 
                    r.TargetId == targetId && 
                    r.RatedFromId == ratedFromId);
        }

        public async Task<bool> ExistsAsync(Guid appointmentId, Guid targetId, Guid ratedFromId)
        {
            return await _context.Ratings
                .AnyAsync(r => 
                    r.AppointmentId == appointmentId && 
                    r.TargetId == targetId && 
                    r.RatedFromId == ratedFromId);
        }

        public async Task<List<Rating>> GetByTargetPagedAsync(Guid targetId, DateTime? beforeUtc, Guid? beforeId, int? limit)
        {
            // Keyset cursor tie-breaker: bkz. EfNotificationDal.GetByUserPagedAsync notu.
            var query = _context.Ratings.AsNoTracking()
                .Where(r => r.TargetId == targetId);

            if (beforeUtc.HasValue)
            {
                if (beforeId.HasValue)
                {
                    var cTs = beforeUtc.Value;
                    var cId = beforeId.Value;
                    query = query.Where(r => r.CreatedAt < cTs
                                          || (r.CreatedAt == cTs && r.Id.CompareTo(cId) < 0));
                }
                else
                {
                    query = query.Where(r => r.CreatedAt < beforeUtc.Value);
                }
            }

            var ordered = query
                .OrderByDescending(r => r.CreatedAt)
                .ThenByDescending(r => r.Id);

            return limit.HasValue
                ? await ordered.Take(limit.Value).ToListAsync()
                : await ordered.ToListAsync();
        }
    }
}
