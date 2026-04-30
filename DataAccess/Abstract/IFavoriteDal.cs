using Core.DataAccess;
using Entities.Concrete.Entities;
using System;
using System.Threading.Tasks;

namespace DataAccess.Abstract
{
    public interface IFavoriteDal : IEntityRepository<Favorite>
    {
        Task<Favorite> GetByUsersAsync(Guid favoritedFromId, Guid favoritedToId);
        Task<bool> ExistsAsync(Guid favoritedFromId, Guid favoritedToId);

        /// <summary>
        /// Cursor-based pagination: kullanıcının aktif favorilerini CreatedAt DESC sıralar,
        /// opsiyonel olarak `CreatedAt &lt; beforeUtc` filtresi ve `limit` uygular.
        /// </summary>
        Task<List<Favorite>> GetMyActiveFavoritesPagedAsync(Guid userId, DateTime? beforeUtc, Guid? beforeId, int? limit);
    }
}
