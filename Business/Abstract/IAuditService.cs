using System;
using System.Threading.Tasks;
using Entities.Concrete.Enums;

namespace Business.Abstract
{
    public interface IAuditService
    {
        /// <summary>
        /// Denetim kaydı ekler. Veritabanı hatası iş akışını bozmaz; hata ILogger ile yazılır.
        /// </summary>
        Task RecordAsync(AuditAction action, Guid? actorUserId, Guid? resourceId, Guid? relatedResourceId, bool success, string? failureReason = null);
    }
}
