using Business.Abstract;
using Entities.Concrete.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.ComponentModel.DataAnnotations;

namespace Api.Controllers
{
    [Route("api/[controller]")]
    public class AIController : BaseApiController
    {
        private readonly IAIAssistantService _aiService;
        private readonly ILogger<AIController> _logger;

        public AIController(IAIAssistantService aiService, ILogger<AIController> logger)
        {
            _aiService = aiService;
            _logger = logger;
        }

        /// <summary>
        /// Kullanıcının doğal dil mesajını AI randevu asistanına gönderir.
        /// Sesle gönderim için önce istemci tarafında Whisper ile transkripsiyon yapılır,
        /// ardından transkript bu endpoint'e gönderilir.
        /// </summary>
        [HttpPost("assistant")]
        public async Task<IActionResult> Assistant([FromBody] AIAssistantRequestDto req)
        {
            if (string.IsNullOrWhiteSpace(req.Message))
                return BadRequest(new { success = false, message = "Mesaj boş olamaz." });

            var result = await _aiService.ProcessRequestAsync(
                CurrentUserId,
                req.Message,
                req.Language ?? "tr",
                req.Latitude,
                req.Longitude);

            // Gemini ücretsiz (free) tier günlük/dakikalık kotası — Google 429 / RESOURCE_EXHAUSTED / quota.
            if (!result.Success && string.Equals(result.Message, "ai_rate_limit", StringComparison.Ordinal))
            {
                _logger.LogWarning(
                    "[AIController.Assistant] Gemini free-tier quota or rate limit exceeded. UserId={UserId}, ErrorCode=ai_rate_limit",
                    CurrentUserId);
                return StatusCode(StatusCodes.Status429TooManyRequests, result);
            }

            return HandleDataResult(result);
        }

        /// <summary>
        /// Ses dosyasını (m4a/mp3/wav) OpenAI Whisper ile metne dönüştürür.
        /// API key backend'de tutulur; frontend sadece JWT ile çağırır.
        /// </summary>
        [HttpPost("transcribe")]
        public async Task<IActionResult> Transcribe(IFormFile file, [FromQuery] string? language = null)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { success = false, message = "Ses dosyası boş." });

            try
            {
                using var rawStream = file.OpenReadStream();
                using var memStream = new MemoryStream();
                await rawStream.CopyToAsync(memStream);
                memStream.Position = 0;
                var result = await _aiService.TranscribeAudioAsync(memStream, file.FileName, file.ContentType, language);

                // Kontrol noktası: DataResult<string>'in Data alanının gerçekten dolu mu geldiği görünür olsun.
                // UserId + dosya bilgisi eklendi ki hangi kullanıcının hangi isteği olduğu takip edilebilsin.
                _logger.LogInformation(
                    "[AIController.Transcribe] UserId={UserId}, FileName={FileName}, SizeBytes={Size}, Language={Language} | Result: Success={Success}, MessageLen={MsgLen}, DataIsNull={DataNull}, DataLen={DataLen}",
                    CurrentUserId,
                    file.FileName,
                    file.Length,
                    language ?? "auto",
                    result.Success,
                    result.Message?.Length ?? 0,
                    result.Data is null,
                    result.Data?.Length ?? 0);

                // Diğer controller'larla tutarlı dönüş şekli: {"data":"...","success":true,"message":""}
                // Anonymous type + CamelCase naming + Brotli pipeline'ında bazen görülen tuhaflıkları da eler.
                return HandleDataResult(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Audio transcription failed for user {UserId}, file={FileName}, size={Size}", CurrentUserId, file.FileName, file.Length);
                return StatusCode(500, new { success = false, message = "Ses çevirme servisi şu anda kullanılamıyor." });
            }
        }
    }
}
