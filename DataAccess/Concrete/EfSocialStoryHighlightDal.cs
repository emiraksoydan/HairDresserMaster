using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Concrete
{
    public class EfSocialStoryHighlightDal
        : EfEntityRepositoryBase<SocialStoryHighlight, DatabaseContext>, ISocialStoryHighlightDal
    {
        private readonly DatabaseContext _context;

        public EfSocialStoryHighlightDal(DatabaseContext context) : base(context)
        {
            _context = context;
        }

        public async Task<List<SocialStoryHighlight>> GetByProfileIdAsync(Guid profileId)
        {
            return await _context.SocialStoryHighlights
                .AsNoTracking()
                .Where(h => h.ProfileId == profileId && h.Status == SocialContentStatus.Active)
                .OrderBy(h => h.SortOrder)
                .ThenByDescending(h => h.CreatedAt)
                .ToListAsync();
        }

        public async Task<SocialStoryHighlight?> GetWithItemsAsync(Guid highlightId)
        {
            return await _context.SocialStoryHighlights
                .AsNoTracking()
                .Include(h => h.Profile)
                .Include(h => h.Items
                    .Where(i => i.Status == SocialContentStatus.Active)
                    .OrderBy(i => i.SortOrder)
                    .ThenBy(i => i.CreatedAt))
                .FirstOrDefaultAsync(h => h.Id == highlightId && h.Status == SocialContentStatus.Active);
        }

        public async Task<List<SocialStoryHighlight>> GetByProfileAndStatusAsync(
            Guid profileId, SocialContentStatus status, DateTime? removedAfterUtc, int limit)
        {
            var query = _context.SocialStoryHighlights
                .AsNoTracking()
                .Where(h => h.ProfileId == profileId && h.Status == status);

            if (status == SocialContentStatus.Removed && removedAfterUtc.HasValue)
            {
                query = query.Where(h =>
                    (h.RemovedAt ?? h.UpdatedAt) >= removedAfterUtc.Value);
            }

            return await query
                .OrderByDescending(h => h.RemovedAt ?? h.UpdatedAt)
                .ThenByDescending(h => h.CreatedAt)
                .Take(limit)
                .ToListAsync();
        }

        public async Task<int> GetNextSortOrderAsync(Guid profileId)
        {
            var max = await _context.SocialStoryHighlights
                .Where(h => h.ProfileId == profileId && h.Status == SocialContentStatus.Active)
                .MaxAsync(h => (int?)h.SortOrder);
            return (max ?? -1) + 1;
        }

        public async Task<Dictionary<Guid, int>> GetItemCountsAsync(IReadOnlyList<Guid> highlightIds)
        {
            if (highlightIds.Count == 0) return new Dictionary<Guid, int>();

            return await _context.SocialStoryHighlightItems
                .AsNoTracking()
                .Where(i => highlightIds.Contains(i.HighlightId) && i.Status == SocialContentStatus.Active)
                .GroupBy(i => i.HighlightId)
                .Select(g => new { HighlightId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.HighlightId, x => x.Count);
        }
    }
}
