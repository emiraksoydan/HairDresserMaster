using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using Entities.Concrete.Entities;

namespace DataAccess.Concrete
{
    public class EfChatMessageUserDeletionDal : EfEntityRepositoryBase<ChatMessageUserDeletion, DatabaseContext>, IChatMessageUserDeletionDal
    {
        public EfChatMessageUserDeletionDal(DatabaseContext context) : base(context) { }
    }
}
