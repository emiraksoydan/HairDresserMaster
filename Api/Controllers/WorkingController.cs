using Business.Abstract;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [Route("api/[controller]")]
    public class WorkingController : BaseApiController
    {
        private readonly IWorkingHourService _workingHourService;

        public WorkingController(IWorkingHourService workingHourService)
        {
            _workingHourService = workingHourService;
        }

        /// <summary>
        /// Dükkan (OwnerId = storeId) çalışma saatleri — rezervasyon ekranında kullanılır.
        /// </summary>
        [HttpGet("{targetId}")]
        public async Task<IActionResult> Get(Guid targetId)
        {
            return await HandleDataResultAsync(_workingHourService.GetByTargetAsync(targetId));
        }
    }
}
