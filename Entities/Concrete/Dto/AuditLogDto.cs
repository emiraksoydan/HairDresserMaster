using System;
using Entities.Concrete.Enums;

namespace Entities.Concrete.Dto
{
    /// <summary>Audit log filtre parametreleri (sayfalı sorgu için).</summary>
    public class AuditLogFilterDto
    {
        public AuditAction? Action { get; set; }
        public Guid? ActorUserId { get; set; }
        public Guid? ResourceId { get; set; }
        public DateTime? FromUtc { get; set; }
        public DateTime? ToUtc { get; set; }
        public bool? Success { get; set; }
        /// <summary>mobile | admin — mobil uygulama (&lt;100) veya admin panel (≥100) kayıtları.</summary>
        public string? Scope { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }

    /// <summary>Tek bir audit log kaydı (admin görünümü için).</summary>
    public class AuditLogItemDto
    {
        public Guid Id { get; set; }
        public DateTime OccurredAt { get; set; }
        public Guid? ActorUserId { get; set; }
        public string? ActorDisplayName { get; set; }
        public AuditAction Action { get; set; }
        public string ActionName { get; set; } = string.Empty;
        public Guid? ResourceId { get; set; }
        public Guid? RelatedResourceId { get; set; }
        public bool Success { get; set; }
        public string? FailureReason { get; set; }
        public string? ClientIp { get; set; }
    }
}
