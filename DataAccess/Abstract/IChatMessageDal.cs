using Core.DataAccess;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccess.Abstract
{
    public interface IChatMessageDal : IEntityRepository<ChatMessage>
    {
        Task<List<ChatMessageItemDto>> GetMessagesForAppointmentAsync(Guid appointmentId, DateTime? beforeUtc);

        Task<List<ChatMessageItemDto>> GetMessagesByThreadIdAsync(Guid threadId, DateTime? beforeUtc);

        /// <summary>
        /// Gets messages for requesting user: excludes globally deleted messages AND messages the requesting user soft-deleted.
        /// </summary>
        Task<List<ChatMessageItemDto>> GetMessagesByThreadIdWithReadStatusAsync(Guid threadId, DateTime? beforeUtc, List<Guid> allParticipantIds, Guid requestingUserId);

        /// <summary>
        /// Returns IDs of all participants who have soft-deleted the given message.
        /// </summary>
        Task<List<Guid>> GetDeletionUserIdsAsync(Guid messageId);

        /// <summary>
        /// Marks a message as deleted for the given user. Returns true if a new record was created.
        /// </summary>
        Task<bool> AddUserDeletionAsync(Guid messageId, Guid userId);

        /// <summary>
        /// Soft-delete all messages in thread for a given user. Returns the count.
        /// </summary>
        Task<int> AddUserDeletionForThreadAsync(Guid threadId, Guid userId);

        /// <summary>
        /// Checks how many participants have deleted each message in the list,
        /// and sets IsDeleted=true for messages where all participantIds have deleted it.
        /// </summary>
        Task CleanupFullyDeletedMessagesAsync(IEnumerable<Guid> messageIds, IEnumerable<Guid> allParticipantIds);

        /// <summary>
        /// For each thread, the latest message visible to <paramref name="userId"/> (excludes per-user soft deletions).
        /// Used for thread list preview/sort; not the denormalized ChatThread.LastMessage* row.
        /// </summary>
        Task<Dictionary<Guid, ChatMessageItemDto>> GetLatestVisibleMessagePerThreadAsync(Guid userId, IReadOnlyList<Guid> threadIds);
    }
}
