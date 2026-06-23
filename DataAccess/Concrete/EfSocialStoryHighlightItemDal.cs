using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Concrete
{
    public class EfSocialStoryHighlightItemDal
        : EfEntityRepositoryBase<SocialStoryHighlightItem, DatabaseContext>, ISocialStoryHighlightItemDal
    {
        private readonly DatabaseContext _context;

        public EfSocialStoryHighlightItemDal(DatabaseContext context) : base(context)
        {
            _context = context;
        }

        public async Task<List<SocialStoryHighlightItem>> GetRemovedByProfileIdAsync(
            Guid profileId, DateTime? removedAfterUtc, int limit)
        {
            var query = _context.SocialStoryHighlightItems
                .AsNoTracking()
                .Include(i => i.Highlight)
                .Where(i =>
                    i.Status == SocialContentStatus.Removed &&
                    i.Highlight.ProfileId == profileId &&
                    i.Highlight.Status == SocialContentStatus.Active);

            if (removedAfterUtc.HasValue)
            {
                query = query.Where(i =>
                    (i.RemovedAt ?? i.CreatedAt) >= removedAfterUtc.Value);
            }

            return await query
                .OrderByDescending(i => i.RemovedAt ?? i.CreatedAt)
                .Take(limit)
                .ToListAsync();
        }
    }
}
