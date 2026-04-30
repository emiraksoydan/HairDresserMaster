using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DataAccess.Concrete
{
    public class EfChatMessageDal : EfEntityRepositoryBase<ChatMessage, DatabaseContext>, IChatMessageDal
    {
        public EfChatMessageDal(DatabaseContext context) : base(context) { }

        public async Task<List<ChatMessageItemDto>> GetMessagesByThreadIdWithReadStatusAsync(
            Guid threadId, DateTime? beforeUtc, Guid? beforeId, List<Guid> allParticipantIds, Guid requestingUserId, int limit = 30)
        {
            // Pagination: user-deleted filtrelemesi EF sorgusunun İÇİNDE yapılıyor (NOT EXISTS subquery).
            // Böylece Take(limit) gerçekten istenen adette satır döndürüyor; "30 iste, 25 dönsün"
            // sorunu yaşanmıyor. Önceden liste memory'de filtreleniyordu — paged dünyada yanıltıcı.
            //
            // Keyset tie-breaker: Yüksek frekanslı chat'te aynı CreatedAt'a sahip 2 mesaj
            // bulunması mümkündür. `beforeId` ile `(CreatedAt, Id)` çiftinden sıkı sıralama sağlanır:
            //   WHERE CreatedAt < @ts OR (CreatedAt == @ts AND Id < @id)
            //   ORDER BY CreatedAt DESC, Id DESC
            var query = Context.ChatMessages.AsNoTracking()
                .Where(m => m.ThreadId == threadId && !m.IsDeleted)
                .Where(m => !Context.ChatMessageUserDeletions
                    .Any(d => d.UserId == requestingUserId && d.MessageId == m.Id));

            if (beforeUtc.HasValue)
            {
                if (beforeId.HasValue)
                {
                    var cTs = beforeUtc.Value;
                    var cId = beforeId.Value;
                    query = query.Where(m => m.CreatedAt < cTs
                                          || (m.CreatedAt == cTs && m.Id.CompareTo(cId) < 0));
                }
                else
                {
                    query = query.Where(m => m.CreatedAt < beforeUtc.Value);
                }
            }

            // Keyset pagination: en yeniden eskiye sırala, istenen adet kadar al.
            // Frontend reverse ederek UI'da "eski -> yeni" gösterir.
            var msgs = await query
                .OrderByDescending(m => m.CreatedAt)
                .ThenByDescending(m => m.Id)
                .Take(limit)
                .Select(m => new ChatMessageItemDto
                {
                    MessageId = m.Id,
                    SenderUserId = m.SenderUserId,
                    Text = m.Text,
                    CreatedAt = m.CreatedAt,
                    MessageType = (int)m.MessageType,
                    MediaUrl = m.MediaUrl,
                    ReplyToMessageId = m.ReplyToMessageId,
                    ReplyToTextPreview = m.ReplyToTextPreview
                })
                .ToListAsync();

            if (msgs.Count == 0 || allParticipantIds.Count == 0)
            {
                msgs.Reverse();
                return msgs;
            }

            var messageIds = msgs.Select(m => m.MessageId).ToList();

            var receipts = await Context.MessageReadReceipts
                .AsNoTracking()
                .Where(r => r.ThreadId == threadId && messageIds.Contains(r.MessageId))
                .Select(r => new { r.MessageId, r.UserId })
                .ToListAsync();

            var receiptsByMessage = receipts
                .GroupBy(r => r.MessageId)
                .ToDictionary(g => g.Key, g => new HashSet<Guid>(g.Select(r => r.UserId)));

            foreach (var msg in msgs)
            {
                var requiredReaders = allParticipantIds.Where(id => id != msg.SenderUserId).ToList();
                if (requiredReaders.Count == 0)
                {
                    msg.IsFullyRead = true;
                    continue;
                }
                if (receiptsByMessage.TryGetValue(msg.MessageId, out var readers))
                    msg.IsFullyRead = requiredReaders.All(id => readers.Contains(id));
            }

            msgs.Reverse();
            return msgs;
        }

        public async Task<List<Guid>> GetDeletionUserIdsAsync(Guid messageId)
        {
            return await Context.ChatMessageUserDeletions
                .AsNoTracking()
                .Where(d => d.MessageId == messageId)
                .Select(d => d.UserId)
                .ToListAsync();
        }

        public async Task<bool> AddUserDeletionAsync(Guid messageId, Guid userId)
        {
            var exists = await Context.ChatMessageUserDeletions
                .AnyAsync(d => d.MessageId == messageId && d.UserId == userId);

            if (exists) return false;

            Context.ChatMessageUserDeletions.Add(new ChatMessageUserDeletion
            {
                Id = Guid.NewGuid(),
                MessageId = messageId,
                UserId = userId,
                DeletedAt = DateTime.UtcNow
            });
            await Context.SaveChangesAsync();
            return true;
        }

        public async Task<int> AddUserDeletionForThreadAsync(Guid threadId, Guid userId)
        {
            var (added, _) = await AddUserDeletionForThreadWithIdsAsync(threadId, userId);
            return added;
        }

        public async Task<(int addedCount, List<Guid> allThreadMessageIds)> AddUserDeletionForThreadWithIdsAsync(Guid threadId, Guid userId)
        {
            var messageIds = await Context.ChatMessages
                .AsNoTracking()
                .Where(m => m.ThreadId == threadId && !m.IsDeleted)
                .Select(m => m.Id)
                .ToListAsync();

            if (messageIds.Count == 0) return (0, messageIds);

            var alreadyDeleted = await Context.ChatMessageUserDeletions
                .AsNoTracking()
                .Where(d => messageIds.Contains(d.MessageId) && d.UserId == userId)
                .Select(d => d.MessageId)
                .ToListAsync();

            var alreadyDeletedSet = new HashSet<Guid>(alreadyDeleted);
            var toAdd = messageIds.Where(id => !alreadyDeletedSet.Contains(id)).ToList();

            if (toAdd.Count == 0) return (0, messageIds);

            var deletions = toAdd.Select(msgId => new ChatMessageUserDeletion
            {
                Id = Guid.NewGuid(),
                MessageId = msgId,
                UserId = userId,
                DeletedAt = DateTime.UtcNow
            });

            await Context.ChatMessageUserDeletions.AddRangeAsync(deletions);
            await Context.SaveChangesAsync();
            return (toAdd.Count, messageIds);
        }

        public async Task CleanupFullyDeletedMessagesAsync(IEnumerable<Guid> messageIds, IEnumerable<Guid> allParticipantIds)
        {
            var msgIdList = messageIds.ToList();
            var participantList = allParticipantIds.ToList();
            if (msgIdList.Count == 0 || participantList.Count == 0) return;

            // Group deletions by messageId
            var deletions = await Context.ChatMessageUserDeletions
                .AsNoTracking()
                .Where(d => msgIdList.Contains(d.MessageId))
                .GroupBy(d => d.MessageId)
                .Select(g => new { MessageId = g.Key, DeletedByCount = g.Count() })
                .ToListAsync();

            var fullyDeletedIds = deletions
                .Where(d => d.DeletedByCount >= participantList.Count)
                .Select(d => d.MessageId)
                .ToList();

            if (fullyDeletedIds.Count == 0) return;

            await Context.ChatMessages
                .Where(m => fullyDeletedIds.Contains(m.Id))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(m => m.IsDeleted, true)
                    .SetProperty(m => m.DeletedAt, DateTime.UtcNow));
        }

        public async Task<Dictionary<Guid, ChatMessageItemDto>> GetLatestVisibleMessagePerThreadAsync(Guid userId, IReadOnlyList<Guid> threadIds)
        {
            if (threadIds == null || threadIds.Count == 0)
                return new Dictionary<Guid, ChatMessageItemDto>();

            // Pagination optimizasyonu: kullanıcının TÜM silme kayıtlarını çekmek yerine
            // NOT EXISTS subquery ile SQL içinde filtrele. Aktif kullanıcı 6 ayda 2k+ mesaj
            // silmişse eski versiyon her thread listesinde tüm Guid setini RAM'e çekiyordu.
            // Yeni versiyon ise UserId/MessageId composite-index'inden EXISTS ile yararlanır.
            //
            // Ek olarak: her thread için sadece EN YENİ visible mesajı al — Postgres
            // DISTINCT ON pattern'inin LINQ karşılığı GroupBy + OrderByDescending'dir.
            // Eski kod tüm satırları çekip memory'de GroupBy yapıyordu (1k mesajlık 5
            // threadte 5k satır → şimdi yalnız 5 satır).
            var rows = await Context.ChatMessages.AsNoTracking()
                .Where(m => threadIds.Contains(m.ThreadId) && !m.IsDeleted)
                .Where(m => !Context.ChatMessageUserDeletions
                    .Any(d => d.UserId == userId && d.MessageId == m.Id))
                .GroupBy(m => m.ThreadId)
                .Select(g => g
                    .OrderByDescending(m => m.CreatedAt)
                    .Select(m => new
                    {
                        m.ThreadId,
                        m.Id,
                        m.SenderUserId,
                        m.Text,
                        m.CreatedAt,
                        MessageType = (int)m.MessageType,
                        m.MediaUrl,
                        m.ReplyToMessageId,
                        m.ReplyToTextPreview
                    })
                    .First())
                .ToListAsync();

            var dict = new Dictionary<Guid, ChatMessageItemDto>(rows.Count);
            foreach (var r in rows)
            {
                dict[r.ThreadId] = new ChatMessageItemDto
                {
                    MessageId = r.Id,
                    SenderUserId = r.SenderUserId,
                    Text = r.Text,
                    CreatedAt = r.CreatedAt,
                    MessageType = r.MessageType,
                    MediaUrl = r.MediaUrl,
                    ReplyToMessageId = r.ReplyToMessageId,
                    ReplyToTextPreview = r.ReplyToTextPreview,
                    FileName = null,
                    IsFullyRead = false,
                    IsEdited = false
                };
            }

            return dict;
        }
    }
}
