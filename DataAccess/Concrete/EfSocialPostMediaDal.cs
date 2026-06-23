using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using Entities.Concrete.Entities;

namespace DataAccess.Concrete
{
    public class EfSocialPostMediaDal : EfEntityRepositoryBase<SocialPostMedia, DatabaseContext>, ISocialPostMediaDal
    {
        public EfSocialPostMediaDal(DatabaseContext context) : base(context)
        {
        }
    }
}
