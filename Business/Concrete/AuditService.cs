using System;
using System.Threading.Tasks;
using Business.Abstract;
using DataAccess.Abstract;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Business.Concrete
{
    public class AuditService(
        IAuditLogDal auditLogDal,
        IHttpContextAccessor httpContextAccessor,
        ILogger<AuditService> logger) : IAuditService
    {
        private const int MaxFailureReasonLength = 500;
        private const int MaxIpLength = 64;

        public async Task RecordAsync(
            AuditAction action,
            Guid? actorUserId,
            Guid? resourceId,
            Guid? relatedResourceId,
            bool success,
            string? failureReason = null)
        {
            try
            {
                if (failureReason != null && failureReason.Length > MaxFailureReasonLength)
                    failureReason = failureReason[..MaxFailureReasonLength];

                var ip = GetClientIp();

                var row = new AuditLog
                {
                    Id = Guid.NewGuid(),
                    OccurredAt = DateTime.UtcNow,
                    ActorUserId = actorUserId,
                    Action = action,
                    ResourceId = resourceId,
                    RelatedResourceId = relatedResourceId,
                    Success = success,
                    FailureReason = failureReason,
                    ClientIp = ip
                };

                await auditLogDal.Add(row);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Audit log yazılamadı: {Action}, Actor:{Actor}", action, actorUserId);
            }
        }

        private string? GetClientIp()
        {
            try
            {
                var ctx = httpContextAccessor.HttpContext;
                var ip = ctx?.Connection.RemoteIpAddress?.ToString();
                if (string.IsNullOrEmpty(ip)) return null;
                return ip.Length > MaxIpLength ? ip[..MaxIpLength] : ip;
            }
            catch
            {
                return null;
            }
        }
    }
}
