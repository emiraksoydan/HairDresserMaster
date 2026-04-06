using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using Entities.Concrete.Entities;

namespace DataAccess.Concrete
{
    public class EfAuditLogDal(DatabaseContext context) : EfEntityRepositoryBase<AuditLog, DatabaseContext>(context), IAuditLogDal
    {
    }
}
