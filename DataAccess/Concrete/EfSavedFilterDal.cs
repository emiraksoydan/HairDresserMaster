using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using Entities.Concrete.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Concrete
{
    public class EfSavedFilterDal : EfEntityRepositoryBase<SavedFilter, DatabaseContext>, ISavedFilterDal
    {
        private readonly DatabaseContext _context;

        public EfSavedFilterDal(DatabaseContext context) : base(context)
        {
            _context = context;
        }

        public async Task<List<SavedFilter>> GetByUserIdAsync(Guid userId)
        {
            return await _context.SavedFilters
                .AsNoTracking()
                .Where(sf => sf.UserId == userId)
                .OrderByDescending(sf => sf.CreatedAt)
                .ToListAsync();
        }

        public async Task<int> CountByUserIdAsync(Guid userId)
        {
            return await _context.SavedFilters
                .CountAsync(sf => sf.UserId == userId);
        }
    }
}
