using Business.Abstract;
using Entities.Concrete.Dto;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [Route("api/[controller]")]
    public class ServicePackageController : BaseApiController
    {
        private readonly IServicePackageService _servicePackageService;

        public ServicePackageController(IServicePackageService servicePackageService)
        {
            _servicePackageService = servicePackageService;
        }

        /// <summary>Yeni hizmet paketi ekler</summary>
        [HttpPost]
        public async Task<IActionResult> Add([FromBody] ServicePackageCreateDto dto)
        {
            return await HandleResultAsync(_servicePackageService.AddAsync(dto, CurrentUserId));
        }

        /// <summary>Var olan hizmet paketini günceller</summary>
        [HttpPut]
        public async Task<IActionResult> Update([FromBody] ServicePackageUpdateDto dto)
        {
            return await HandleResultAsync(_servicePackageService.UpdateAsync(dto, CurrentUserId));
        }

        /// <summary>Hizmet paketini siler</summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            return await HandleResultAsync(_servicePackageService.DeleteAsync(id, CurrentUserId));
        }

        /// <summary>Sahibine ait tüm hizmet paketlerini getirir</summary>
        [HttpGet("owner/{ownerId}")]
        public async Task<IActionResult> GetByOwner(Guid ownerId)
        {
            return await HandleDataResultAsync(_servicePackageService.GetAllByOwnerAsync(ownerId, CurrentUserId));
        }

        /// <summary>Randevuya ait paket snapshot'larını getirir</summary>
        [HttpGet("appointment/{appointmentId}")]
        public async Task<IActionResult> GetByAppointment(Guid appointmentId)
        {
            return await HandleDataResultAsync(_servicePackageService.GetPackagesByAppointmentAsync(appointmentId));
        }
    }
}
