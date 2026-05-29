using System;
using System.Threading.Tasks;
using Business.Abstract;
using Core.Utilities.Results;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace Business.Concrete
{
    /// <summary>
    /// MailKit tabanlı SMTP email gönderici. appsettings.json "Email" bölümü kullanır:
    /// SmtpHost, SmtpPort, SmtpUser, SmtpPass, FromEmail.
    /// </summary>
    public class MailKitEmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<MailKitEmailService> _logger;

        public MailKitEmailService(IConfiguration configuration, ILogger<MailKitEmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<IResult> SendAsync(string toEmail, string subject, string htmlBody, string? plainTextBody = null)
        {
            try
            {
                var section = _configuration.GetSection("Email");
                var host = section["SmtpHost"];
                var portStr = section["SmtpPort"];
                var user = section["SmtpUser"];
                var pass = section["SmtpPass"];
                var fromEmail = section["FromEmail"] ?? user;

                if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(portStr)
                    || string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(pass))
                {
                    _logger.LogError("[Email] SMTP konfigürasyonu eksik (Email:SmtpHost/Port/User/Pass).");
                    return new ErrorResult("SMTP konfigürasyonu eksik.");
                }

                if (!int.TryParse(portStr, out var port)) port = 587;

                var message = new MimeMessage();
                message.From.Add(MailboxAddress.Parse(fromEmail));
                message.To.Add(MailboxAddress.Parse(toEmail));
                message.Subject = subject;

                var builder = new BodyBuilder
                {
                    HtmlBody = htmlBody,
                    TextBody = plainTextBody ?? StripHtml(htmlBody)
                };
                message.Body = builder.ToMessageBody();

                using var client = new SmtpClient();
                var secureOption = port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
                await client.ConnectAsync(host, port, secureOption);
                await client.AuthenticateAsync(user, pass);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                _logger.LogInformation("[Email] Mail gönderildi → {To} | Konu: {Subject}", toEmail, subject);
                return new SuccessResult("Mail gönderildi.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Email] Mail gönderim hatası → {To}", toEmail);
                return new ErrorResult("Mail gönderilemedi: " + ex.Message);
            }
        }

        private static string StripHtml(string html)
        {
            if (string.IsNullOrEmpty(html)) return string.Empty;
            return System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", string.Empty);
        }
    }
}
