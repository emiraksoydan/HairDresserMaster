using Core.Utilities.Results;
using Entities.Concrete.Dto;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Business.Abstract
{
    public interface IChatService
    {
        // Randevu thread'i için mesaj gönderme (AppointmentId ile)
        Task<IDataResult<ChatMessageDto>> SendMessageAsync(Guid senderUserId, Guid appointmentId, string text, Guid? replyToMessageId = null);

        // Favori thread için mesaj gönderme (ThreadId ile)
        Task<IDataResult<ChatMessageDto>> SendFavoriteMessageAsync(Guid senderUserId, Guid threadId, string text, Guid? replyToMessageId = null);

        // Medya mesajı gönderme (resim / konum / dosya)
        Task<IDataResult<ChatMessageDto>> SendMediaMessageAsync(Guid senderUserId, Guid threadId, int messageType, string mediaUrl, Guid? replyToMessageId = null, string? fileName = null);

        // Mesaj silme (per-user gizleme: yalnızca thread katılımcıları; gönderen şartı yok)
        Task<IResult> DeleteMessageAsync(Guid requestingUserId, Guid messageId);

        // Metin mesajı düzenleme (yalnızca gönderen, yalnızca Text tipi)
        Task<IResult> EditMessageAsync(Guid requestingUserId, Guid messageId, string newText);

        /// <summary>Hesap kapanışı: kullanıcının gönderdiği mesaj içeriklerini şifreli boş metne indirger (KVKK). Thread kayıtları silinmez.</summary>
        Task RedactUserContentForAccountClosureAsync(Guid userId);

        // Tüm sohbeti sil (per-user soft-delete: sadece bu kullanıcı için thread gizlenir)
        Task<IResult> DeleteThreadForUserAsync(Guid requestingUserId, Guid threadId);
        
        // Thread okundu işaretleme (ThreadId ile - hem randevu hem favori için)
        Task<IDataResult<bool>> MarkThreadReadAsync(Guid userId, Guid threadId);
        
        // Randevu thread'i için okundu işaretleme (geriye dönük uyumluluk için)
        Task<IDataResult<bool>> MarkThreadReadByAppointmentAsync(Guid userId, Guid appointmentId);

        // System worker kullanımı için (SecuredOperation olmadan)
        Task<IDataResult<bool>> MarkThreadReadByAppointmentSystemAsync(Guid userId, Guid appointmentId);

        /// <summary>
        /// Kullanıcının thread listesi (opsiyonel pagination).
        /// `beforeUtc` = son yüklü thread'in LastMessageAt'i; `beforeId` = aynı timestamp'lı
        /// ties için ThreadId tie-breaker (opsiyonel, null iken timestamp-only fallback);
        /// `limit` = sayfa boyutu. Parametresiz çağrı eski davranış (tüm liste).
        /// </summary>
        Task<IDataResult<List<ChatThreadListItemDto>>> GetThreadsAsync(Guid userId, DateTime? beforeUtc = null, Guid? beforeId = null, int? limit = null);

        // Mesajları getir (ThreadId ile - hem randevu hem favori için) - pagination destekli
        // beforeUtc: cursor timestamp (null ise en yeni sayfa);
        // beforeId: aynı timestamp'lı mesajlar için MessageId tie-breaker (opsiyonel);
        // limit: sayfa boyutu (Controller tarafında clamp edilir).
        Task<IDataResult<List<ChatMessageItemDto>>> GetMessagesByThreadAsync(Guid userId, Guid threadId, DateTime? beforeUtc, Guid? beforeId, int limit = 30);

        // Randevu thread'i için mesajları getir (geriye dönük uyumluluk için) - pagination destekli
        Task<IDataResult<List<ChatMessageItemDto>>> GetMessagesAsync(Guid userId, Guid appointmentId, DateTime? beforeUtc, Guid? beforeId, int limit = 30);

        Task<IDataResult<int>> GetUnreadTotalAsync(Guid userId);
        
        // Favori thread oluştur veya güncelle
        // storeId: Store bazlı favori thread'leri için StoreId (nullable - diğer favori thread'leri için null)
        Task<IDataResult<Guid>> EnsureFavoriteThreadAsync(Guid fromUserId, Guid toUserId, Guid? storeId = null);
        Task PushAppointmentThreadCreatedAsync(Guid appointmentId);
        Task PushAppointmentThreadUpdatedAsync(Guid appointmentId);
        Task PushFavoriteThreadUpdatedAsync(Guid fromUserId, Guid toUserId, Guid threadId);
        
        // Typing indicator gönder
        Task<IDataResult<bool>> NotifyTypingAsync(Guid userId, Guid threadId, bool isTyping);

        /// <summary>Admin için tüm chat thread'lerini getir.</summary>
        Task<IDataResult<List<ChatThreadListItemDto>>> GetAllThreadsForAdminAsync();
    }
}
