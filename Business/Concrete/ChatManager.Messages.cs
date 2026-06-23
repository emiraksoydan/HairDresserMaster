using Business.Abstract;
using Business.BusinessAspect.Autofac;

using Business.Helpers;
using Business.Resources;
using Core.Aspect.Autofac.ExceptionHandling;
using Core.Aspect.Autofac.Logging;
using Core.Aspect.Autofac.Transaction;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;
using Entities.Concrete.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Business.Concrete
{
    public partial class ChatManager
    {
        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        // Read-only query - no transaction needed
        public async Task<IDataResult<List<ChatMessageItemDto>>> GetMessagesAsync(
            Guid userId, Guid appointmentId, DateTime? beforeUtc, Guid? beforeId, int limit = 30)
        {

            // Performance: Use repository instead of direct DbContext access
            var appt = await appointmentDal.Get(x => x.Id == appointmentId);
            if (appt is null) return new ErrorDataResult<List<ChatMessageItemDto>>(Messages.AppointmentNotFound);

            // sadece Pending + Approved sohbet gösterimi
            if (appt.Status is not (AppointmentStatus.Pending or AppointmentStatus.Approved))
                return new ErrorDataResult<List<ChatMessageItemDto>>(Messages.ChatOnlyForActiveAppointments);

            // katılımcı mı?
            // Performance: Use repository instead of direct DbContext access
            var thread = await threadDal.Get(t => t.AppointmentId == appointmentId);
            if (thread is null) return new SuccessDataResult<List<ChatMessageItemDto>>();

            var isParticipant =
                thread.CustomerUserId == userId || thread.StoreOwnerUserId == userId || thread.FreeBarberUserId == userId;

            if (!isParticipant) return new ErrorDataResult<List<ChatMessageItemDto>>(Messages.NotAParticipant);

            var allParticipantIds2 = new List<Guid>();
            if (thread.CustomerUserId.HasValue) allParticipantIds2.Add(thread.CustomerUserId.Value);
            if (thread.StoreOwnerUserId.HasValue) allParticipantIds2.Add(thread.StoreOwnerUserId.Value);
            if (thread.FreeBarberUserId.HasValue) allParticipantIds2.Add(thread.FreeBarberUserId.Value);

            var msgs = await messageDal.GetMessagesByThreadIdWithReadStatusAsync(thread.Id, beforeUtc, beforeId, allParticipantIds2, userId, limit);

            // DB'den okunan mesajları decrypt et
            foreach (var m in msgs)
            {
                m.Text = messageEncryption.Decrypt(m.Text);
                if (m.MediaUrl != null && m.MessageType != (int)Entities.Concrete.Entities.ChatMessageType.Text)
                    m.MediaUrl = messageEncryption.Decrypt(m.MediaUrl);
                if (!string.IsNullOrEmpty(m.ReplyToTextPreview))
                    m.ReplyToTextPreview = messageEncryption.Decrypt(m.ReplyToTextPreview);
                if (m.MessageType == (int)Entities.Concrete.Entities.ChatMessageType.File)
                    m.FileName = m.Text;
            }

            return new SuccessDataResult<List<ChatMessageItemDto>>(msgs);
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        public async Task<IDataResult<ChatMessageDto>> SendFavoriteMessageAsync(Guid senderUserId, Guid threadId, string text, Guid? replyToMessageId = null)
        {
            text = (text ?? "").Trim();
            if (text.Length == 0) return new ErrorDataResult<ChatMessageDto>(Messages.EmptyMessage);

            var moderationCheck = await contentModeration.CheckContentAsync(text);
            if (!moderationCheck.Success)
                return new ErrorDataResult<ChatMessageDto>(moderationCheck.Message);

            var thread = await threadDal.Get(t => t.Id == threadId);
            if (thread is null) return new ErrorDataResult<ChatMessageDto>(Messages.ChatNotFound);

            // Favori thread kontrolü
            if (thread.AppointmentId.HasValue) return new ErrorDataResult<ChatMessageDto>(Messages.MethodOnlyForFavoriteThreads);

            // Katılımcı kontrolü
            var isParticipant = (thread.FavoriteFromUserId == senderUserId || thread.FavoriteToUserId == senderUserId);
            if (!isParticipant) return new ErrorDataResult<ChatMessageDto>(Messages.NotAParticipant);

            // Kural: Mesaj gönderebilmek için SENDER'IN KENDİSİNİN karşı tarafa aktif favorisi olmalı.
            // Karşı tarafın favori yapıp yapmaması gönderme iznini etkilemez.
            var fromUserId = thread.FavoriteFromUserId!.Value;
            var toUserId = thread.FavoriteToUserId!.Value;
            var otherParticipantId = fromUserId == senderUserId ? toUserId : fromUserId;

            if (!thread.IsSocialThread)
            {
                bool senderHasFavorite = await HasActiveFavoriteFromUserAsync(senderUserId, otherParticipantId);
                if (!senderHasFavorite)
                    return new ErrorDataResult<ChatMessageDto>(Messages.FavoriteRequiredToSend);
            }

            // Reply: varsa alıntı metnini al
            var replyPreview = await BuildReplyPreviewAsync(replyToMessageId);

            // Mesaj metnini şifrele (DB'ye kaydedilecek)
            var encryptedText = messageEncryption.Encrypt(text);
            var previewText = text.Length > 60 ? text[..60] : text;
            var encryptedPreview = messageEncryption.Encrypt(previewText);
            var encryptedReplyPreview = string.IsNullOrEmpty(replyPreview) ? null : messageEncryption.Encrypt(replyPreview);

            var msg = new ChatMessage
            {
                Id = Guid.NewGuid(),
                ThreadId = thread.Id,
                AppointmentId = null, // Favori thread'de AppointmentId null
                SenderUserId = senderUserId,
                Text = encryptedText,
                IsSystem = false,
                ReplyToMessageId = replyToMessageId,
                ReplyToTextPreview = encryptedReplyPreview,
                CreatedAt = DateTime.UtcNow
            };
            await messageDal.Add(msg);

            thread.LastMessageAt = msg.CreatedAt;
            thread.LastMessagePreview = encryptedPreview;
            thread.UpdatedAt = DateTime.UtcNow;

            // Unread count artır (sender dışındaki katılımcıya)
            var otherUserId = thread.FavoriteFromUserId == senderUserId ? thread.FavoriteToUserId : thread.FavoriteFromUserId;

            if (otherUserId.HasValue)
            {
                // Thread'deki user mapping'leri kullan (EnsureFavoriteThreadAsync'te set edilmiş)
                if (thread.CustomerUserId == otherUserId) thread.CustomerUnreadCount++;
                else if (thread.StoreOwnerUserId == otherUserId) thread.StoreUnreadCount++;
                else if (thread.FreeBarberUserId == otherUserId) thread.FreeBarberUnreadCount++;
            }

            await threadDal.Update(thread);

            // SignalR DTO'su plaintext kullanır (TLS ile korunur)
            var dto = new ChatMessageDto
            {
                ThreadId = thread.Id,
                MessageId = msg.Id,
                SenderUserId = senderUserId,
                Text = text,
                CreatedAt = msg.CreatedAt,
                ReplyToMessageId = replyToMessageId,
                ReplyToTextPreview = replyPreview
            };

            // B8: Push -> katılımcıları favori kısıtlamasına göre grupla ve tek round-trip ile gönder
            var favoriteRecipients = new HashSet<Guid> { senderUserId };
            if (otherUserId.HasValue) favoriteRecipients.Add(otherUserId.Value);

            var unrestricted = new List<Guid>();
            var restricted = new List<Guid>();
            if (thread.IsSocialThread)
            {
                await realtime.PushChatMessageToUsersAsync(favoriteRecipients.ToList(), dto);
                await PushSocialThreadUpdatedAsync(fromUserId, toUserId, thread.Id);
            }
            else
            {
                foreach (var recipientId in favoriteRecipients)
                {
                    if (recipientId == senderUserId) { unrestricted.Add(recipientId); continue; }
                    if (await HasActiveFavoriteFromUserAsync(recipientId, senderUserId))
                        unrestricted.Add(recipientId);
                    else
                        restricted.Add(recipientId);
                }
                if (unrestricted.Count > 0)
                    await realtime.PushChatMessageToUsersAsync(unrestricted, dto);
                if (restricted.Count > 0)
                    await realtime.PushChatMessageToUsersAsync(restricted, MaskChatMessageForFavoriteRecipientWithoutMutualFavorite(dto));

                await PushFavoriteThreadUpdatedAsync(fromUserId, toUserId, thread.Id);
            }

            // Badge update: sender dışındaki katılımcı için badge count'u güncelle
            if (otherUserId.HasValue && otherUserId.Value != senderUserId)
            {
                await badgeService.NotifyBadgeChangeAsync(otherUserId.Value, BadgeChangeReason.MessageReceived);
            }

            if (thread.IsSocialThread)
            {
                await PushSocialChatNotificationForFirstUnreadAsync(thread, senderUserId, text);
            }
            else
            {
                await PushChatNotificationForFirstUnreadAsync(
                    thread,
                    senderUserId,
                    text,
                    "Yeni mesaj",
                    null);
            }

            await auditService.RecordAsync(AuditAction.ChatMessageSentFavoriteThread, senderUserId, msg.Id, thread.Id, true);

            return new SuccessDataResult<ChatMessageDto>(dto);
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        public async Task<IDataResult<bool>> MarkThreadReadAsync(Guid userId, Guid threadId)
        {
            var thread = await threadDal.Get(t => t.Id == threadId);
            if (thread is null) return new ErrorDataResult<bool>(false, Messages.ChatNotFound);

            // Randevu thread'i için
            if (thread.AppointmentId.HasValue)
            {
                if (thread.CustomerUserId == userId) thread.CustomerUnreadCount = 0;
                else if (thread.StoreOwnerUserId == userId) thread.StoreUnreadCount = 0;
                else if (thread.FreeBarberUserId == userId) thread.FreeBarberUnreadCount = 0;
                else return new ErrorDataResult<bool>(false, Messages.ParticipantNotFound);
            }
            // Favori thread için
            else if (thread.FavoriteFromUserId.HasValue && thread.FavoriteToUserId.HasValue)
            {
                if (!thread.IsSocialThread)
                {
                    var favoriteOtherUserId = thread.FavoriteFromUserId == userId
                        ? thread.FavoriteToUserId!.Value
                        : thread.FavoriteFromUserId!.Value;
                    bool userHasFavorite = await HasActiveFavoriteFromUserAsync(userId, favoriteOtherUserId);
                    if (!userHasFavorite)
                        return new SuccessDataResult<bool>(true);
                }

                if (thread.FavoriteFromUserId == userId || thread.FavoriteToUserId == userId)
                {
                    if (thread.CustomerUserId == userId) thread.CustomerUnreadCount = 0;
                    else if (thread.StoreOwnerUserId == userId) thread.StoreUnreadCount = 0;
                    else if (thread.FreeBarberUserId == userId) thread.FreeBarberUnreadCount = 0;
                }
                else return new ErrorDataResult<bool>(false, Messages.ParticipantNotFound);
            }

            await threadDal.Update(thread);

            // Read receipt'leri işle ve çift tik bildirimi gönder
            await ProcessReadReceiptsAsync(thread, userId);

            // Badge count'u güncelle (normal + sosyal ayrı)
            await badgeService.NotifyBadgeChangeAsync(userId, BadgeChangeReason.MessageRead);

            return new SuccessDataResult<bool>(true);
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        // Read-only query - no transaction needed
        public async Task<IDataResult<List<ChatMessageItemDto>>> GetMessagesByThreadAsync(Guid userId, Guid threadId, DateTime? beforeUtc, Guid? beforeId, int limit = 30)
        {
            var thread = await threadDal.Get(t => t.Id == threadId);
            if (thread is null) return new ErrorDataResult<List<ChatMessageItemDto>>(Messages.ChatNotFound);

            // Favori thread: karşı tarafı favoriye almamış kullanıcıya sadece kendi gönderdiği mesajlar (GetThreads ile tutarlı)
            bool filterMessagesToOutgoingOnly = false;

            // Katılımcı kontrolü
            bool isParticipant = false;
            if (thread.AppointmentId.HasValue)
            {
                // Randevu thread'i
                var appt = await appointmentDal.Get(x => x.Id == thread.AppointmentId.Value);
                if (appt is null) return new ErrorDataResult<List<ChatMessageItemDto>>(Messages.AppointmentNotFound);

                if (appt.Status is not (AppointmentStatus.Pending or AppointmentStatus.Approved))
                    return new ErrorDataResult<List<ChatMessageItemDto>>(Messages.ChatOnlyForActiveAppointments);

                isParticipant = thread.CustomerUserId == userId || thread.StoreOwnerUserId == userId || thread.FreeBarberUserId == userId;
            }
            else if (thread.IsSocialThread && thread.FavoriteFromUserId.HasValue && thread.FavoriteToUserId.HasValue)
            {
                isParticipant = thread.FavoriteFromUserId == userId || thread.FavoriteToUserId == userId;
            }
            else if (thread.FavoriteFromUserId.HasValue && thread.FavoriteToUserId.HasValue)
            {
                // Favori thread
                isParticipant = thread.FavoriteFromUserId == userId || thread.FavoriteToUserId == userId;

                // Favori aktif mi kontrolü - en az bir tarafın favori olması yeterli
                // Store bazlı thread'ler için: StoreId ile favori kontrolü yapılır
                // Diğer thread'ler için: User ID bazlı favori kontrolü yapılır
                var fromUserId = thread.FavoriteFromUserId.Value;
                var toUserId = thread.FavoriteToUserId.Value;

                bool isFavoriteActive = false;

                // Store bazlı thread'ler için StoreId ile favori kontrolü
                if (thread.StoreId.HasValue)
                {
                    // Store bazlı favori kontrolü: StoreId ile kontrol yap
                    // fromUserId -> StoreId yönünde
                    var favorite1Store = await favoriteDal.Get(x => x.FavoritedFromId == fromUserId && x.FavoritedToId == thread.StoreId.Value && x.IsActive);
                    if (favorite1Store != null)
                    {
                        isFavoriteActive = true;
                    }
                    // Ek kural: Karşı taraf userId bazlı favori yaptıysa thread görünür kalmalı
                    if (!isFavoriteActive)
                    {
                        var favUser1 = await favoriteDal.GetByUsersAsync(fromUserId, toUserId);
                        if (favUser1 != null && favUser1.IsActive) isFavoriteActive = true;
                    }
                    if (!isFavoriteActive)
                    {
                        var favUser2 = await favoriteDal.GetByUsersAsync(toUserId, fromUserId);
                        if (favUser2 != null && favUser2.IsActive) isFavoriteActive = true;
                    }
                }
                else
                {
                    // Store bazlı değil, User ID bazlı favori kontrolü
                    // 1. fromUserId -> toUserId yönünde
                    var favorite1 = await favoriteDal.GetByUsersAsync(fromUserId, toUserId);
                    if (favorite1 != null && favorite1.IsActive)
                    {
                        isFavoriteActive = true;
                    }

                    // 2. toUserId -> fromUserId yönünde
                    if (!isFavoriteActive)
                    {
                        var favorite2 = await favoriteDal.GetByUsersAsync(toUserId, fromUserId);
                        if (favorite2 != null && favorite2.IsActive)
                        {
                            isFavoriteActive = true;
                        }
                    }

                    // 3. Mağaza bazlı favori (thread'de StoreId null; FavoritedToId = mağaza Id — GetThreads ile aynı mantık)
                    if (!isFavoriteActive)
                    {
                        var storesOfTo = await barberStoreDal.GetAll(x => x.BarberStoreOwnerId == toUserId);
                        if (storesOfTo.Count > 0)
                        {
                            var storeIds = storesOfTo.Select(s => s.Id).ToList();
                            var favStores = await favoriteDal.GetAll(x =>
                                x.FavoritedFromId == fromUserId &&
                                storeIds.Contains(x.FavoritedToId) &&
                                x.IsActive);
                            if (favStores.Count > 0) isFavoriteActive = true;
                        }
                    }
                    if (!isFavoriteActive)
                    {
                        var storesOfFrom = await barberStoreDal.GetAll(x => x.BarberStoreOwnerId == fromUserId);
                        if (storesOfFrom.Count > 0)
                        {
                            var storeIds = storesOfFrom.Select(s => s.Id).ToList();
                            var favStores = await favoriteDal.GetAll(x =>
                                x.FavoritedFromId == toUserId &&
                                storeIds.Contains(x.FavoritedToId) &&
                                x.IsActive);
                            if (favStores.Count > 0) isFavoriteActive = true;
                        }
                    }
                }

                if (!isFavoriteActive)
                    return new ErrorDataResult<List<ChatMessageItemDto>>(Messages.FavoriteNotActiveForMessages);

                // Kısıtlı kullanıcı: karşı tarafı favoriye almamışsa API hata yerine sadece kendi gönderilen mesajları döndür
                var msgOtherUserId = thread.FavoriteFromUserId!.Value == userId
                    ? thread.FavoriteToUserId!.Value
                    : thread.FavoriteFromUserId!.Value;
                bool currentUserHasFavorite = await HasActiveFavoriteFromUserAsync(userId, msgOtherUserId);
                if (!currentUserHasFavorite)
                    filterMessagesToOutgoingOnly = true;
            }

            if (!isParticipant) return new ErrorDataResult<List<ChatMessageItemDto>>(Messages.NotAParticipant);

            // Tüm katılımcı ID'lerini toparla (isFullyRead hesabı için)
            var allParticipantIds = new List<Guid>();
            if (thread.CustomerUserId.HasValue) allParticipantIds.Add(thread.CustomerUserId.Value);
            if (thread.StoreOwnerUserId.HasValue) allParticipantIds.Add(thread.StoreOwnerUserId.Value);
            if (thread.FreeBarberUserId.HasValue) allParticipantIds.Add(thread.FreeBarberUserId.Value);
            // Favori thread: CustomerUserId vb. tek kolonda iki kişi tutulmayabiliyor; okundu hesabı için her iki tarafı ekle
            if (thread.FavoriteFromUserId.HasValue && thread.FavoriteToUserId.HasValue)
            {
                if (!allParticipantIds.Contains(thread.FavoriteFromUserId.Value))
                    allParticipantIds.Add(thread.FavoriteFromUserId.Value);
                if (!allParticipantIds.Contains(thread.FavoriteToUserId.Value))
                    allParticipantIds.Add(thread.FavoriteToUserId.Value);
            }

            var msgs = await messageDal.GetMessagesByThreadIdWithReadStatusAsync(threadId, beforeUtc, beforeId, allParticipantIds, userId, limit);

            // DB'den okunan mesajları decrypt et
            foreach (var m in msgs)
            {
                m.Text = messageEncryption.Decrypt(m.Text);
                if (m.MediaUrl != null && m.MessageType != (int)Entities.Concrete.Entities.ChatMessageType.Text)
                    m.MediaUrl = messageEncryption.Decrypt(m.MediaUrl);
                if (!string.IsNullOrEmpty(m.ReplyToTextPreview))
                    m.ReplyToTextPreview = messageEncryption.Decrypt(m.ReplyToTextPreview);
                if (m.MessageType == (int)Entities.Concrete.Entities.ChatMessageType.File)
                    m.FileName = m.Text;
            }

            var rawCount = msgs.Count;
            if (filterMessagesToOutgoingOnly)
                msgs = msgs.Where(m => m.SenderUserId == userId).ToList();

            logger.LogInformation(
                "[GetMessagesByThread] threadId={ThreadId} userId={UserId} appointmentId={ApptId} favFrom={FavFrom} favTo={FavTo} storeIdOnThread={StoreId} filterOutgoingOnly={FilterOut} rawMsgCount={Raw} finalCount={Final}",
                threadId,
                userId,
                thread.AppointmentId,
                thread.FavoriteFromUserId,
                thread.FavoriteToUserId,
                thread.StoreId,
                filterMessagesToOutgoingOnly,
                rawCount,
                msgs.Count);

            return new SuccessDataResult<List<ChatMessageItemDto>>(msgs);
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        [ExceptionHandlingAspect]
        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        public async Task<IDataResult<Guid>> EnsureFavoriteThreadAsync(Guid fromUserId, Guid toUserId, Guid? storeId = null)
        {
            // REVIZE: StoreId parametresi artık kullanılmıyor - User ID bazlı tek thread olmalı
            // Mevcut thread'i kontrol et (her iki yönde, StoreId null ile)
            var existingThread = await threadDal.GetFavoriteThreadAsync(fromUserId, toUserId, storeId: null);

            ChatThread thread;
            bool isNewThread = false;

            if (existingThread != null)
            {
                thread = existingThread;
                if (storeId.HasValue && thread.FavoriteContextStoreId != storeId.Value)
                {
                    thread.FavoriteContextStoreId = storeId.Value;
                    thread.UpdatedAt = DateTime.UtcNow;
                    await threadDal.Update(thread);
                }
            }
            else
            {
                // Yeni thread oluştur
                var fromUser = await userDal.Get(u => u.Id == fromUserId);
                var toUser = await userDal.Get(u => u.Id == toUserId);

                if (fromUser == null || toUser == null)
                    return new ErrorDataResult<Guid>(Messages.UserNotFound);

                thread = new ChatThread
                {
                    Id = Guid.NewGuid(),
                    AppointmentId = null,
                    FavoriteFromUserId = fromUserId,
                    FavoriteToUserId = toUserId,
                    StoreId = null, // REVIZE: StoreId null - User ID bazlı tek thread (birden fazla dükkan favorilense bile)
                    FavoriteContextStoreId = storeId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                // Her iki kullanıcının UserType'ına göre CustomerUserId, StoreOwnerUserId veya FreeBarberUserId'yi set et
                if (fromUser.UserType == UserType.Customer)
                    thread.CustomerUserId = fromUserId;
                else if (fromUser.UserType == UserType.BarberStore)
                    thread.StoreOwnerUserId = fromUserId;
                else if (fromUser.UserType == UserType.FreeBarber)
                    thread.FreeBarberUserId = fromUserId;

                if (toUser.UserType == UserType.Customer && thread.CustomerUserId != toUserId)
                    thread.CustomerUserId = toUserId;
                else if (toUser.UserType == UserType.BarberStore && thread.StoreOwnerUserId != toUserId)
                    thread.StoreOwnerUserId = toUserId;
                else if (toUser.UserType == UserType.FreeBarber && thread.FreeBarberUserId != toUserId)
                    thread.FreeBarberUserId = toUserId;

                await threadDal.Add(thread);
                isNewThread = true;
            }

            // REVIZE: Aktif favori kontrolü - en az bir tarafın favori aktif olmalı
            // Artık User ID bazlı kontrol yapılır (Store bazlı thread'ler için de)
            // Store bazlı favoriler için: StoreId -> Store Owner User ID kontrolü yapılır
            // FreeBarber/Customer için: User ID -> User ID kontrolü yapılır
            // ÖNEMLİ: Transaction commit edilmeden önce bu metod çağrılıyor olabilir (FavoriteManager'dan),
            // bu yüzden favori henüz DB'de görünmeyebilir.

            bool isFavoriteActive = false;

            // REVIZE: User ID bazlı favori kontrolü (her iki yönde)
            // 1. fromUserId -> toUserId yönünde (User ID bazlı veya Store bazlı)
            var favorite1 = await favoriteDal.GetByUsersAsync(fromUserId, toUserId);
            if (favorite1 != null && favorite1.IsActive)
            {
                isFavoriteActive = true;
            }

            // 2. toUserId -> fromUserId yönünde (User ID bazlı veya Store bazlı)
            if (!isFavoriteActive)
            {
                var favorite2 = await favoriteDal.GetByUsersAsync(toUserId, fromUserId);
                if (favorite2 != null && favorite2.IsActive)
                {
                    isFavoriteActive = true;
                }
            }

            // 3. Store bazlı favoriler için kontrol (StoreId -> Store Owner User ID)
            // fromUserId'nin toUserId'nin store'larından birini favoriye eklemiş olabilir
            if (!isFavoriteActive)
            {
                // toUserId'nin store'larını bul
                var stores = await barberStoreDal.GetAll(x => x.BarberStoreOwnerId == toUserId);
                foreach (var store in stores)
                {
                    var favoriteStore = await favoriteDal.Get(x => x.FavoritedFromId == fromUserId && x.FavoritedToId == store.Id && x.IsActive);
                    if (favoriteStore != null)
                    {
                        isFavoriteActive = true;
                        break;
                    }
                }
            }

            // 4. toUserId'nin fromUserId'nin store'larından birini favoriye eklemiş olabilir
            if (!isFavoriteActive)
            {
                // fromUserId'nin store'larını bul
                var stores = await barberStoreDal.GetAll(x => x.BarberStoreOwnerId == fromUserId);
                foreach (var store in stores)
                {
                    var favoriteStore = await favoriteDal.Get(x => x.FavoritedFromId == toUserId && x.FavoritedToId == store.Id && x.IsActive);
                    if (favoriteStore != null)
                    {
                        isFavoriteActive = true;
                        break;
                    }
                }
            }

            // Eğer hiçbir tarafın favori aktif değilse thread gönderme (DB'de kalabilir ama SignalR ile gönderme)
            if (!isFavoriteActive)
            {
                // Thread DB'de kalabilir ama görünür olmamalı (GetThreadsAsync'te zaten filtreleniyor)
                // SignalR ile thread göndermiyoruz
                return new SuccessDataResult<Guid>(thread.Id);
            }

            // Her iki kullanıcı için de thread detaylarını al ve SignalR ile gönder
            // GetThreadsAsync mantığını kullanarak thread detaylarını doldur
            var recipients = new[] { fromUserId, toUserId }.Distinct().ToList();

            foreach (var recipientUserId in recipients)
            {
                // ExceptionHandlingAspect method seviyesinde exception'ları handle eder
                // Burada try-catch'e gerek yok, aspect otomatik handle edecek
                string displayName = "";
                string? imageUrl = null;
                BarberType? barberType = null;
                Guid participantUserId = Guid.Empty;
                UserType participantUserType;

                // REVIZE: Thread bilgisinde dükkan/serbest berber panel bilgileri gösterilsin
                // Store bazlı favoriler için: En az bir dükkanı favoriye almışsa o dükkanın bilgileri gösterilsin
                // FreeBarber için: Panel bilgileri gösterilsin
                var otherUserId = thread.FavoriteFromUserId == recipientUserId
                    ? thread.FavoriteToUserId!.Value
                    : thread.FavoriteFromUserId!.Value;

                var otherUser = await userDal.Get(u => u.Id == otherUserId);
                if (otherUser == null) continue;

                participantUserId = otherUser.Id;
                Guid? favoriteStoreId = null;

                if (otherUser.UserType == UserType.Customer)
                {
                    displayName = $"{otherUser.FirstName} {otherUser.LastName}";
                    if (otherUser.ImageId.HasValue)
                    {
                        var img = await imageDal.GetLatestImageAsync(otherUser.Id, ImageOwnerType.User);
                        imageUrl = img?.ImageUrl;
                    }
                    barberType = null;
                    participantUserType = UserType.Customer;
                }
                else if (otherUser.UserType == UserType.BarberStore)
                {
                    // REVIZE: Store sahibi için - en az bir dükkanı favoriye almışsa o dükkanın bilgileri gösterilsin
                    // recipientUserId'nin otherUserId'nin store'larından birini favoriye eklemiş olabilir
                    BarberStore? store = null;
                    if (thread.FavoriteContextStoreId.HasValue)
                        store = await barberStoreDal.Get(x => x.Id == thread.FavoriteContextStoreId.Value && x.BarberStoreOwnerId == otherUserId);
                    if (store == null)
                    {
                        var favoriteStores = await favoriteDal.GetAll(x => x.FavoritedFromId == recipientUserId && x.IsActive);
                        var storeIds = favoriteStores.Select(f => f.FavoritedToId).ToList();
                        var stores = await barberStoreDal.GetAll(x => storeIds.Contains(x.Id) && x.BarberStoreOwnerId == otherUserId);
                        store = stores.FirstOrDefault();
                    }
                    if (store != null)
                    {
                        displayName = store.StoreName;
                        barberType = store.Type;
                        var img = await imageDal.GetLatestImageAsync(store.Id, ImageOwnerType.Store);
                        imageUrl = img?.ImageUrl;
                        favoriteStoreId = store.Id;
                    }
                    else
                    {
                        // Favori store yoksa, ilk store'u göster (fallback)
                        var firstStore = await barberStoreDal.Get(x => x.BarberStoreOwnerId == otherUserId);
                        if (firstStore != null)
                        {
                            displayName = firstStore.StoreName;
                            barberType = firstStore.Type;
                            var img = await imageDal.GetLatestImageAsync(firstStore.Id, ImageOwnerType.Store);
                            imageUrl = img?.ImageUrl;
                            favoriteStoreId = firstStore.Id;
                        }
                    }
                    participantUserType = UserType.BarberStore;
                }
                else if (otherUser.UserType == UserType.FreeBarber)
                {
                    // REVIZE: FreeBarber için panel bilgileri gösterilsin
                    var freeBarber = await freeBarberDal.Get(x => x.FreeBarberUserId == otherUserId);
                    if (freeBarber != null)
                    {
                        displayName = $"{freeBarber.FirstName} {freeBarber.LastName}";
                        barberType = freeBarber.Type;
                        var img = await imageDal.GetLatestImageAsync(freeBarber.Id, ImageOwnerType.FreeBarber);
                        imageUrl = img?.ImageUrl;
                    }
                    participantUserType = UserType.FreeBarber;
                }
                else
                {
                    continue; // Beklenmeyen durum
                }

                // UnreadCount'u thread entity'den al
                int unreadCount = 0;
                if (thread.CustomerUserId == recipientUserId)
                    unreadCount = thread.CustomerUnreadCount;
                else if (thread.StoreOwnerUserId == recipientUserId)
                    unreadCount = thread.StoreUnreadCount;
                else if (thread.FreeBarberUserId == recipientUserId)
                    unreadCount = thread.FreeBarberUnreadCount;

                // Mevcut kullanıcının (recipientUserId) profil resmini al
                string? currentUserImageUrlForRecipient = null;
                var recipientUser = await userDal.Get(u => u.Id == recipientUserId);
                if (recipientUser != null)
                {
                    if (recipientUser.UserType == UserType.Customer)
                    {
                        var userImg = await imageDal.GetLatestImageAsync(recipientUser.Id, ImageOwnerType.User);
                        currentUserImageUrlForRecipient = userImg?.ImageUrl;
                    }
                    else if (recipientUser.UserType == UserType.BarberStore)
                    {
                        var userStore = await barberStoreDal.Get(x => x.BarberStoreOwnerId == recipientUserId);
                        if (userStore != null)
                        {
                            var storeImg = await imageDal.GetLatestImageAsync(userStore.Id, ImageOwnerType.Store);
                            currentUserImageUrlForRecipient = storeImg?.ImageUrl;
                        }
                    }
                    else if (recipientUser.UserType == UserType.FreeBarber)
                    {
                        var userFreeBarber = await freeBarberDal.Get(x => x.FreeBarberUserId == recipientUserId);
                        if (userFreeBarber != null)
                        {
                            var freeBarberImg = await imageDal.GetLatestImageAsync(userFreeBarber.Id, ImageOwnerType.FreeBarber);
                            currentUserImageUrlForRecipient = freeBarberImg?.ImageUrl;
                        }
                    }
                }

                // Alıcının karşı tarafa aktif favorisi var mı? (kısıtlı mı değil mi?)
                bool isRestrictedForRecipient = !await HasActiveFavoriteFromUserAsync(recipientUserId, otherUserId);

                var threadDto = new ChatThreadListItemDto
                {
                    ThreadId = thread.Id,
                    AppointmentId = null,
                    Status = null,
                    IsFavoriteThread = true,
                    Title = displayName,
                    LastMessagePreview = isRestrictedForRecipient ? null : DecryptOptionalPreview(thread.LastMessagePreview),
                    LastMessageAt = thread.LastMessageAt,
                    UnreadCount = unreadCount,
                    CurrentUserImageUrl = currentUserImageUrlForRecipient,
                    IsRestrictedForCurrentUser = isRestrictedForRecipient,
                    FavoriteStoreId = favoriteStoreId,
                    Participants = new List<ChatThreadParticipantDto>
                    {
                        new ChatThreadParticipantDto
                        {
                            UserId = participantUserId,
                            DisplayName = displayName,
                            ImageUrl = imageUrl,
                            UserType = participantUserType,
                            BarberType = barberType
                        }
                    }
                };

                // Thread oluşturulduğunda veya güncellendiğinde SignalR ile bildir
                if (isNewThread)
                    await realtime.PushChatThreadCreatedAsync(recipientUserId, threadDto);
                else
                    await realtime.PushChatThreadUpdatedAsync(recipientUserId, threadDto);
            }

            return new SuccessDataResult<Guid>(thread.Id);
        }

    }
}
