using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Business.Abstract;
using Business.Resources;
using Core.Extensions;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities.Concrete.Dto;
using Entities.Concrete.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [Route("api/[controller]")]
    public class AdminController(
        IUserDal userDal,
        IAppointmentService appointmentService,
        IBarberStoreChairService barberStoreChairService,
        IBarberStoreService barberStoreService,
        IBlockedService blockedService,
        IChatService chatService,
        IRatingService ratingService,
        IFavoriteService favoriteService,
        ISavedFilterService savedFilterService,
        IUserService userService,
        IComplaintService complaintService,
        IRequestService requestService,
        IServiceOfferingService serviceOfferingService,
        IServicePackageService servicePackageService,
        IManuelBarberService manuelBarberService,
        IAdminUserService adminUserService,
        IAuditLogDal auditLogDal,
        IAuditService auditService,
        ICategoryService categoryService,
        IHelpGuideService helpGuideService,
        IOperationClaimService operationClaimService,
        IFreeBarberService freeBarberService,
        IAdminSearchService adminSearchService,
        IAdminMediaService adminMediaService,
        ISocialAdminService socialAdminService,
        IBarberStoreDal barberStoreDal,
        IFreeBarberDal freeBarberDal,
        IPresenceTracker presenceTracker,
        INotificationService notificationService) : BaseApiController
    {
        public record BanRequest(string? Reason);
        public record SetSubscriptionRequest(DateTime EndDate);
        public record CategoryUpsertRequest(string Name, Guid? ParentId);
        public record CategoryDeleteRequest(Guid? ReparentTo);

        // -------- Admin role guard + acting admin id helper --------
        private IActionResult AdminOnly()
        {
            if (!User.ClaimRoles().Contains("Admin"))
                return StatusCode(403, new ErrorResult(Messages.AdminOperationRequiresAdminRole));
            return null!;
        }

        private Guid CurrentAdminId()
        {
            var idStr = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? User?.FindFirst("identifier")?.Value
                     ?? User?.FindFirst("sub")?.Value;
            return Guid.TryParse(idStr, out var id) ? id : Guid.Empty;
        }

        // ============================================================
        // KULLANICI YÖNETİMİ (ban / unban / subscription)
        // ============================================================
        [HttpPost("users/{userId:guid}/ban")]
        public async Task<IActionResult> BanUser(Guid userId, [FromBody] BanRequest request)
        {
            var guard = AdminOnly();
            if (guard != null) return guard;

            var user = await userDal.Get(u => u.Id == userId);
            if (user == null) return BadRequest(new ErrorResult(Messages.UserNotFound));

            user.IsBanned = true;
            user.BanReason = request.Reason;
            user.UpdatedAt = DateTime.UtcNow;
            await userDal.Update(user);

            await socialAdminService.AdminRemoveAllProfilesForUserAsync(CurrentAdminId(), userId);
            await auditService.RecordAsync(AuditAction.AdminUserBanned, CurrentAdminId(), userId, null, true);
            return Ok(new SuccessResult(Messages.UserBannedSuccess));
        }

        [HttpPost("users/{userId:guid}/unban")]
        public async Task<IActionResult> UnbanUser(Guid userId)
        {
            var guard = AdminOnly();
            if (guard != null) return guard;

            var user = await userDal.Get(u => u.Id == userId);
            if (user == null) return BadRequest(new ErrorResult(Messages.UserNotFound));

            user.IsBanned = false;
            user.BanReason = null;
            user.UpdatedAt = DateTime.UtcNow;
            await userDal.Update(user);

            await socialAdminService.AdminRestoreAllProfilesForUserAsync(CurrentAdminId(), userId);
            await auditService.RecordAsync(AuditAction.AdminUserUnbanned, CurrentAdminId(), userId, null, true);
            return Ok(new SuccessResult(Messages.UserUnbannedSuccess));
        }

        [HttpPost("users/{userId:guid}/subscription")]
        public async Task<IActionResult> SetSubscription(Guid userId, [FromBody] SetSubscriptionRequest request)
        {
            var guard = AdminOnly();
            if (guard != null) return guard;

            var user = await userDal.Get(u => u.Id == userId);
            if (user == null) return BadRequest(new ErrorResult(Messages.UserNotFound));

            user.SubscriptionEndDate = request.EndDate.ToUniversalTime();
            user.UpdatedAt = DateTime.UtcNow;
            await userDal.Update(user);

            await auditService.RecordAsync(AuditAction.AdminSubscriptionUpdated, CurrentAdminId(), userId, null, true);
            return Ok(new SuccessResult(Messages.OperationSuccess));
        }

        // ============================================================
        // LİSTELEME (mevcut)
        // ============================================================
        [HttpGet("appointments")]
        public async Task<IActionResult> GetAllAppointments([FromQuery] AppointmentFilter filter = AppointmentFilter.All)
        {
            var guard = AdminOnly();
            if (guard != null) return guard;
            return await HandleDataResultAsync(appointmentService.GetAllAppointmentsForAdminAsync(filter));
        }

        public record AdminCancelAppointmentRequest(string? Reason);
        public record AdminSuspendRequest(bool Suspend, string? Reason);

        [HttpPatch("barberstores/{id:guid}/suspend")]
        public async Task<IActionResult> SuspendBarberStore(Guid id, [FromBody] AdminSuspendRequest req)
        {
            var guard = AdminOnly();
            if (guard != null) return guard;
            var result = await barberStoreService.AdminSetSuspendedAsync(CurrentAdminId(), id, req.Suspend, req.Reason);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpPatch("free-barbers/{id:guid}/suspend")]
        public async Task<IActionResult> SuspendFreeBarber(Guid id, [FromBody] AdminSuspendRequest req)
        {
            var guard = AdminOnly();
            if (guard != null) return guard;
            var result = await freeBarberService.AdminSetSuspendedAsync(CurrentAdminId(), id, req.Suspend, req.Reason);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpPatch("appointments/{id:guid}/cancel")]
        public async Task<IActionResult> AdminCancelAppointment(Guid id, [FromBody] AdminCancelAppointmentRequest? req)
        {
            var guard = AdminOnly();
            if (guard != null) return guard;
            var result = await appointmentService.AdminCancelAsync(CurrentAdminId(), id, req?.Reason);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpGet("chairs")]
        public async Task<IActionResult> GetAllChairs()
        {
            var guard = AdminOnly();
            if (guard != null) return guard;
            return await HandleDataResultAsync(barberStoreChairService.GetAllForAdminAsync());
        }

        [HttpGet("barberstores")]
        public async Task<IActionResult> GetAllBarberStores()
        {
            var guard = AdminOnly();
            if (guard != null) return guard;
            return await HandleDataResultAsync(barberStoreService.GetAllForAdminAsync());
        }

        [HttpGet("service-offerings")]
        public async Task<IActionResult> GetAllServiceOfferings()
        {
            var guard = AdminOnly();
            if (guard != null) return guard;
            return await HandleDataResultAsync(serviceOfferingService.GetAllForAdminAsync());
        }

        [HttpGet("service-packages")]
        public async Task<IActionResult> GetAllServicePackages()
        {
            var guard = AdminOnly();
            if (guard != null) return guard;
            return await HandleDataResultAsync(servicePackageService.GetAllForAdminAsync());
        }

        // ============================================================
        // ANLIK ÇEVRİMİÇİ KULLANICILAR (SignalR presence)
        // ============================================================
        [HttpGet("online-users/count")]
        public async Task<IActionResult> GetOnlineUsersCount()
        {
            var guard = AdminOnly();
            if (guard != null) return guard;

            var onlineIds = presenceTracker.GetOnlineUserIds();
            if (onlineIds.Count == 0)
                return Ok(new SuccessDataResult<OnlineUserCountDto>(new OnlineUserCountDto()));

            var idList = onlineIds.ToList();
            var users = await userDal.GetAll(u => idList.Contains(u.Id));

            var dto = new OnlineUserCountDto
            {
                Total = users.Count,
                Customers = users.Count(u => u.UserType == UserType.Customer),
                FreeBarbers = users.Count(u => u.UserType == UserType.FreeBarber),
                Stores = users.Count(u => u.UserType == UserType.BarberStore),
            };
            return Ok(new SuccessDataResult<OnlineUserCountDto>(dto));
        }

        // ============================================================
        // TOPLU BİLDİRİM / DUYURU (admin -> mobil kullanıcılar)
        // ============================================================
        public record BroadcastRequest(string Title, string? Body, int? UserType, List<Guid>? UserIds);

        [HttpPost("notifications/broadcast")]
        public async Task<IActionResult> BroadcastNotification([FromBody] BroadcastRequest request)
        {
            var guard = AdminOnly();
            if (guard != null) return guard;

            if (request == null || string.IsNullOrWhiteSpace(request.Title))
                return BadRequest(new ErrorResult("Başlık zorunludur."));

            // Hedef kullanıcıları belirle: userIds > userType > tümü. Banlı kullanıcılar her zaman hariç.
            List<Entities.Concrete.Entities.User> targets;
            if (request.UserIds != null && request.UserIds.Count > 0)
            {
                var ids = request.UserIds;
                targets = await userDal.GetAll(u => ids.Contains(u.Id) && !u.IsBanned);
            }
            else if (request.UserType.HasValue)
            {
                var ut = (UserType)request.UserType.Value;
                targets = await userDal.GetAll(u => u.UserType == ut && !u.IsBanned);
            }
            else
            {
                targets = await userDal.GetAll(u => !u.IsBanned);
            }

            var result = new BroadcastResultDto { Total = targets.Count };
            var payload = new { kind = "admin_announcement", title = request.Title, body = request.Body };

            foreach (var user in targets)
            {
                try
                {
                    var r = await notificationService.CreateAndPushAsync(
                        user.Id,
                        NotificationType.AdminAnnouncement,
                        null,
                        request.Title,
                        payload,
                        request.Body);

                    if (r != null && r.Success) result.Sent++;
                    else result.Failed++;
                }
                catch
                {
                    result.Failed++;
                }
            }

            await auditService.RecordAsync(AuditAction.AdminBroadcastSent, CurrentAdminId(), null, null, true);
            return Ok(new SuccessDataResult<BroadcastResultDto>(result));
        }

        [HttpGet("manuel-barbers")]
        public async Task<IActionResult> GetAllManuelBarbers()
        {
            var guard = AdminOnly();
            if (guard != null) return guard;
            return await HandleDataResultAsync(manuelBarberService.GetAllForAdminAsync());
        }

        [HttpGet("free-barbers")]
        public async Task<IActionResult> GetAllFreeBarbers()
        {
            var guard = AdminOnly();
            if (guard != null) return guard;
            return await HandleDataResultAsync(freeBarberService.GetAllForAdminAsync());
        }

        [HttpGet("barberstores/{storeId:guid}/earnings")]
        public async Task<IActionResult> GetStoreEarnings(
            Guid storeId,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            var guard = AdminOnly();
            if (guard != null) return guard;

            var end = (endDate ?? DateTime.UtcNow).Date;
            var start = (startDate ?? end.AddMonths(-1)).Date;
            if (start > end)
                return BadRequest(new ErrorResult("Başlangıç tarihi bitişten sonra olamaz."));

            var detail = await barberStoreDal.GetAdminEarningsDetailAsync(storeId, start, end);
            return Ok(new SuccessDataResult<AdminEarningsDetailDto>(detail));
        }

        [HttpGet("free-barbers/{freeBarberUserId:guid}/earnings")]
        public async Task<IActionResult> GetFreeBarberEarnings(
            Guid freeBarberUserId,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            var guard = AdminOnly();
            if (guard != null) return guard;

            var end = (endDate ?? DateTime.UtcNow).Date;
            var start = (startDate ?? end.AddMonths(-1)).Date;
            if (start > end)
                return BadRequest(new ErrorResult("Başlangıç tarihi bitişten sonra olamaz."));

            var detail = await freeBarberDal.GetAdminEarningsDetailAsync(freeBarberUserId, start, end);
            return Ok(new SuccessDataResult<AdminEarningsDetailDto>(detail));
        }

        [HttpGet("blocked")]
        public async Task<IActionResult> GetAllBlocked()
        {
            var guard = AdminOnly();
            if (guard != null) return guard;
            return await HandleDataResultAsync(blockedService.GetAllBlockedForAdminAsync());
        }

        [HttpGet("chat-threads")]
        public async Task<IActionResult> GetAllChatThreads()
        {
            var guard = AdminOnly();
            if (guard != null) return guard;
            return await HandleDataResultAsync(chatService.GetAllThreadsForAdminAsync());
        }

        [HttpGet("media-files")]
        public async Task<IActionResult> GetMediaFiles(
            [FromQuery] string? category = null,
            [FromQuery] string? search = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 24)
        {
            var guard = AdminOnly();
            if (guard != null) return guard;
            return await HandleDataResultAsync(adminMediaService.GetMediaFilesAsync(category, search, page, pageSize));
        }

        [HttpGet("media-files/stats")]
        public async Task<IActionResult> GetMediaStats()
        {
            var guard = AdminOnly();
            if (guard != null) return guard;
            return await HandleDataResultAsync(adminMediaService.GetMediaStatsAsync());
        }

        [HttpDelete("media-files/{id:guid}")]
        public async Task<IActionResult> DeleteMediaFile(Guid id, [FromQuery] string? category = null)
        {
            var guard = AdminOnly();
            if (guard != null) return guard;
            var result = await adminMediaService.DeleteMediaAsync(CurrentAdminId(), id, category);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpGet("ratings")]
        public async Task<IActionResult> GetAllRatings()
        {
            var guard = AdminOnly();
            if (guard != null) return guard;
            return await HandleDataResultAsync(ratingService.GetAllRatingsForAdminAsync());
        }

        [HttpGet("ratings/by-target/{targetId:guid}")]
        public async Task<IActionResult> GetRatingsByTarget(Guid targetId)
        {
            var guard = AdminOnly();
            if (guard != null) return guard;
            return await HandleDataResultAsync(ratingService.GetRatingsByTargetForAdminAsync(targetId));
        }

        [HttpDelete("ratings/{id:guid}")]
        public async Task<IActionResult> DeleteRating(Guid id)
        {
            var guard = AdminOnly();
            if (guard != null) return guard;
            var result = await ratingService.AdminDeleteRatingAsync(CurrentAdminId(), id);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpPost("ratings/{id:guid}/hide")]
        public async Task<IActionResult> HideRating(Guid id)
        {
            var guard = AdminOnly();
            if (guard != null) return guard;
            var result = await ratingService.AdminHideRatingAsync(CurrentAdminId(), id);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpPost("ratings/{id:guid}/unhide")]
        public async Task<IActionResult> UnhideRating(Guid id)
        {
            var guard = AdminOnly();
            if (guard != null) return guard;
            var result = await ratingService.AdminUnhideRatingAsync(CurrentAdminId(), id);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpGet("favorites")]
        public async Task<IActionResult> GetAllFavorites()
        {
            var guard = AdminOnly();
            if (guard != null) return guard;
            return await HandleDataResultAsync(favoriteService.GetAllFavoritesForAdminAsync());
        }

        [HttpGet("saved-filters")]
        public async Task<IActionResult> GetAllSavedFilters()
        {
            var guard = AdminOnly();
            if (guard != null) return guard;
            return await HandleDataResultAsync(savedFilterService.GetAllSavedFiltersForAdminAsync());
        }

        [HttpGet("users")]
        public async Task<IActionResult> GetAllUsers()
        {
            var guard = AdminOnly();
            if (guard != null) return guard;
            return await HandleDataResultAsync(userService.GetAllUsersForAdminAsync());
        }

        /// <summary>Admin panel varlık araması (kind ile tek tür veya tüm türler).</summary>
        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string? q, [FromQuery] int limit = 20, [FromQuery] string? kind = null)
        {
            var guard = AdminOnly();
            if (guard != null) return guard;
            return await HandleDataResultAsync(adminSearchService.SearchAsync(q ?? string.Empty, limit, kind));
        }

        [HttpGet("complaints")]
        public async Task<IActionResult> GetAllComplaints()
        {
            var guard = AdminOnly();
            if (guard != null) return guard;
            return await HandleDataResultAsync(complaintService.GetAllComplaintsForAdminAsync());
        }

        [HttpGet("requests")]
        public async Task<IActionResult> GetAllRequests()
        {
            var guard = AdminOnly();
            if (guard != null) return guard;
            return await HandleDataResultAsync(requestService.GetAllRequestsForAdminAsync());
        }

        public record ResolveComplaintRequest(bool IsResolved = true);
        public record MarkRequestProcessedRequest(bool IsProcessed);

        [HttpPatch("complaints/{id:guid}/resolve")]
        public async Task<IActionResult> ResolveComplaint(Guid id, [FromBody] ResolveComplaintRequest? req)
        {
            var guard = AdminOnly();
            if (guard != null) return guard;
            var result = await complaintService.ResolveComplaintAsync(CurrentAdminId(), id);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpPatch("requests/{id:guid}/process")]
        public async Task<IActionResult> MarkRequestProcessed(Guid id, [FromBody] MarkRequestProcessedRequest req)
        {
            var guard = AdminOnly();
            if (guard != null) return guard;
            var result = await requestService.MarkProcessedAsync(CurrentAdminId(), id, req.IsProcessed);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ============================================================
        // CHAT — Thread içindeki mesajlar (silinmiş dahil, decrypt edilmiş)
        // ============================================================
        [HttpGet("chat-threads/{threadId:guid}/messages")]
        public async Task<IActionResult> GetThreadMessages(Guid threadId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            var guard = AdminOnly();
            if (guard != null) return guard;

            var result = await chatService.GetThreadMessagesForAdminAsync(threadId, page, pageSize);
            if (result.Success)
                await auditService.RecordAsync(AuditAction.AdminChatThreadViewed, CurrentAdminId(), threadId, null, true);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ============================================================
        // AUDIT LOG — kim ne yapmış (filtreli, sayfalı)
        // ============================================================
        [HttpGet("audit-logs")]
        public async Task<IActionResult> GetAuditLogs(
            [FromQuery] AuditAction? action = null,
            [FromQuery] Guid? actorUserId = null,
            [FromQuery] Guid? resourceId = null,
            [FromQuery] DateTime? fromUtc = null,
            [FromQuery] DateTime? toUtc = null,
            [FromQuery] bool? success = null,
            [FromQuery] string? scope = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            var guard = AdminOnly();
            if (guard != null) return guard;

            if (page < 1 || pageSize < 1)
                return BadRequest(new ErrorResult(Messages.AuditLogPageInvalid));

            var filter = new AuditLogFilterDto
            {
                Action = action,
                ActorUserId = actorUserId,
                ResourceId = resourceId,
                FromUtc = fromUtc,
                ToUtc = toUtc,
                Success = success,
                Scope = scope,
                Page = page,
                PageSize = pageSize
            };

            var result = await auditLogDal.QueryPagedAsync(filter);
            return Ok(new SuccessDataResult<PagedResultDto<AuditLogItemDto>>(result));
        }

        // ============================================================
        // ADMIN YÖNETİMİ (sadece login olmuş admin başka admin ekleyebilir/silebilir)
        // ============================================================
        [HttpGet("admins")]
        public async Task<IActionResult> GetAllAdmins()
        {
            var guard = AdminOnly();
            if (guard != null) return guard;
            return await HandleDataResultAsync(adminUserService.GetAllAsync());
        }

        [HttpPost("admins")]
        public async Task<IActionResult> CreateAdmin([FromBody] AdminUserCreateDto dto)
        {
            var guard = AdminOnly();
            if (guard != null) return guard;
            var result = await adminUserService.CreateAsync(dto, CurrentAdminId());
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpPatch("admins/{adminId:guid}/active")]
        public async Task<IActionResult> SetAdminActive(Guid adminId, [FromBody] AdminUserSetActiveDto dto)
        {
            var guard = AdminOnly();
            if (guard != null) return guard;
            var result = await adminUserService.SetActiveAsync(adminId, dto.IsActive, CurrentAdminId());
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpGet("admins/me")]
        public async Task<IActionResult> GetMyProfile()
        {
            var guard = AdminOnly();
            if (guard != null) return guard;
            var result = await adminUserService.GetMeAsync(CurrentAdminId());
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpPut("admins/me")]
        public async Task<IActionResult> UpdateMyProfile([FromBody] AdminUserUpdateProfileDto dto)
        {
            var guard = AdminOnly();
            if (guard != null) return guard;
            var result = await adminUserService.UpdateProfileAsync(CurrentAdminId(), dto);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpPost("admins/me/change-password")]
        public async Task<IActionResult> ChangeMyPassword([FromBody] AdminUserChangePasswordDto dto)
        {
            var guard = AdminOnly();
            if (guard != null) return guard;
            var result = await adminUserService.ChangePasswordAsync(CurrentAdminId(), dto);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        public record AdminAvatarUploadForm(Microsoft.AspNetCore.Http.IFormFile File);

        [HttpPost("admins/me/avatar")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadMyAvatar([FromForm] AdminAvatarUploadForm form)
        {
            var guard = AdminOnly();
            if (guard != null) return guard;
            var result = await adminUserService.UploadAvatarAsync(CurrentAdminId(), form.File);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpDelete("admins/me/avatar")]
        public async Task<IActionResult> RemoveMyAvatar()
        {
            var guard = AdminOnly();
            if (guard != null) return guard;
            var result = await adminUserService.RemoveAvatarAsync(CurrentAdminId());
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpDelete("admins/{adminId:guid}")]
        public async Task<IActionResult> DeleteAdmin(Guid adminId)
        {
            var guard = AdminOnly();
            if (guard != null) return guard;
            var result = await adminUserService.DeleteAsync(adminId, CurrentAdminId());
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ============================================================
        // KATEGORİ YÖNETİMİ (hiyerarşik tree) — sonsuz derinlik desteği.
        // Listeleme için mevcut /api/categories/hierarchy zaten public/anon kullanılabiliyor;
        // burada admin tarafına özel CRUD endpoint'leri tutuyoruz.
        // ============================================================
        [HttpGet("categories/hierarchy")]
        public async Task<IActionResult> GetCategoryHierarchy()
        {
            var guard = AdminOnly();
            if (guard != null) return guard;
            return await HandleDataResultAsync(categoryService.GetCategoryHierarchyAsync());
        }

        [HttpPost("categories")]
        public async Task<IActionResult> CreateCategory([FromBody] CategoryUpsertRequest req)
        {
            var guard = AdminOnly();
            if (guard != null) return guard;
            if (string.IsNullOrWhiteSpace(req?.Name))
                return BadRequest(new ErrorResult(Messages.CategoryNameRequired));

            var entity = new Entities.Concrete.Entities.Category
            {
                Id = Guid.NewGuid(),
                Name = req.Name.Trim(),
                ParentId = req.ParentId,
            };
            var result = await categoryService.AddCategory(entity);
            if (result.Success)
                await auditService.RecordAsync(AuditAction.AdminCategoryCreated, CurrentAdminId(), entity.Id, req.ParentId, true);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpPut("categories/{id:guid}")]
        public async Task<IActionResult> UpdateCategory(Guid id, [FromBody] CategoryUpsertRequest req)
        {
            var guard = AdminOnly();
            if (guard != null) return guard;
            var result = await categoryService.UpdateCategory(id, req?.Name ?? string.Empty, req?.ParentId);
            if (result.Success)
                await auditService.RecordAsync(AuditAction.AdminCategoryUpdated, CurrentAdminId(), id, req?.ParentId, true);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpDelete("categories/{id:guid}")]
        public async Task<IActionResult> DeleteCategory(Guid id, [FromBody] CategoryDeleteRequest? req = null)
        {
            var guard = AdminOnly();
            if (guard != null) return guard;
            var result = await categoryService.DeleteCategoryAndReparent(id, req?.ReparentTo);
            if (result.Success)
                await auditService.RecordAsync(AuditAction.AdminCategoryDeleted, CurrentAdminId(), id, req?.ReparentTo, true);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ============================================================
        // HELP GUIDE (Yardım Rehberi) — admin CRUD
        // ============================================================
        public record HelpGuideSetActiveRequest(bool IsActive);

        [HttpGet("help-guides")]
        public async Task<IActionResult> GetAllHelpGuides([FromQuery] int? userType = null)
        {
            var guard = AdminOnly();
            if (guard != null) return guard;
            return await HandleDataResultAsync(helpGuideService.GetAllForAdminAsync(userType));
        }

        [HttpPost("help-guides")]
        public async Task<IActionResult> CreateHelpGuide([FromBody] HelpGuideCreateDto dto)
        {
            var guard = AdminOnly();
            if (guard != null) return guard;
            var result = await helpGuideService.CreateAsync(dto);
            if (result.Success && result.Data != null)
                await auditService.RecordAsync(AuditAction.AdminHelpGuideCreated, CurrentAdminId(), result.Data.Id, null, true);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpPut("help-guides/{id:guid}")]
        public async Task<IActionResult> UpdateHelpGuide(Guid id, [FromBody] HelpGuideUpdateDto dto)
        {
            var guard = AdminOnly();
            if (guard != null) return guard;
            var result = await helpGuideService.UpdateAsync(id, dto);
            if (result.Success)
                await auditService.RecordAsync(AuditAction.AdminHelpGuideUpdated, CurrentAdminId(), id, null, true);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpDelete("help-guides/{id:guid}")]
        public async Task<IActionResult> DeleteHelpGuide(Guid id)
        {
            var guard = AdminOnly();
            if (guard != null) return guard;
            var result = await helpGuideService.DeleteAsync(id);
            if (result.Success)
                await auditService.RecordAsync(AuditAction.AdminHelpGuideDeleted, CurrentAdminId(), id, null, true);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpPatch("help-guides/{id:guid}/active")]
        public async Task<IActionResult> SetHelpGuideActive(Guid id, [FromBody] HelpGuideSetActiveRequest req)
        {
            var guard = AdminOnly();
            if (guard != null) return guard;
            var result = await helpGuideService.SetActiveAsync(id, req.IsActive);
            if (result.Success)
                await auditService.RecordAsync(AuditAction.AdminHelpGuideActiveChanged, CurrentAdminId(), id, null, true);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ============================================================
        // SOSYAL MEDYA MODERASYONU
        // ============================================================
        [HttpGet("social/posts")]
        public async Task<IActionResult> GetSocialPosts(
            [FromQuery] SocialContentStatus? status,
            [FromQuery] SocialPostType? postType,
            [FromQuery] string? search,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25)
        {
            var guard = AdminOnly();
            if (guard != null) return guard;
            return await HandleDataResultAsync(
                socialAdminService.GetPostsForAdminAsync(status, postType, search, page, pageSize));
        }

        [HttpGet("social/comments")]
        public async Task<IActionResult> GetSocialComments(
            [FromQuery] SocialContentStatus? status,
            [FromQuery] string? search,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25)
        {
            var guard = AdminOnly();
            if (guard != null) return guard;
            return await HandleDataResultAsync(
                socialAdminService.GetCommentsForAdminAsync(status, search, page, pageSize));
        }

        [HttpDelete("social/comments/{id:guid}")]
        public async Task<IActionResult> RemoveSocialComment(Guid id)
        {
            var guard = AdminOnly();
            if (guard != null) return guard;
            var result = await socialAdminService.AdminRemoveCommentAsync(CurrentAdminId(), id);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpGet("social/stories")]
        public async Task<IActionResult> GetSocialStories(
            [FromQuery] SocialContentStatus? status,
            [FromQuery] string? search,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25)
        {
            var guard = AdminOnly();
            if (guard != null) return guard;
            return await HandleDataResultAsync(
                socialAdminService.GetStoriesForAdminAsync(status, search, page, pageSize));
        }

        [HttpGet("social/profiles")]
        public async Task<IActionResult> GetSocialProfiles(
            [FromQuery] SocialContentStatus? status,
            [FromQuery] string? search,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25)
        {
            var guard = AdminOnly();
            if (guard != null) return guard;
            return await HandleDataResultAsync(
                socialAdminService.GetProfilesForAdminAsync(status, search, page, pageSize));
        }

        [HttpDelete("social/posts/{id:guid}")]
        public async Task<IActionResult> RemoveSocialPost(Guid id)
        {
            var guard = AdminOnly();
            if (guard != null) return guard;
            var result = await socialAdminService.AdminRemovePostAsync(CurrentAdminId(), id);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpDelete("social/stories/{id:guid}")]
        public async Task<IActionResult> RemoveSocialStory(Guid id)
        {
            var guard = AdminOnly();
            if (guard != null) return guard;
            var result = await socialAdminService.AdminRemoveStoryAsync(CurrentAdminId(), id);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpDelete("social/profiles/{id:guid}")]
        public async Task<IActionResult> RemoveSocialProfile(Guid id)
        {
            var guard = AdminOnly();
            if (guard != null) return guard;
            var result = await socialAdminService.AdminRemoveProfileAsync(CurrentAdminId(), id);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpGet("social/highlights")]
        public async Task<IActionResult> GetSocialHighlights(
            [FromQuery] SocialContentStatus? status,
            [FromQuery] string? search,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25)
        {
            var guard = AdminOnly();
            if (guard != null) return guard;
            return await HandleDataResultAsync(
                socialAdminService.GetHighlightsForAdminAsync(status, search, page, pageSize));
        }

        [HttpDelete("social/highlights/{id:guid}")]
        public async Task<IActionResult> RemoveSocialHighlight(Guid id)
        {
            var guard = AdminOnly();
            if (guard != null) return guard;
            var result = await socialAdminService.AdminRemoveHighlightAsync(CurrentAdminId(), id);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ============================================================
        // OPERATION CLAIMS (sistem rolleri) — sadece okuma.
        // Pratikte sabit: Admin/Customer/FreeBarber/BarberStore.
        // ============================================================
        [HttpGet("operation-claims")]
        public async Task<IActionResult> GetAllOperationClaims()
        {
            var guard = AdminOnly();
            if (guard != null) return guard;
            return await HandleDataResultAsync(operationClaimService.GetAllOperationClaim());
        }
    }
}
