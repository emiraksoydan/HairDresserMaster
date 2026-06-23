using Core.Utilities.Results;
using Entities.Concrete.Constants;
using Entities.Concrete.Dto;
using Microsoft.AspNetCore.Mvc;
using Business.Helpers;

namespace Api.Controllers
{
    [Route("api/social/config")]
    public class SocialConfigController : BaseApiController
    {
        private readonly SocialSubscriptionGuard _socialSubscriptionGuard;

        public SocialConfigController(SocialSubscriptionGuard socialSubscriptionGuard)
        {
            _socialSubscriptionGuard = socialSubscriptionGuard;
        }

        [HttpGet("limits")]
        public IActionResult GetLimits()
        {
            return HandleDataResult(new SuccessDataResult<SocialLimitsDto>(SocialMediaLimits.ToDto()));
        }

        [HttpGet("usage")]
        public async Task<IActionResult> GetUsage()
        {
            return await HandleUserDataOperation<SocialFreeTierUsageDto>(async userId =>
            {
                var usage = await _socialSubscriptionGuard.GetFreeTierUsageAsync(userId);
                return new SuccessDataResult<SocialFreeTierUsageDto>(usage);
            });
        }
    }
}
