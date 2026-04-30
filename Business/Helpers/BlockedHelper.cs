using DataAccess.Abstract;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Business.Helpers
{
    /// <summary>
    /// Engelleme kontrolü için helper sınıf
    /// </summary>
    public class BlockedHelper
    {
        private readonly IBlockedDal _blockedDal;

        public BlockedHelper(IBlockedDal blockedDal)
        {
            _blockedDal = blockedDal;
        }

        /// <summary>
        /// Kullanıcının engellediği ve engellendiği tüm kullanıcı ID'lerini getirir
        /// </summary>
        public async Task<HashSet<Guid>> GetAllBlockedUserIdsAsync(Guid userId)
        {
            return await _blockedDal.GetBlockedUserIdsAsync(userId);
        }

        /// <summary>
        /// İki kullanıcı arasında engelleme var mı kontrol eder (çift yönlü)
        /// </summary>
        public async Task<bool> HasBlockBetweenAsync(Guid userId1, Guid userId2)
        {
            return await _blockedDal.HasAnyBlockBetweenAsync(userId1, userId2);
        }

        /// <summary>
        /// Belirli bir kullanıcı engellemiş mi kontrol eder (tek yönlü)
        /// </summary>
        public async Task<bool> IsBlockedByAsync(Guid blockerId, Guid blockedId)
        {
            return await _blockedDal.IsBlockedAsync(blockerId, blockedId);
        }

        // NOT: Eski FilterBlockedStoresAsync/FilterBlockedUsersAsync in-memory post-filter
        // method'ları kaldırıldı. Artık Discovery DAL'ları (EfBarberStoreDal/EfFreeBarberDal)
        // blockedUserIds listesini parametre olarak alıp SQL WHERE içinde uyguluyor; bu
        // sayede pagination sonuçları tam dolu gelir.
        // İhtiyaç duyan taraflar:
        //   var blocked = (await helper.GetAllBlockedUserIdsAsync(userId)).ToList();
        //   await dal.GetXxx(..., blockedUserIds: blocked);
    }
}
