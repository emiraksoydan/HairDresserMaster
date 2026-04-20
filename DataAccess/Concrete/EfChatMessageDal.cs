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

        public async Task<List<ChatMessageItemDto>> GetMessagesForAppointmentAsync(Guid appointmentId, DateTime? beforeUtc)
        {
            var query = Context.ChatMessages.AsNoTracking()
                .Where(m => m.AppointmentId == appointmentId && !m.IsDeleted);

            if (beforeUtc.HasValue)
                query = query.Where(m => m.CreatedAt < beforeUtc.Value);

            var msgs = await query
                .OrderByDescending(m => m.CreatedAt)
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

            msgs.Reverse();
            return msgs;
        }

        public async Task<List<ChatMessageItemDto>> GetMessagesByThreadIdAsync(Guid threadId, DateTime? beforeUtc)
        {
            var query = Context.ChatMessages.AsNoTracking()
                .Where(m => m.ThreadId == threadId && !m.IsDeleted);

            if (beforeUtc.HasValue)
                query = query.Where(m => m.CreatedAt < beforeUtc.Value);

            var msgs = await query
                .OrderByDescending(m => m.CreatedAt)
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

            msgs.Reverse();
            return msgs;
        }

        public async Task<List<ChatMessageItemDto>> GetMessagesByThreadIdWithReadStatusAsync(
            Guid threadId, DateTime? beforeUtc, List<Guid> allParticipantIds, Guid requestingUserId)
        {
            // Get user-deleted message IDs for requestingUserId
            var userDeletedMessageIds = await Context.ChatMessageUserDeletions
                .AsNoTracking()
                .Where(d => d.UserId == requestingUserId)
                .Select(d => d.MessageId)
                .ToListAsync();

            var userDeletedSet = new HashSet<Guid>(userDeletedMessageIds);

            var query = Context.ChatMessages.AsNoTracking()
                .Where(m => m.ThreadId == threadId && !m.IsDeleted);

            if (beforeUtc.HasValue)
                query = query.Where(m => m.CreatedAt < beforeUtc.Value);

            var msgs = await query
                .OrderByDescending(m => m.CreatedAt)
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

            // Filter out messages the requesting user has deleted
            msgs = msgs.Where(m => !userDeletedSet.Contains(m.MessageId)).ToList();

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
            var messageIds = await Context.ChatMessages
                .AsNoTracking()
                .Where(m => m.ThreadId == threadId && !m.IsDeleted)
                .Select(m => m.Id)
                .ToListAsync();

            if (messageIds.Count == 0) return 0;

            var alreadyDeleted = await Context.ChatMessageUserDeletions
                .AsNoTracking()
                .Where(d => messageIds.Contains(d.MessageId) && d.UserId == userId)
                .Select(d => d.MessageId)
                .ToListAsync();

            var alreadyDeletedSet = new HashSet<Guid>(alreadyDeleted);
            var toAdd = messageIds.Where(id => !alreadyDeletedSet.Contains(id)).ToList();

            if (toAdd.Count == 0) return 0;

            var deletions = toAdd.Select(msgId => new ChatMessageUserDeletion
            {
                Id = Guid.NewGuid(),
                MessageId = msgId,
                UserId = userId,
                DeletedAt = DateTime.UtcNow
            });

            await Context.ChatMessageUserDeletions.AddRangeAsync(deletions);
            await Context.SaveChangesAsync();
            return toAdd.Count;
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

            var userDeletedIds = await Context.ChatMessageUserDeletions.AsNoTracking()
                .Where(d => d.UserId == userId)
                .Select(d => d.MessageId)
                .ToListAsync();
            var delSet = userDeletedIds.ToHashSet();

            var rows = await Context.ChatMessages.AsNoTracking()
                .Where(m => threadIds.Contains(m.ThreadId) && !m.IsDeleted)
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
                .ToListAsync();

            rows = rows.Where(m => !delSet.Contains(m.Id)).ToList();

            var dict = new Dictionary<Guid, ChatMessageItemDto>();
            foreach (var g in rows.GroupBy(r => r.ThreadId))
            {
                var r = g.OrderByDescending(x => x.CreatedAt).First();
                dict[g.Key] = new ChatMessageItemDto
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
