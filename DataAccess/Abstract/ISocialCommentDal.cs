using Core.DataAccess;
using Entities.Concrete.Entities;

namespace DataAccess.Abstract
{
    public interface ISocialCommentDal : IEntityRepository<SocialComment>
    {
        Task<List<SocialComment>> GetByPostAsync(
            Guid postId, Guid? parentCommentId, DateTime? beforeUtc, Guid? beforeId, int limit);
        Task<Dictionary<Guid, int>> GetCommentCountsAsync(IReadOnlyList<Guid> postIds);
        Task<Dictionary<Guid, int>> GetReplyCountsAsync(IReadOnlyList<Guid> parentCommentIds);
    }
}
