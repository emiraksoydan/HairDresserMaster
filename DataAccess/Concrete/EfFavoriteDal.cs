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
    public class EfFavoriteDal : EfEntityRepositoryBase<Favorite, DatabaseContext>, IFavoriteDal
    {
        private readonly DatabaseContext _context;

        public EfFavoriteDal(DatabaseContext context) : base(context)
        {
            _context = context;
        }

        public async Task<Favorite> GetByUsersAsync(Guid favoritedFromId, Guid favoritedToId)
        {
            // Aktif veya pasif fark etmeksizin favoriyi getir (toggle için)
            return await _context.Favorites
                .FirstOrDefaultAsync(f => 
                    f.FavoritedFromId == favoritedFromId && 
                    f.FavoritedToId == favoritedToId);
        }

        public async Task<bool> ExistsAsync(Guid favoritedFromId, Guid favoritedToId)
        {
            // Sadece aktif favorileri kontrol et
            return await _context.Favorites
                .AnyAsync(f => 
                    f.FavoritedFromId == favoritedFromId && 
                    f.FavoritedToId == favoritedToId &&
                    f.IsActive);
        }

        public async Task<List<Favorite>> GetMyActiveFavoritesPagedAsync(Guid userId, DateTime? beforeUtc, Guid? beforeId, int? limit)
        {
            // Keyset cursor tie-breaker: bkz. EfNotificationDal.GetByUserPagedAsync notu.
            var query = _context.Favorites.AsNoTracking()
                .Where(f => f.FavoritedFromId == userId && f.IsActive);

            if (beforeUtc.HasValue)
            {
                if (beforeId.HasValue)
                {
                    var cTs = beforeUtc.Value;
                    var cId = beforeId.Value;
                    query = query.Where(f => f.CreatedAt < cTs
                                          || (f.CreatedAt == cTs && f.Id.CompareTo(cId) < 0));
                }
                else
                {
                    query = query.Where(f => f.CreatedAt < beforeUtc.Value);
                }
            }

            var ordered = query
                .OrderByDescending(f => f.CreatedAt)
                .ThenByDescending(f => f.Id);

            return limit.HasValue
                ? await ordered.Take(limit.Value).ToListAsync()
                : await ordered.ToListAsync();
        }
    }
}
