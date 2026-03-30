using Core.Utilities.Results;
using Entities.Concrete.Dto;

namespace Business.Abstract
{
    public interface IAIAssistantService
    {
        /// <summary>
        /// Kullanıcının sesle/yazıyla verdiği komutu işler; randevu bağlamını oluşturup
        /// GPT-4o ile yorumlar ve uygun aksiyonu (onay/ret/iptal/liste) gerçekleştirir.
        /// </summary>
        Task<IDataResult<AIAssistantResponseDto>> ProcessRequestAsync(Guid userId, string userMessage, string language = "tr", double? latitude = null, double? longitude = null);

        /// <summary>
        /// Ses dosyasını OpenAI Whisper ile metne dönüştürür.
        /// </summary>
        Task<IDataResult<string>> TranscribeAudioAsync(Stream audioStream, string fileName, string? contentType = null);
    }
}
