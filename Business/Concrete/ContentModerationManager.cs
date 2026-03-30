using Business.Abstract;
using Core.Utilities.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Business.Concrete
{
    /// <summary>
    /// İçerik moderasyonu — Azure AI Content Safety
    ///   - Metin  → /contentsafety/text:analyze
    ///   - Görsel → /contentsafety/image:analyze (base64)
    /// Severity eşiği: >= 2 → flagged (0=safe, 2=low, 4=medium, 6=high)
    /// HTTP hata / ağ / kota durumunda (API anahtarı yok veya başarısız yanıt) kontrol atlanır ve içerik geçer (fail-open);
    /// yalnızca 200 OK ve içerik işaretliyse reddedilir.
    /// </summary>
    public class ContentModerationManager : IContentModerationService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<ContentModerationManager> _logger;
        private readonly HttpClient _httpClient;

        private const string API_VERSION = "2023-10-01";
        private static readonly string[] CATEGORIES = ["Hate", "Sexual", "Violence", "SelfHarm"];
        private const int SEVERITY_THRESHOLD = 2; // 0=safe, 2=low, 4=medium, 6=high

        public ContentModerationManager(
            IConfiguration configuration,
            ILogger<ContentModerationManager> logger,
            IHttpClientFactory httpClientFactory)
        {
            _configuration = configuration;
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient("Azure");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Metin Moderasyonu
        // ─────────────────────────────────────────────────────────────────────

        public async Task<IResult> CheckContentAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return new SuccessResult();

            var (apiKey, endpoint) = GetConfig();
            if (apiKey == null)
                return new SuccessResult();

            try
            {
                var url = $"{endpoint}contentsafety/text:analyze?api-version={API_VERSION}";

                var payload = new
                {
                    text,
                    categories = CATEGORIES,
                    outputType = "FourSeverityLevels"
                };

                var request = BuildRequest(HttpMethod.Post, url, apiKey, payload);
                var response = await _httpClient.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Azure Content Safety metin hatası: {StatusCode} - {Content}",
                        response.StatusCode, responseContent);
                    return new SuccessResult(); // Fail-open
                }

                if (IsFlagged(responseContent, out var flaggedCategory))
                {
                    _logger.LogWarning("Azure Content Safety metin işaretledi. Kategori: {Category}", flaggedCategory);
                    return new ErrorResult("Mesajınız uygunsuz içerik barındırmaktadır. Lütfen küfür, hakaret veya uygunsuz ifadeler kullanmayınız.");
                }

                return new SuccessResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Azure Content Safety metin moderasyon hatası");
                return new SuccessResult(); // Fail-open
            }
        }

        public async Task<IResult> CheckContentsAsync(params string[] texts)
        {
            foreach (var text in texts)
            {
                var result = await CheckContentAsync(text);
                if (!result.Success)
                    return result;
            }
            return new SuccessResult();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Görsel Moderasyon
        // ─────────────────────────────────────────────────────────────────────

        public async Task<IResult> CheckImageContentAsync(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return new SuccessResult();

            using var memoryStream = new System.IO.MemoryStream();
            await file.CopyToAsync(memoryStream);
            return await CheckImageBytesAsync(memoryStream.ToArray(), file.FileName);
        }

        public async Task<IResult> CheckImageContentAsync(byte[] imageBytes, string contentType, string fileName = "image")
        {
            if (imageBytes == null || imageBytes.Length == 0)
                return new SuccessResult();

            return await CheckImageBytesAsync(imageBytes, fileName);
        }

        private async Task<IResult> CheckImageBytesAsync(byte[] imageBytes, string fileName)
        {
            var (apiKey, endpoint) = GetConfig();
            if (apiKey == null)
                return new SuccessResult();

            try
            {
                var url = $"{endpoint}contentsafety/image:analyze?api-version={API_VERSION}";

                var payload = new
                {
                    image = new { content = Convert.ToBase64String(imageBytes) },
                    categories = CATEGORIES,
                    outputType = "FourSeverityLevels"
                };

                var request = BuildRequest(HttpMethod.Post, url, apiKey, payload);
                var response = await _httpClient.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Azure Content Safety görsel hatası: {StatusCode} - {Content}",
                        response.StatusCode, responseContent);
                    return new SuccessResult(); // Fail-open
                }

                if (IsFlagged(responseContent, out var flaggedCategory))
                {
                    _logger.LogWarning("Azure Content Safety görsel işaretledi. Kategori: {Category}, Dosya: {FileName}",
                        flaggedCategory, fileName);
                    return new ErrorResult("Yüklediğiniz görsel uygunsuz içerik barındırmaktadır. Lütfen uygun bir görsel yükleyiniz.");
                }

                return new SuccessResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Azure Content Safety görsel moderasyon hatası. Dosya: {FileName}", fileName);
                return new SuccessResult(); // Fail-open
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Yardımcı metodlar
        // ─────────────────────────────────────────────────────────────────────

        private (string? apiKey, string endpoint) GetConfig()
        {
            var apiKey = _configuration["Azure:ContentSafety:ApiKey"];
            var endpoint = _configuration["Azure:ContentSafety:Endpoint"] ?? "";

            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.LogWarning("Azure Content Safety API key yapılandırılmamış. Moderasyon atlanıyor.");
                return (null, endpoint);
            }

            // Endpoint trailing slash garantisi
            if (!endpoint.EndsWith('/'))
                endpoint += '/';

            return (apiKey, endpoint);
        }

        private static HttpRequestMessage BuildRequest(HttpMethod method, string url, string apiKey, object payload)
        {
            return new HttpRequestMessage(method, url)
            {
                Headers = { { "Ocp-Apim-Subscription-Key", apiKey } },
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
        }

        private bool IsFlagged(string responseContent, out string flaggedCategory)
        {
            flaggedCategory = string.Empty;
            try
            {
                using var doc = JsonDocument.Parse(responseContent);
                if (!doc.RootElement.TryGetProperty("categoriesAnalysis", out var categories))
                    return false;

                foreach (var item in categories.EnumerateArray())
                {
                    if (item.TryGetProperty("severity", out var severityEl) &&
                        severityEl.GetInt32() >= SEVERITY_THRESHOLD)
                    {
                        flaggedCategory = item.TryGetProperty("category", out var cat)
                            ? cat.GetString() ?? ""
                            : "Unknown";
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Azure Content Safety yanıtı parse edilemedi");
            }

            return false;
        }
    }
}
