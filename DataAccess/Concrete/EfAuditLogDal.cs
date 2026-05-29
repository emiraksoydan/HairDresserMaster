using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Concrete
{
    public class EfAuditLogDal(DatabaseContext context)
        : EfEntityRepositoryBase<AuditLog, DatabaseContext>(context), IAuditLogDal
    {
        private readonly DatabaseContext _context = context;

        public async Task<PagedResultDto<AuditLogItemDto>> QueryPagedAsync(AuditLogFilterDto filter)
        {
            var page = filter.Page < 1 ? 1 : filter.Page;
            var pageSize = filter.PageSize < 1 ? 50 : Math.Min(filter.PageSize, 500);

            IQueryable<AuditLog> q = _context.AuditLogs.AsNoTracking();

            if (filter.Action.HasValue) q = q.Where(x => x.Action == filter.Action.Value);
            if (filter.ActorUserId.HasValue) q = q.Where(x => x.ActorUserId == filter.ActorUserId.Value);
            if (filter.ResourceId.HasValue) q = q.Where(x => x.ResourceId == filter.ResourceId.Value);
            if (filter.FromUtc.HasValue) q = q.Where(x => x.OccurredAt >= filter.FromUtc.Value);
            if (filter.ToUtc.HasValue) q = q.Where(x => x.OccurredAt <= filter.ToUtc.Value);
            if (filter.Success.HasValue) q = q.Where(x => x.Success == filter.Success.Value);

            var scope = filter.Scope?.Trim().ToLowerInvariant();
            if (scope == "admin")
                q = q.Where(x => x.Action >= AuditAction.AdminUserBanned);
            else if (scope == "mobile")
                q = q.Where(x => x.Action < AuditAction.AdminUserBanned);

            var total = await q.CountAsync();

            var rows = await q
                .OrderByDescending(x => x.OccurredAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Actor display name lookup (User + AdminUser).
            var actorIds = rows
                .Where(x => x.ActorUserId.HasValue)
                .Select(x => x.ActorUserId!.Value)
                .Distinct()
                .ToList();

            var userMap = await _context.Users
                .AsNoTracking()
                .Where(u => actorIds.Contains(u.Id))
                .Select(u => new { u.Id, u.FirstName, u.LastName })
                .ToDictionaryAsync(u => u.Id, u => $"{u.FirstName} {u.LastName}".Trim());

            var adminMap = await _context.AdminUsers
                .AsNoTracking()
                .Where(a => actorIds.Contains(a.Id))
                .Select(a => new { a.Id, a.Email, a.FullName })
                .ToDictionaryAsync(a => a.Id, a => $"[Admin] {(string.IsNullOrWhiteSpace(a.FullName) ? a.Email : a.FullName)}");

            var items = rows.Select(x => new AuditLogItemDto
            {
                Id = x.Id,
                OccurredAt = x.OccurredAt,
                ActorUserId = x.ActorUserId,
                ActorDisplayName = x.ActorUserId.HasValue
                    ? adminMap.TryGetValue(x.ActorUserId.Value, out var adminName) ? adminName
                      : userMap.TryGetValue(x.ActorUserId.Value, out var userName) ? userName
                      : null
                    : null,
                Action = x.Action,
                ActionName = x.Action.ToString(),
                ResourceId = x.ResourceId,
                RelatedResourceId = x.RelatedResourceId,
                Success = x.Success,
                FailureReason = x.FailureReason,
                ClientIp = x.ClientIp
            }).ToList();

            return new PagedResultDto<AuditLogItemDto>
            {
                Items = items,
                Total = total,
                Page = page,
                PageSize = pageSize
            };
        }
    }
}
