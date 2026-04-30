using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using Entities.Concrete.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Concrete
{
    public class EfNotificationDal : EfEntityRepositoryBase<Notification, DatabaseContext>, INotificationDal
    {
        private readonly DatabaseContext _context;
        public EfNotificationDal(DatabaseContext context) : base(context)
        {
            _context = context;
        }

        public async Task<List<Notification>> GetByUserPagedAsync(Guid userId, DateTime? beforeUtc, Guid? beforeId, int limit)
        {
            // Keyset (composite cursor) — tie-breaker:
            //   WHERE CreatedAt < @ts OR (CreatedAt == @ts AND Id < @id)
            //   ORDER BY CreatedAt DESC, Id DESC
            // `beforeId` yoksa geriye dönük uyumluluk: sadece timestamp bazlı (eski tie
            // gürültüsü kalır ama var olan client'lar kırılmaz).
            var query = _context.Notifications
                .AsNoTracking()
                .Where(n => n.UserId == userId);

            if (beforeUtc.HasValue)
            {
                if (beforeId.HasValue)
                {
                    var cTs = beforeUtc.Value;
                    var cId = beforeId.Value;
                    query = query.Where(n => n.CreatedAt < cTs
                                          || (n.CreatedAt == cTs && n.Id.CompareTo(cId) < 0));
                }
                else
                {
                    query = query.Where(n => n.CreatedAt < beforeUtc.Value);
                }
            }

            return await query
                .OrderByDescending(n => n.CreatedAt)
                .ThenByDescending(n => n.Id)
                .Take(limit)
                .ToListAsync();
        }
    }
}
