using System;
using Entities.Abstract;
using Entities.Concrete.Enums;

namespace Entities.Concrete.Entities
{
    /// <summary>
    /// Append-only denetim kaydı: kim, ne zaman, hangi işlem, kaynak id'leri, sonuç.
    /// Güncelleme/silme uygulama katmanında yapılmamalıdır.
    /// </summary>
    public class AuditLog : IEntity
    {
        public Guid Id { get; set; }
        public DateTime OccurredAt { get; set; }
        public Guid? ActorUserId { get; set; }
        public AuditAction Action { get; set; }
        /// <summary>Birincil kaynak (örn. mesaj veya kullanıcı id).</summary>
        public Guid? ResourceId { get; set; }
        /// <summary>İkincil bağlam (örn. thread id).</summary>
        public Guid? RelatedResourceId { get; set; }
        public bool Success { get; set; }
        public string? FailureReason { get; set; }
        public string? ClientIp { get; set; }
    }
}
