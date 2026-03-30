using Business.Abstract;
using Entities.Concrete.Dto;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace Api.Controllers
{
    [Route("api/[controller]")]
    public class AIController : BaseApiController
    {
        private readonly IAIAssistantService _aiService;

        public AIController(IAIAssistantService aiService)
        {
            _aiService = aiService;
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

            return await HandleUserDataOperation(userId =>
                _aiService.ProcessRequestAsync(userId, req.Message, req.Language ?? "tr", req.Latitude, req.Longitude));
        }

        /// <summary>
        /// Ses dosyasını (m4a/mp3/wav) OpenAI Whisper ile metne dönüştürür.
        /// API key backend'de tutulur; frontend sadece JWT ile çağırır.
        /// </summary>
        [HttpPost("transcribe")]
        public async Task<IActionResult> Transcribe(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { success = false, message = "Ses dosyası boş." });

            try
            {
                using var rawStream = file.OpenReadStream();
                using var memStream = new MemoryStream();
                await rawStream.CopyToAsync(memStream);
                memStream.Position = 0;
                var result = await _aiService.TranscribeAudioAsync(memStream, file.FileName, file.ContentType);

                if (!result.Success)
                    return BadRequest(new { success = false, message = result.Message });

                return Ok(new { success = true, data = result.Data });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Ses çevirme servisi şu anda kullanılamıyor." });
            }
        }
    }
}
