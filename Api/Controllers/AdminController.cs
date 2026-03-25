using Business.Abstract;
using Business.Resources;
using Core.Extensions;
using Core.Utilities.Results;
using DataAccess.Abstract;
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
        IManuelBarberService manuelBarberService) : BaseApiController
    {
        public record BanRequest(string? Reason);
        public record SetSubscriptionRequest(DateTime EndDate);

        private IActionResult AdminOnly()
        {
            if (!User.ClaimRoles().Contains("Admin"))
                return StatusCode(403, new ErrorResult("Bu işlem için Admin yetkisi gereklidir."));
            return null!;
        }

        /// <summary>
        /// Kullanıcıyı engelle. Admin operation claim'i olan kullanıcılar çağırabilir.
        /// </summary>
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

            return Ok(new SuccessResult(Messages.UserBannedSuccess));
        }

        /// <summary>
        /// Kullanıcı engelini kaldır.
        /// </summary>
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

            return Ok(new SuccessResult(Messages.UserUnbannedSuccess));
        }

        /// <summary>
        /// Kullanıcıya abonelik ekle / uzat.
        /// </summary>
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

            return Ok(new SuccessResult(Messages.OperationSuccess));
        }

        [HttpGet("appointments")]
        public async Task<IActionResult> GetAllAppointments([FromQuery] AppointmentFilter filter = AppointmentFilter.All)
        {
            var guard = AdminOnly();
            if (guard != null) return guard;

            return await HandleDataResultAsync(appointmentService.GetAllAppointmentsForAdminAsync(filter));
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

        [HttpGet("manuel-barbers")]
        public async Task<IActionResult> GetAllManuelBarbers()
        {
            var guard = AdminOnly();
            if (guard != null) return guard;

            return await HandleDataResultAsync(manuelBarberService.GetAllForAdminAsync());
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

        [HttpGet("ratings")]
        public async Task<IActionResult> GetAllRatings()
        {
            var guard = AdminOnly();
            if (guard != null) return guard;

            return await HandleDataResultAsync(ratingService.GetAllRatingsForAdminAsync());
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
    }
}
