using System.Threading.Tasks;
using Core.DataAccess;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;

namespace DataAccess.Abstract
{
    public interface IAuditLogDal : IEntityRepository<AuditLog>
    {
        /// <summary>
        /// Sayfalı + filtreli audit log listesi. Actor display name'i hem User (FirstName+LastName)
        /// hem de AdminUser (Email) tablolarından lookup ile doldurur.
        /// </summary>
        Task<PagedResultDto<AuditLogItemDto>> QueryPagedAsync(AuditLogFilterDto filter);
    }
}
