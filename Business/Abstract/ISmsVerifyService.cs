using Core.Utilities.Results;
using System.Threading.Tasks;

namespace Business.Abstract
{
    public interface ISmsVerifyService
    {
        Task<IResult> SendAsync(string e164, string? language = null);
        Task<IResult> CheckAsync(string e164, string code);

        /// <summary>
        /// Transactional (OTP olmayan) SMS gönderimi. Reader pattern checkout linki,
        /// abonelik hatırlatma vb. iş bildirimleri için. NetGsm bulk SMS endpoint'i
        /// kullanılır; OTP rate-limit hesabıyla karışmaz.
        /// </summary>
        Task<IResult> SendTransactionalSmsAsync(string e164, string message);
    }
}
