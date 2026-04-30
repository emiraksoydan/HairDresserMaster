using Entities.Concrete.Dto;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Business.Abstract
{
    public interface IRealTimePublisher
    {
        Task PushNotificationAsync(Guid userId, NotificationDto dto);
        Task PushNotificationSilentUpdateAsync(Guid userId, NotificationDto dto);
        Task PushChatMessageAsync(Guid userId, ChatMessageDto dto);
        Task PushChatMessageRemovedAsync(Guid userId, Guid threadId, Guid messageId);
        Task PushChatMessageEditedAsync(Guid userId, Guid threadId, Guid messageId, string newText);
        Task PushChatThreadCreatedAsync(Guid userId, ChatThreadListItemDto dto);
        Task PushChatThreadUpdatedAsync(Guid userId, ChatThreadListItemDto dto);
        Task PushChatThreadRemovedAsync(Guid userId, Guid threadId);

        // Batched variants: tek SignalR round-trip ile birden fazla kullanıcıya push
        Task PushChatMessageToUsersAsync(IEnumerable<Guid> userIds, ChatMessageDto dto);
        Task PushChatMessageRemovedToUsersAsync(IEnumerable<Guid> userIds, Guid threadId, Guid messageId);
        Task PushChatMessageEditedToUsersAsync(IEnumerable<Guid> userIds, Guid threadId, Guid messageId, string newText);
        Task PushChatThreadRemovedToUsersAsync(IEnumerable<Guid> userIds, Guid threadId);
        Task PushChatTypingAsync(Guid userId, Guid threadId, Guid typingUserId, string typingUserName, bool isTyping);
        Task PushChatMessagesReadAsync(Guid userId, Guid threadId, Guid readerUserId, List<Guid> messageIds);
        Task PushAppointmentUpdatedAsync(Guid userId, Entities.Concrete.Dto.AppointmentGetDto appointment);
        /// <summary>İşletme randevu müsaitliğini o dükkana abone olan tüm istemcilerde tazele (koltuk/slot cache).</summary>
        Task PushStoreAvailabilityChangedAsync(Guid storeId, DateOnly date);
        Task PushBadgeUpdateAsync(Guid userId, int? notificationUnreadCount = null, int? chatUnreadCount = null);
        Task PushImageUpdatedAsync(Guid userId, Guid imageId, string imageUrl);
        Task PushImageRemovedAsync(Guid userId, Guid imageId);
    }
}
