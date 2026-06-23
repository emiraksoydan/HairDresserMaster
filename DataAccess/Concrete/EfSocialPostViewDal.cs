using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using Entities.Concrete.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Concrete
{
    public class EfSocialPostViewDal : EfEntityRepositoryBase<SocialPostView, DatabaseContext>, ISocialPostViewDal
    {
        private readonly DatabaseContext _context;

        public EfSocialPostViewDal(DatabaseContext context) : base(context)
        {
            _context = context;
        }

        public async Task<bool> TryAddViewAsync(Guid postId, Guid profileId)
        {
            var exists = await _context.SocialPostViews
                .AsNoTracking()
                .AnyAsync(v => v.PostId == postId && v.ProfileId == profileId);
            if (exists) return false;

            try
            {
                await _context.SocialPostViews.AddAsync(new SocialPostView
                {
                    Id = Guid.NewGuid(),
                    PostId = postId,
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
    }
}
