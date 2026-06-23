using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using Entities.Concrete.Entities;

namespace DataAccess.Concrete
{
    public class EfSocialStoryReplyDal : EfEntityRepositoryBase<SocialStoryReply, DatabaseContext>, ISocialStoryReplyDal
    {
        public EfSocialStoryReplyDal(DatabaseContext context) : base(context)
        {
        }
    }
}
