using Business.Abstract;
using Entities.Concrete.Dto;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [Route("api/[controller]")]
    public class SavedFilterController : BaseApiController
    {
        private readonly ISavedFilterService _savedFilterService;

        public SavedFilterController(ISavedFilterService savedFilterService)
        {
            _savedFilterService = savedFilterService;
        }

        [HttpGet]
        public async Task<IActionResult> GetMyFilters()
        {
            return await HandleUserDataOperation(userId => _savedFilterService.GetMyFiltersAsync(userId));
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] SavedFilterCreateDto dto)
        {
            return await HandleUserDataOperation(userId => _savedFilterService.CreateAsync(userId, dto));
        }

        [HttpPut]
        public async Task<IActionResult> Update([FromBody] SavedFilterUpdateDto dto)
        {
            return await HandleUserDataOperation(userId => _savedFilterService.UpdateAsync(userId, dto));
        }

        [HttpDelete("{filterId}")]
        public async Task<IActionResult> Delete(Guid filterId)
        {
            return await HandleUserDataOperation(userId => _savedFilterService.DeleteAsync(userId, filterId));
        }
    }
}
