using System.Threading.Tasks;
using Core.DataAccess;
using Entities.Concrete.Entities;

namespace DataAccess.Abstract
{
    public interface IAdminUserDal : IEntityRepository<AdminUser>
    {
        Task<AdminUser?> GetByEmail(string email);
        Task<AdminUser?> GetByResetTokenHash(string resetTokenHash);
        Task<AdminUser?> GetByRefreshTokenHash(string refreshTokenHash);
    }
}
