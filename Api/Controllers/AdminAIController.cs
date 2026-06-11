using Business.Abstract;
using Business.Resources;
using Core.Extensions;
using Core.Utilities.Results;
using Entities.Concrete.Dto;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Api.Controllers
{
    [Route("api/admin/ai")]
    public class AdminAIController(
        IAdminAIAssistantService adminAiService,
        IAIAssistantService aiAssistantService,
        ILogger<AdminAIController> logger) : BaseApiController
    {
        private IActionResult? AdminOnly()
        {
            if (!User.ClaimRoles().Contains("Admin"))
                return StatusCode(403, new ErrorResult(Messages.AdminOperationRequiresAdminRole));
            return null;
        }

        private Guid CurrentAdminId()
        {
            var idStr = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? User?.FindFirst("identifier")?.Value
                     ?? User?.FindFirst("sub")?.Value;
            return Guid.TryParse(idStr, out var id) ? id : Guid.Empty;
        }

        /// <summary>
        /// Admin panel yapay zeka asistanı — Gemini (ücretsiz tier) OpenAI uyumlu tool-calling.
        /// </summary>
        [HttpPost("chat")]
        public async Task<IActionResult> Chat([FromBody] AdminAIChatRequestDto request)
        {
            var guard = AdminOnly();
            if (guard != null) return guard;

            var adminId = CurrentAdminId();
            if (adminId == Guid.Empty)
                return Unauthorized(new ErrorResult(Messages.AdminAuthUserNotFound));

            var result = await adminAiService.ChatAsync(adminId, request);

            if (!result.Success && string.Equals(result.Message, Messages.AdminAiRateLimit, StringComparison.Ordinal))
                return StatusCode(StatusCodes.Status429TooManyRequests, result);

            return HandleDataResult(result);
        }

        /// <summary>Onay bekleyen yıkıcı admin AI işlemlerini uygular.</summary>
        [HttpPost("confirm")]
        public async Task<IActionResult> Confirm([FromBody] AdminAIConfirmRequestDto request)
        {
            var guard = AdminOnly();
            if (guard != null) return guard;

            var adminId = CurrentAdminId();
            if (adminId == Guid.Empty)
                return Unauthorized(new ErrorResult(Messages.AdminAuthUserNotFound));

            var result = await adminAiService.ConfirmActionsAsync(adminId, request);
            return HandleDataResult(result);
        }

        /// <summary>
        /// Sesli komut — Groq Whisper ile metne çevirir (Claude/Gemini değil; yalnızca transkripsiyon).
        /// </summary>
        [HttpPost("transcribe")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Transcribe(IFormFile file, [FromQuery] string? language = "tr")
        {
            var guard = AdminOnly();
            if (guard != null) return guard;

            if (file == null || file.Length == 0)
                return BadRequest(new { success = false, message = Messages.AiVoiceFileEmpty });

            var audioCheck = Business.Helpers.UploadFileValidator.ValidateTranscriptionAudio(file);
            if (!audioCheck.Success)
                return BadRequest(new { success = false, message = audioCheck.Message });

            try
            {
                using var rawStream = file.OpenReadStream();
                using var memStream = new MemoryStream();
                await rawStream.CopyToAsync(memStream);
                memStream.Position = 0;

                var result = await aiAssistantService.TranscribeAudioAsync(
                    memStream, file.FileName, file.ContentType, language);

                if (!result.Success && string.Equals(result.Message, Messages.WhisperRateLimitKey, StringComparison.Ordinal))
                    return StatusCode(StatusCodes.Status429TooManyRequests, result);

                return HandleDataResult(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[AdminAI.Transcribe] failed for admin");
                return StatusCode(500, new { success = false, message = Messages.AiVoiceTranscriptionServiceUnavailable });
            }
        }
    }
}
