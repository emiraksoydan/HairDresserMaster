using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;
using Entities.Concrete.Constants;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Concrete
{
    public class EfSocialAdminDal : ISocialAdminDal
    {
        private readonly DatabaseContext _context;

        public EfSocialAdminDal(DatabaseContext context)
        {
            _context = context;
        }

        public async Task<List<SocialPost>> GetPostsForAdminAsync(
            SocialContentStatus? status, SocialPostType? postType, string? search, int skip, int take)
        {
            var query = _context.SocialPosts
                .AsNoTracking()
                .Include(p => p.Profile)
                .AsQueryable();

            if (status.HasValue)
                query = query.Where(p => p.Status == status.Value);

            if (postType.HasValue)
                query = query.Where(p => p.Type == postType.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = $"%{search.Trim()}%";
                query = query.Where(p =>
                    EF.Functions.ILike(p.Profile.Username, term) ||
                    (p.Caption != null && EF.Functions.ILike(p.Caption, term)));
            }

            return await query
                .OrderByDescending(p => p.CreatedAt)
                .ThenByDescending(p => p.Id)
                .Skip(skip)
                .Take(take)
                .ToListAsync();
        }

        public async Task<List<SocialComment>> GetCommentsForAdminAsync(
            SocialContentStatus? status, string? search, int skip, int take)
        {
            var query = _context.SocialComments
                .AsNoTracking()
                .Include(c => c.Profile)
                .Include(c => c.Post)
                .AsQueryable();

            if (status.HasValue)
                query = query.Where(c => c.Status == status.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = $"%{search.Trim()}%";
                query = query.Where(c =>
                    EF.Functions.ILike(c.Profile.Username, term) ||
                    EF.Functions.ILike(c.Text, term) ||
                    (c.Post.Caption != null && EF.Functions.ILike(c.Post.Caption, term)));
            }

            return await query
                .OrderByDescending(c => c.CreatedAt)
                .ThenByDescending(c => c.Id)
                .Skip(skip)
                .Take(take)
                .ToListAsync();
        }

        public async Task<List<SocialStory>> GetStoriesForAdminAsync(
            SocialContentStatus? status, string? search, int skip, int take)
        {
            var query = _context.SocialStories
                .AsNoTracking()
                .Include(s => s.Profile)
                .AsQueryable();

            if (status.HasValue)
                query = query.Where(s => s.Status == status.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = $"%{search.Trim()}%";
                query = query.Where(s => EF.Functions.ILike(s.Profile.Username, term));
            }

            return await query
                .OrderByDescending(s => s.CreatedAt)
                .ThenByDescending(s => s.Id)
                .Skip(skip)
                .Take(take)
                .ToListAsync();
        }

        public async Task<List<SocialProfile>> GetProfilesForAdminAsync(
            SocialContentStatus? status, string? search, int skip, int take)
        {
            var query = _context.SocialProfiles.AsNoTracking().AsQueryable();

            if (status.HasValue)
                query = query.Where(p => p.Status == status.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = $"%{search.Trim()}%";
                query = query.Where(p =>
                    EF.Functions.ILike(p.Username, term) ||
                    (p.Bio != null && EF.Functions.ILike(p.Bio, term)));
            }

            return await query
                .OrderByDescending(p => p.CreatedAt)
                .ThenByDescending(p => p.Id)
                .Skip(skip)
                .Take(take)
                .ToListAsync();
        }

        public async Task<List<SocialStoryHighlight>> GetHighlightsForAdminAsync(
            SocialContentStatus? status, string? search, int skip, int take)
        {
            var query = _context.SocialStoryHighlights
                .AsNoTracking()
                .Include(h => h.Profile)
                .Include(h => h.Items)
                .AsQueryable();

            if (status.HasValue)
                query = query.Where(h => h.Status == status.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = $"%{search.Trim()}%";
                query = query.Where(h =>
                    EF.Functions.ILike(h.Profile.Username, term) ||
                    EF.Functions.ILike(h.Title, term));
            }

            return await query
                .OrderByDescending(h => h.CreatedAt)
                .ThenByDescending(h => h.Id)
                .Skip(skip)
                .Take(take)
                .ToListAsync();
        }

        public async Task SetProfileContentRemovedAsync(Guid profileId)
        {
            var now = DateTime.UtcNow;
            await _context.SocialPosts
                .Where(p => p.ProfileId == profileId && p.Status == SocialContentStatus.Active)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.Status, SocialContentStatus.Removed)
                    .SetProperty(p => p.UpdatedAt, now));

            await _context.SocialStories
                .Where(s => s.ProfileId == profileId && s.Status == SocialContentStatus.Active)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(st => st.Status, SocialContentStatus.Removed));

            await _context.SocialStoryHighlights
                .Where(h => h.ProfileId == profileId && h.Status == SocialContentStatus.Active)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(h => h.Status, SocialContentStatus.Removed)
                    .SetProperty(h => h.UpdatedAt, now));
        }

        public async Task RestoreProfileContentAsync(Guid profileId)
        {
            var now = DateTime.UtcNow;

            await _context.SocialPosts
                .Where(p =>
                    p.ProfileId == profileId &&
                    p.Status == SocialContentStatus.Removed &&
                    p.RemovedAt == null)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.Status, SocialContentStatus.Active)
                    .SetProperty(p => p.UpdatedAt, now));

            await _context.SocialStories
                .Where(st =>
                    st.ProfileId == profileId &&
                    st.Status == SocialContentStatus.Removed &&
                    st.RemovedAt == null)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(st => st.Status, SocialContentStatus.Active)
                    .SetProperty(st => st.RemovedAt, (DateTime?)null));

            await _context.SocialStories
                .Where(st =>
                    st.ProfileId == profileId &&
                    st.Status == SocialContentStatus.Active &&
                    st.ExpiresAt <= now)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(st => st.ExpiresAt, now.AddHours(SocialMediaLimits.StoryLifetimeHours)));

            await _context.SocialStoryHighlights
                .Where(h =>
                    h.ProfileId == profileId &&
                    h.Status == SocialContentStatus.Removed &&
                    h.RemovedAt == null)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(h => h.Status, SocialContentStatus.Active)
                    .SetProperty(h => h.RemovedAt, (DateTime?)null)
                    .SetProperty(h => h.UpdatedAt, now));
        }
    }
}
