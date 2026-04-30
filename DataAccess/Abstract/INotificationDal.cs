using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core.DataAccess;
using Core.Utilities.Results;
using Entities.Concrete.Entities;

namespace DataAccess.Abstract
{
    public interface INotificationDal : IEntityRepository<Notification>
    {
        /// <summary>
        /// Cursor-based pagination: belirli kullanıcı için CreatedAt &lt; beforeUtc olan
        /// bildirimleri en yeniden eskiye `limit` adet kadar döner.
        /// `beforeUtc` null ise en yeni sayfa döner.
        /// `beforeId` (opsiyonel tie-breaker): aynı CreatedAt'a sahip kayıtlar arasında
        /// deterministik sıralama için Id bazlı alt-sıralama. Null geçildiğinde sadece
        /// timestamp bazlı çalışır (geriye dönük uyumluluk).
        /// </summary>
        Task<List<Notification>> GetByUserPagedAsync(Guid userId, DateTime? beforeUtc, Guid? beforeId, int limit);
    }
}
