using Business.Abstract;
using Entities.Concrete.Dto;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Api.Controllers
{
    [Route("api/[controller]")]
    public class DiscoveryController : BaseApiController
    {
        private readonly IDiscoveryService _discoveryService;

        public DiscoveryController(IDiscoveryService discoveryService)
        {
            _discoveryService = discoveryService;
        }

        /// <summary>
        /// Müşteri keşfi: tek round-trip ile filtrelenmiş dükkan + serbest berber sayfaları (ayrı offset).
        /// </summary>
        [EnableRateLimiting("discover")]
        [HttpPost("filtered")]
        public async Task<IActionResult> GetFiltered(
            [FromBody] FilterRequestDto? filter,
            [FromQuery] int limit = 20,
            [FromQuery] int storeOffset = 0,
            [FromQuery] int freeBarberOffset = 0)
        {
            // Defensive guards: malformed/null body veya negatif paging değerleri
            // discovery endpoint'ini 500'e düşürmesin.
            filter ??= new FilterRequestDto();
            limit = Math.Clamp(limit, 1, 100);
            storeOffset = Math.Max(0, storeOffset);
            freeBarberOffset = Math.Max(0, freeBarberOffset);

            filter.CurrentUserId = CurrentUserId;
            return await HandleDataResultAsync(_discoveryService.GetFilteredDiscoveryAsync(
                filter, limit, storeOffset, freeBarberOffset));
        }
    }
}
