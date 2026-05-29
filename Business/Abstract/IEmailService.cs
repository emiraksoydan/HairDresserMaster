using System.Threading.Tasks;
using Core.Utilities.Results;

namespace Business.Abstract
{
    public interface IEmailService
    {
        Task<IResult> SendAsync(string toEmail, string subject, string htmlBody, string? plainTextBody = null);
    }
}
