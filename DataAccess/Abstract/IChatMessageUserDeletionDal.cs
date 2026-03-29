using Core.DataAccess;
using Entities.Concrete.Entities;

namespace DataAccess.Abstract
{
    public interface IChatMessageUserDeletionDal : IEntityRepository<ChatMessageUserDeletion>
    {
    }
}
