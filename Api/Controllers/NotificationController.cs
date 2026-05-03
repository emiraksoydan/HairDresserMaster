using Business.Abstract;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [Route("api/[controller]")]
    public class NotificationController : BaseApiController
    {
        private readonly INotificationService _svc;

        public NotificationController(INotificationService svc)
        {
            _svc = svc;
        }

        /// <summary>Tüm okunmamış bildirimleri tek istekte okundu yapar (read/{guid} ile çakışmaması için önce tanımlı).</summary>
        [HttpPost("read-all")]
        public async Task<IActionResult> MarkAllRead()
        {
            return await HandleUserDataOperation(userId => _svc.MarkAllReadAsync(userId));
        }

        [HttpPost("read/{id:guid}")]
        public async Task<IActionResult> MarkRead(Guid id)
        {
            return await HandleUserDataOperation(userId => _svc.MarkReadAsync(userId, id));
        }

        // Pagination: `before` = son yüklü bildirimin CreatedAt (UTC ISO), `limit` = sayfa boyutu.
        // `beforeId` = aynı timestamp'lı bildirimler için Id tie-breaker (opsiyonel).
        // Parametresiz çağrı eski davranışla uyumlu kalır (en yeni 30).
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] DateTime? before, [FromQuery] Guid? beforeId, [FromQuery] int? limit = 30)
        {
            var safeLimit = Math.Clamp(limit ?? 30, 1, 100);
            return await HandleUserDataOperation(userId => _svc.GetAllNotify(userId, before, beforeId, safeLimit));
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            return await HandleUserDataOperation(userId => _svc.DeleteAsync(userId, id));
        }

        [HttpDelete("all")]
        public async Task<IActionResult> DeleteAll()
        {
            return await HandleUserDataOperation(userId => _svc.DeleteAllAsync(userId));
        }
    }
}
