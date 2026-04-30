using System;
using System.Linq;
using Business.Abstract;
using Core.Extensions;
using Entities.Concrete.Dto;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Api.Controllers
{
    [Route("api/[controller]")]
    public class BarberStoreController : BaseApiController
    {
        private readonly IBarberStoreService _storeService;

        public BarberStoreController(IBarberStoreService storeService)
        {
            _storeService = storeService;
        }

        [HttpPost("create-store")]
        public async Task<IActionResult> Add([FromBody] BarberStoreCreateDto dto)
        {
            var result = await _storeService.Add(dto, CurrentUserId);
            return HandleDataResult(result);
        }

        [HttpPut("update-store")]
        public async Task<IActionResult> Update([FromBody] BarberStoreUpdateDto dto)
        {
            return await HandleUpdateOperation(dto, _storeService.Update);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            return await HandleDeleteOperation(id, _storeService.DeleteAsync);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(Guid id)
        {
            return await HandleDataResultAsync(_storeService.GetByIdAsync(id));
        }

        [EnableRateLimiting("discover")]
        [HttpGet("nearby")]
        public async Task<IActionResult> GetNearby([FromQuery] double lat, [FromQuery] double lon, [FromQuery] double distance = 10.0, [FromQuery] int limit = 100)
        {
            var currentUserId = User.GetUserIdOrNull(); // Optional: giriş yapmamış kullanıcılar da görebilmeli
            return await HandleDataResultAsync(_storeService.GetNearbyStoresAsync(lat, lon, distance, currentUserId, limit));
        }

        [EnableRateLimiting("discover")]
        [HttpPost("filtered")]
        public async Task<IActionResult> GetFiltered([FromBody] FilterRequestDto filter, [FromQuery] int limit = 100, [FromQuery] int offset = 0)
        {
            filter.CurrentUserId = CurrentUserId; // Set current user for favorites
            return await HandleDataResultAsync(_storeService.GetFilteredStoresAsync(filter, limit, offset));
        }

        [HttpGet("mine")]
        public async Task<IActionResult> GetMine()
        {
            return await HandleUserDataOperation(userId => _storeService.GetByCurrentUserAsync(userId));
        }

        [HttpGet("get-store-for-users")]
        public async Task<IActionResult> GetStoreForUsers([FromQuery] Guid storeId)
        {
            return await HandleDataResultAsync(_storeService.GetBarberStoreForUsers(storeId));
        }

        [HttpGet("earnings")]
        public async Task<IActionResult> GetEarnings(
            [FromQuery] Guid storeId,
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate)
        {
            var start = startDate.HasValue ? DateTime.SpecifyKind(startDate.Value, DateTimeKind.Utc) : DateTime.UtcNow.AddMonths(-1);
            var end = endDate.HasValue ? DateTime.SpecifyKind(endDate.Value, DateTimeKind.Utc) : DateTime.UtcNow;
            return await HandleDataResultAsync(_storeService.GetEarningsAsync(storeId, CurrentUserId, start, end));
        }

        /// <summary>
        /// Virgülle ayrılmış mağaza kimlikleri: <c>?storeIds=guid1,guid2</c>
        /// </summary>
        [HttpGet("earnings-aggregated")]
        public async Task<IActionResult> GetEarningsAggregated(
            [FromQuery] string storeIds,
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate)
        {
            var start = startDate.HasValue ? DateTime.SpecifyKind(startDate.Value, DateTimeKind.Utc) : DateTime.UtcNow.AddMonths(-1);
            var end = endDate.HasValue ? DateTime.SpecifyKind(endDate.Value, DateTimeKind.Utc) : DateTime.UtcNow;

            if (string.IsNullOrWhiteSpace(storeIds))
                return await HandleDataResultAsync(_storeService.GetAggregatedEarningsAsync(Array.Empty<Guid>(), CurrentUserId, start, end));

            var parsed = storeIds
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => Guid.TryParse(s, out var g) ? g : (Guid?)null)
                .Where(g => g.HasValue)
                .Select(g => g!.Value)
                .Distinct()
                .ToList();

            if (parsed.Count == 0)
                return BadRequest(new { message = "Geçerli mağaza kimliği bulunamadı." });

            return await HandleDataResultAsync(_storeService.GetAggregatedEarningsAsync(parsed, CurrentUserId, start, end));
        }
    }
}
