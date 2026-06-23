using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using Entities.Concrete.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Concrete
{
    public class EfSocialStoryViewDal : EfEntityRepositoryBase<SocialStoryView, DatabaseContext>, ISocialStoryViewDal
    {
        private readonly DatabaseContext _context;

        public EfSocialStoryViewDal(DatabaseContext context) : base(context)
        {
            _context = context;
        }

        public async Task<bool> TryAddViewAsync(Guid storyId, Guid profileId)
        {
            var exists = await _context.SocialStoryViews
                .AsNoTracking()
                .AnyAsync(v => v.StoryId == storyId && v.ProfileId == profileId);
            if (exists) return false;

            try
            {
                await _context.SocialStoryViews.AddAsync(new SocialStoryView
                {
                    Id = Guid.NewGuid(),
                    StoryId = storyId,
                    ProfileId = profileId,
                    ViewedAt = DateTime.UtcNow,
                });
                await _context.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateException)
            {
                return false;
            }
        }

        public Task<int> GetViewCountAsync(Guid storyId) =>
            _context.SocialStoryViews.AsNoTracking().CountAsync(v => v.StoryId == storyId);

        public Task<List<SocialStoryView>> GetViewersAsync(
            Guid storyId,
            DateTime? beforeUtc,
            Guid? beforeId,
            int limit)
        {
            var query = _context.SocialStoryViews
                .AsNoTracking()
                .Include(v => v.Profile)
                .Where(v => v.StoryId == storyId);

            if (beforeUtc.HasValue)
            {
                if (beforeId.HasValue)
                {
                    var cTs = beforeUtc.Value;
                    var cId = beforeId.Value;
                    query = query.Where(v =>
                        v.ViewedAt < cTs || (v.ViewedAt == cTs && v.Id.CompareTo(cId) < 0));
                }
                else
                {
                    query = query.Where(v => v.ViewedAt < beforeUtc.Value);
                }
            }

            return query
                .OrderByDescending(v => v.ViewedAt)
                .ThenByDescending(v => v.Id)
                .Take(limit)
                .ToListAsync();
        }
    }
}
