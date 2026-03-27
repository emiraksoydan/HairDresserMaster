using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using DataAccess.Concrete;
using Entities.Concrete.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace DataAccess.Concrete
{
    public class EfUserFcmTokenDal : EfEntityRepositoryBase<UserFcmToken, DatabaseContext>, IUserFcmTokenDal
    {
        private readonly DatabaseContext _context;
        
        private static string ComputeTokenHash(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return string.Empty;
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            return Convert.ToBase64String(bytes);
        }

        public EfUserFcmTokenDal(DatabaseContext context) : base(context)
        {
            _context = context;
        }

        public async Task<List<UserFcmToken>> GetActiveTokensByUserIdAsync(Guid userId)
        {
            return await _context.Set<UserFcmToken>()
                .Where(x => x.UserId == userId && x.IsActive)
                .OrderByDescending(x => x.UpdatedAt) // Most recently used tokens first
                .ToListAsync();
        }

        public async Task<UserFcmToken?> GetByTokenAsync(string fcmToken)
        {
            var tokenHash = ComputeTokenHash(fcmToken);
            if (!string.IsNullOrWhiteSpace(tokenHash))
            {
                var byHash = await _context.Set<UserFcmToken>()
                    .FirstOrDefaultAsync(x => x.FcmTokenHash == tokenHash);
                if (byHash is not null)
                    return byHash;
            }

            return await _context.Set<UserFcmToken>()
                .FirstOrDefaultAsync(x => x.FcmToken == fcmToken);
        }

        public async Task DeactivateTokenAsync(string fcmToken)
        {
            var tokenHash = ComputeTokenHash(fcmToken);
            UserFcmToken? token = null;
            if (!string.IsNullOrWhiteSpace(tokenHash))
            {
                token = await _context.Set<UserFcmToken>()
                    .FirstOrDefaultAsync(x => x.FcmTokenHash == tokenHash);
            }
            token ??= await _context.Set<UserFcmToken>()
                .FirstOrDefaultAsync(x => x.FcmToken == fcmToken);
            if (token != null)
            {
                token.IsActive = false;
                token.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }

        public async Task DeactivateAllUserTokensAsync(Guid userId)
        {
            var tokens = await _context.Set<UserFcmToken>()
                .Where(x => x.UserId == userId && x.IsActive)
                .ToListAsync();
            
            if (tokens.Count == 0) return;
            
            foreach (var token in tokens)
            {
                token.IsActive = false;
                token.UpdatedAt = DateTime.UtcNow;
            }
            await _context.SaveChangesAsync();
        }
    }
}

