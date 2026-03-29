using Core.Utilities.Results;
using System.Threading.Tasks;

namespace Business.Abstract
{
    public interface IContentModerationService
    {
        /// <summary>
        /// Verilen metni Gemini 2.0 Flash ile kontrol eder.
        /// </summary>
        Task<IResult> CheckContentAsync(string text);

        /// <summary>
        /// Birden fazla metni kontrol eder.
        /// </summary>
        Task<IResult> CheckContentsAsync(params string[] texts);

        /// <summary>
        /// Verilen görseli Gemini 2.0 Flash vision ile kontrol eder.
        /// Uygunsuz içerik (cinsel, şiddet, nefret vb.) tespit edilirse hata döner.
        /// </summary>
        Task<IResult> CheckImageContentAsync(Microsoft.AspNetCore.Http.IFormFile file);

        /// <summary>
        /// Verilen görsel byte dizisini Gemini 2.0 Flash vision ile kontrol eder.
        /// Background task'lardan IFormFile kullanılamadığı durumlarda kullanılır.
        /// </summary>
        Task<IResult> CheckImageContentAsync(byte[] imageBytes, string contentType, string fileName = "image");
    }
}
