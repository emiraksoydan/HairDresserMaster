using System.Net;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Core.Exceptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Core.Extensions
{
    /// <summary>
    /// Global exception middleware - Tüm hataları yakalar ve tutarlı JSON response döner
    /// Response format: { success: false, message: "...", errors?: [...] }
    /// </summary>
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;

        public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (ValidationException ex)
            {
                // FluentValidation hataları - 400 Bad Request
                await WriteErrorResponse(context, HttpStatusCode.BadRequest, "Doğrulama hatası", 
                    ex.Errors.Select(e => new { field = e.PropertyName, message = e.ErrorMessage }));
            }
            catch (UnauthorizedOperationException ex)
            {
                // Yetki hataları - 403 Forbidden
                await WriteErrorResponse(context, HttpStatusCode.Forbidden, ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                // Genel yetki hataları - 403 Forbidden
                await WriteErrorResponse(context, HttpStatusCode.Forbidden, ex.Message);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException pgEx)
            {
                // PostgreSQL veritabanı hataları - 400 Bad Request
                var userMessage = pgEx.SqlState switch
                {
                    "23505" => "Bu işlem için zaten bir kayıt mevcut. Lütfen farklı bir değer deneyin.",
                    "23503" => "İlgili kayıt bulunamadı. Lütfen geçerli bir değer seçin.",
                    "23502" => "Zorunlu alanlar eksik. Lütfen tüm gerekli bilgileri doldurun.",
                    "23514" => "Girilen değer geçersiz. Lütfen kontrol edin.",
                    "23506" => "Bu işlem için çakışan bir kayıt mevcut.",
                    _ => "Veritabanı hatası oluştu. Lütfen tekrar deneyin."
                };
                await WriteErrorResponse(context, HttpStatusCode.BadRequest, userMessage);
            }
            catch (ArgumentException ex)
            {
                // Argüman hataları - 400 Bad Request
                await WriteErrorResponse(context, HttpStatusCode.BadRequest, ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                // Geçersiz işlem hataları - 400 Bad Request
                await WriteErrorResponse(context, HttpStatusCode.BadRequest, ex.Message);
            }
            // ----------------------------------------------------------------------
            // ConnectionResetException / OperationCanceledException — istemci kopması
            // Mobil app background'a alındığında, ağ değiştiğinde, kullanıcı geri tuşuna
            // bastığında client connection kapanır. Sunucu bu sırada body okumaya çalışırsa
            // bu exception fırlar. Bu BUG DEĞİL — beklenen davranış. ERROR seviyesinde
            // log yapmak gürültü yaratıyor (özellikle /api/Auth/refresh gibi otomatik
            // tetiklenen endpoint'lerde). Debug seviyesine indirildi + response yazılmıyor
            // (zaten client gitmiş).
            // ----------------------------------------------------------------------
            catch (ConnectionResetException)
            {
                _logger.LogDebug(
                    "Client disconnected during {Method} {Path} (ConnectionResetException — normal davranış, action gereği yok).",
                    context.Request.Method, context.Request.Path);
                // Response yazma — client zaten gitti.
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                _logger.LogDebug(
                    "Request aborted by client on {Method} {Path} (OperationCanceledException).",
                    context.Request.Method, context.Request.Path);
            }
            catch (Exception ex) when (
                ex.GetType().FullName == "Microsoft.AspNetCore.Server.Kestrel.Core.BadHttpRequestException"
                || ex.GetType().FullName == "Microsoft.AspNetCore.Http.BadHttpRequestException"
                || ex.InnerException is ConnectionResetException)
            {
                // Bozuk HTTP request veya inner ConnectionReset — yine client kaynaklı
                _logger.LogDebug(ex, "Client-side request issue on {Method} {Path}", context.Request.Method, context.Request.Path);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path);

                // Diğer tüm beklenmeyen hatalar - 500 Internal Server Error
                if (!context.Response.HasStarted)
                    await WriteErrorResponse(context, HttpStatusCode.InternalServerError, "Beklenmeyen bir hata oluştu. Lütfen tekrar deneyin.");
            }
        }

        private static async Task WriteErrorResponse(HttpContext context, HttpStatusCode statusCode, string message, object? errors = null)
        {
            context.Response.StatusCode = (int)statusCode;
            context.Response.ContentType = "application/json";

            var payload = errors != null
                ? new { success = false, message, errors }
                : new { success = false, message, errors = (object?)null };

            var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            await context.Response.WriteAsync(JsonSerializer.Serialize(payload, options));
        }
    }
}
