using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Concrete
{
    public class EfSocialCommentDal : EfEntityRepositoryBase<SocialComment, DatabaseContext>, ISocialCommentDal
    {
        private readonly DatabaseContext _context;

        public EfSocialCommentDal(DatabaseContext context) : base(context)
        {
            _context = context;
        }

        public async Task<List<SocialComment>> GetByPostAsync(
            Guid postId, Guid? parentCommentId, DateTime? beforeUtc, Guid? beforeId, int limit)
        {
            var query = _context.SocialComments
                .AsNoTracking()
                .Include(c => c.Profile)
                .Where(c => c.PostId == postId && c.Status == SocialContentStatus.Active);

            if (parentCommentId.HasValue)
                query = query.Where(c => c.ParentCommentId == parentCommentId.Value);
            else
                query = query.Where(c => c.ParentCommentId == null);

            if (beforeUtc.HasValue)
            {
                if (beforeId.HasValue)
                {
                    query = query.Where(c =>
                        c.CreatedAt < beforeUtc.Value ||
                        (c.CreatedAt == beforeUtc.Value && c.Id.CompareTo(beforeId.Value) < 0));
                }
                else
                {
                    query = query.Where(c => c.CreatedAt < beforeUtc.Value);
                }
            }

            if (parentCommentId.HasValue)
            {
                return await query
                    .OrderBy(c => c.CreatedAt)
                    .ThenBy(c => c.Id)
                    .Take(limit)
                    .ToListAsync();
            }

            return await query
                .OrderByDescending(c => c.CreatedAt)
                .ThenByDescending(c => c.Id)
                .Take(limit)
                .ToListAsync();
        }

        public async Task<Dictionary<Guid, int>> GetCommentCountsAsync(IReadOnlyList<Guid> postIds)
        {
            if (postIds.Count == 0) return new Dictionary<Guid, int>();

            return await _context.SocialComments
                .AsNoTracking()
                .Where(c => postIds.Contains(c.PostId) && c.Status == SocialContentStatus.Active)
                .GroupBy(c => c.PostId)
                .Select(g => new { PostId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.PostId, x => x.Count);
        }

        public async Task<Dictionary<Guid, int>> GetReplyCountsAsync(IReadOnlyList<Guid> parentCommentIds)
        {
            if (parentCommentIds.Count == 0) return new Dictionary<Guid, int>();

            return await _context.SocialComments
                .AsNoTracking()
                .Where(c =>
                    c.ParentCommentId != null &&
                    parentCommentIds.Contains(c.ParentCommentId.Value) &&
                    c.Status == SocialContentStatus.Active)
                .GroupBy(c => c.ParentCommentId!.Value)
                .Select(g => new { ParentId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ParentId, x => x.Count);
        }
    }
}
