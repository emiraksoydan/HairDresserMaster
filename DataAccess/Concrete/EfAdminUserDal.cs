using System.Threading.Tasks;
using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using Entities.Concrete.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Concrete
{
    public class EfAdminUserDal : EfEntityRepositoryBase<AdminUser, DatabaseContext>, IAdminUserDal
    {
        private readonly DatabaseContext _context;

        public EfAdminUserDal(DatabaseContext context) : base(context)
        {
            _context = context;
        }

        public async Task<AdminUser?> GetByEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return null;
            var normalized = email.Trim().ToLowerInvariant();
            return await _context.AdminUsers
                .FirstOrDefaultAsync(a => a.Email == normalized);
        }

        public async Task<AdminUser?> GetByResetTokenHash(string resetTokenHash)
        {
            if (string.IsNullOrWhiteSpace(resetTokenHash)) return null;
            return await _context.AdminUsers
                .FirstOrDefaultAsync(a => a.ResetTokenHash == resetTokenHash);
        }

        public async Task<AdminUser?> GetByRefreshTokenHash(string refreshTokenHash)
        {
            if (string.IsNullOrWhiteSpace(refreshTokenHash)) return null;
            return await _context.AdminUsers
                .FirstOrDefaultAsync(a => a.RefreshTokenHash == refreshTokenHash);
        }
    }
}
