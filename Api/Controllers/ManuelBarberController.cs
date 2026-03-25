using Business.Abstract;
using Entities.Concrete.Dto;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [Route("api/[controller]")]
    public class ManuelBarberController : BaseApiController
    {
        private readonly IManuelBarberService _manuelBarberService;

        public ManuelBarberController(IManuelBarberService manuelBarberService)
        {
            _manuelBarberService = manuelBarberService;
        }

        [HttpPost]
        public async Task<IActionResult> Add([FromBody] ManuelBarberCreateDto dto)
        {
            return await HandleCreateOperation(dto, _manuelBarberService.AddAsync);
        }

        [HttpPut]
        public async Task<IActionResult> Update([FromBody] ManuelBarberUpdateDto dto)
        {
            return await HandleUpdateOperation(dto, _manuelBarberService.UpdateAsync);
        }

        [HttpGet("store/{storeId:guid}")]
        public async Task<IActionResult> GetByStore(Guid storeId)
        {
            var result = await _manuelBarberService.GetAllByStoreAsync(storeId, CurrentUserId);
            return HandleDataResult(result);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            return await HandleDeleteOperation(id, _manuelBarberService.DeleteAsync);
        }
    }
}
