using Business.Abstract;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.ComponentModel.DataAnnotations;

namespace Api.Controllers
{
    [Route("api/[controller]")]
    public class ChatController : BaseApiController
    {
        private readonly IChatService _chatService;

        public ChatController(IChatService chatService)
        {
            _chatService = chatService;
        }

        [EnableRateLimiting("messaging")]
        [HttpPost("{appointmentId:guid}/message")]
        public async Task<IActionResult> Send(Guid appointmentId, [FromBody] SendMessageRequest req)
        {
            return await HandleUserDataOperation(userId => _chatService.SendMessageAsync(userId, appointmentId, req.Text, req.ReplyToMessageId));
        }

        [HttpPost("{appointmentId:guid}/read")]
        public async Task<IActionResult> Read(Guid appointmentId)
        {
            // Geriye dönük uyumluluk için AppointmentId ile okundu işaretleme
            return await HandleUserDataOperation(userId => _chatService.MarkThreadReadByAppointmentAsync(userId, appointmentId));
        }

        [EnableRateLimiting("messaging")]
        [HttpPost("thread/{threadId:guid}/message")]
        public async Task<IActionResult> SendToThread(Guid threadId, [FromBody] SendMessageRequest req)
        {
            return await HandleUserDataOperation(userId => _chatService.SendFavoriteMessageAsync(userId, threadId, req.Text, req.ReplyToMessageId));
        }

        [EnableRateLimiting("messaging")]
        [HttpPost("thread/{threadId:guid}/media")]
        public async Task<IActionResult> SendMediaToThread(Guid threadId, [FromBody] SendMediaRequest req)
        {
            return await HandleUserDataOperation(userId =>
                _chatService.SendMediaMessageAsync(userId, threadId, req.MessageType, req.MediaUrl, req.ReplyToMessageId, req.FileName));
        }

        [EnableRateLimiting("messaging-delete")]
        [HttpDelete("message/{messageId:guid}")]
        public async Task<IActionResult> DeleteMessage(Guid messageId)
        {
            return await HandleUserOperation(userId => _chatService.DeleteMessageAsync(userId, messageId));
        }

        [EnableRateLimiting("messaging-delete")]
        [HttpPatch("message/{messageId:guid}")]
        public async Task<IActionResult> EditMessage(Guid messageId, [FromBody] EditMessageRequest req)
        {
            return await HandleUserOperation(userId => _chatService.EditMessageAsync(userId, messageId, req.Text));
        }

        [EnableRateLimiting("messaging-delete")]
        [HttpDelete("thread/{threadId:guid}")]
        public async Task<IActionResult> DeleteThread(Guid threadId)
        {
            return await HandleUserOperation(userId => _chatService.DeleteThreadForUserAsync(userId, threadId));
        }

        [HttpPost("thread/{threadId:guid}/read")]
        public async Task<IActionResult> ReadThread(Guid threadId)
        {
            return await HandleUserDataOperation(userId => _chatService.MarkThreadReadAsync(userId, threadId));
        }

        // Pagination opsiyonel. Parametresiz çağrılarda (eski client'lar) eski davranış korunur.
        // `before` = son yüklü thread'in LastMessageAt (UTC ISO), `limit` = sayfa boyutu.
        // `beforeId` = aynı timestamp'lı thread'ler için ThreadId tie-breaker (opsiyonel).
        [HttpGet("threads")]
        public async Task<IActionResult> Threads([FromQuery] DateTime? before, [FromQuery] Guid? beforeId, [FromQuery] int? limit)
        {
            int? safeLimit = limit.HasValue ? Math.Clamp(limit.Value, 1, 100) : (int?)null;
            var result = await _chatService.GetThreadsAsync(CurrentUserId, before, beforeId, safeLimit);
            return Ok(result);
        }

        // Pagination: `limit` = sayfa boyutu (default 30, clamp 1..100).
        // `before` = en eski yüklü mesajın CreatedAt'i (UTC ISO string). Yoksa "en yeni sayfa" döner.
        // `beforeId` = aynı timestamp'lı mesajlar için MessageId tie-breaker (opsiyonel).
        // Frontend infinite scroll: her yukarı scroll'da son yüklenen dizinin en eski mesajının (CreatedAt, messageId)'ını yollar.
        [HttpGet("{appointmentId:guid}/messages")]
        public async Task<IActionResult> Messages(Guid appointmentId, [FromQuery] DateTime? before, [FromQuery] Guid? beforeId, [FromQuery] int? limit = 30)
        {
            var safeLimit = Math.Clamp(limit ?? 30, 1, 100);
            return await HandleUserDataOperation(userId => _chatService.GetMessagesAsync(userId, appointmentId, before, beforeId, safeLimit));
        }

        [HttpGet("thread/{threadId:guid}/messages")]
        public async Task<IActionResult> ThreadMessages(Guid threadId, [FromQuery] DateTime? before, [FromQuery] Guid? beforeId, [FromQuery] int? limit = 30)
        {
            var safeLimit = Math.Clamp(limit ?? 30, 1, 100);
            return await HandleUserDataOperation(userId => _chatService.GetMessagesByThreadAsync(userId, threadId, before, beforeId, safeLimit));
        }

        [EnableRateLimiting("messaging-typing")]
        [HttpPost("thread/{threadId:guid}/typing")]
        public async Task<IActionResult> NotifyTyping(Guid threadId, [FromBody] TypingRequest req)
        {
            return await HandleUserDataOperation(userId => _chatService.NotifyTypingAsync(userId, threadId, req.IsTyping));
        }
    }

    public class SendMessageRequest
    {
        [Required]
        [MinLength(1)]
        public string Text { get; set; } = "";
        public Guid? ReplyToMessageId { get; set; }
    }

    public class SendMediaRequest
    {
        /// <summary>1=Image, 2=Location, 3=File, 4=Audio</summary>
        [Required]
        public int MessageType { get; set; }
        [Required]
        public string MediaUrl { get; set; } = "";
        public Guid? ReplyToMessageId { get; set; }
        /// <summary>Optional: original filename for File type messages</summary>
        public string? FileName { get; set; }
    }

    public class EditMessageRequest
    {
        [Required]
        [MinLength(1)]
        [MaxLength(500)]
        public string Text { get; set; } = "";
    }

    public class TypingRequest
    {
        [Required]
        public bool IsTyping { get; set; }
    }
}
