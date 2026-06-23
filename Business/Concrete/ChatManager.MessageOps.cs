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

        // ─────────────────────────────────────────────────────────────────────
        // MEDIA MESSAGE (Image / Location)
        // ─────────────────────────────────────────────────────────────────────

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        public async Task<IDataResult<ChatMessageDto>> SendMediaMessageAsync(
            Guid senderUserId, Guid threadId, int messageType, string mediaUrl, Guid? replyToMessageId = null,
            string? fileName = null)
        {
            if (!Enum.IsDefined(typeof(Entities.Concrete.Entities.ChatMessageType), messageType))
                return new ErrorDataResult<ChatMessageDto>(Messages.ChatInvalidMessageType);
            var msgType = (Entities.Concrete.Entities.ChatMessageType)messageType;
            if (msgType == Entities.Concrete.Entities.ChatMessageType.Text)
                return new ErrorDataResult<ChatMessageDto>(Messages.ChatTextMessagesWrongEndpoint);

            // B5: MediaUrl validasyonu (scheme, length, location JSON)
            var (urlOk, urlErr) = ValidateMediaUrl(msgType, mediaUrl);
            if (!urlOk) return new ErrorDataResult<ChatMessageDto>(urlErr!);

            // B5: FileName sanitize (File tipinde kullanılacak)
            var sanitizedFileName = msgType == Entities.Concrete.Entities.ChatMessageType.File
                ? SanitizeFileName(fileName)
                : null;

            // B7: File ismi için content moderation (Image/Audio binary; ileride image moderation ayrıca yapılabilir)
            if (!string.IsNullOrWhiteSpace(sanitizedFileName))
            {
                var fileNameModeration = await contentModeration.CheckContentAsync(sanitizedFileName);
                if (!fileNameModeration.Success)
                    return new ErrorDataResult<ChatMessageDto>(fileNameModeration.Message);
            }

            var thread = await threadDal.Get(t => t.Id == threadId);
            if (thread is null) return new ErrorDataResult<ChatMessageDto>(Messages.ChatNotFound);

            var isParticipant =
                thread.CustomerUserId == senderUserId ||
                thread.StoreOwnerUserId == senderUserId ||
                thread.FreeBarberUserId == senderUserId ||
                thread.FavoriteFromUserId == senderUserId ||
                thread.FavoriteToUserId == senderUserId;

            if (!isParticipant) return new ErrorDataResult<ChatMessageDto>(Messages.NotAParticipant);

            // Favori thread ise: media göndermek için de sender'ın aktif favorisi olmalı (sosyal thread hariç)
            if (!thread.IsSocialThread && thread.FavoriteFromUserId.HasValue && thread.FavoriteToUserId.HasValue)
            {
                var mediaOtherUserId = thread.FavoriteFromUserId == senderUserId
                    ? thread.FavoriteToUserId!.Value
                    : thread.FavoriteFromUserId!.Value;
                bool senderHasFavorite = await HasActiveFavoriteFromUserAsync(senderUserId, mediaOtherUserId);
                if (!senderHasFavorite)
                    return new ErrorDataResult<ChatMessageDto>(Messages.FavoriteRequiredToSend);
            }

            // B9: ReplyToTextPreview helper (decrypt + trim), DB'ye encrypted olarak yazılacak
            var replyPreview = await BuildReplyPreviewAsync(replyToMessageId);

            // Text alanı: File için dosya adı, Image için "[Fotoğraf]", Location için "[Konum]", Audio için "[Ses mesajı]"
            var displayText = msgType switch
            {
                Entities.Concrete.Entities.ChatMessageType.Image => "[Fotoğraf]",
                Entities.Concrete.Entities.ChatMessageType.Location => "[Konum]",
                Entities.Concrete.Entities.ChatMessageType.File => sanitizedFileName ?? "[Dosya]",
                Entities.Concrete.Entities.ChatMessageType.Audio => "[Ses mesajı]",
                _ => "[Medya]"
            };

            var encryptedText = messageEncryption.Encrypt(displayText);
            var encryptedMediaUrl = messageEncryption.Encrypt(mediaUrl);
            var encryptedReplyPreview = string.IsNullOrEmpty(replyPreview) ? null : messageEncryption.Encrypt(replyPreview);

            var msg = new ChatMessage
            {
                Id = Guid.NewGuid(),
                ThreadId = threadId,
                AppointmentId = thread.AppointmentId,
                SenderUserId = senderUserId,
                Text = encryptedText,
                IsSystem = false,
                MessageType = msgType,
                MediaUrl = encryptedMediaUrl,
                ReplyToMessageId = replyToMessageId,
                ReplyToTextPreview = encryptedReplyPreview,
                CreatedAt = DateTime.UtcNow
            };
            await messageDal.Add(msg);

            thread.LastMessageAt = msg.CreatedAt;
            thread.LastMessagePreview = encryptedText;
            thread.UpdatedAt = DateTime.UtcNow;

            var recipients = GetThreadParticipants(thread);

            foreach (var uid in recipients)
            {
                if (uid != senderUserId)
                {
                    if (thread.CustomerUserId == uid) thread.CustomerUnreadCount++;
                    else if (thread.StoreOwnerUserId == uid) thread.StoreUnreadCount++;
                    else if (thread.FreeBarberUserId == uid) thread.FreeBarberUnreadCount++;
                }
            }
            await threadDal.Update(thread);

            var dto = new ChatMessageDto
            {
                ThreadId = threadId,
                MessageId = msg.Id,
                SenderUserId = senderUserId,
                Text = displayText,          // plain text to caller
                CreatedAt = msg.CreatedAt,
                MessageType = messageType,
                MediaUrl = mediaUrl,         // plain URL to caller
                ReplyToMessageId = replyToMessageId,
                ReplyToTextPreview = replyPreview
            };

            var isFavoriteOnlyThread = !thread.AppointmentId.HasValue &&
                                      thread.FavoriteFromUserId.HasValue &&
                                      thread.FavoriteToUserId.HasValue;

            // B8: SignalR batch push. Favorite-only thread'de mask gerekebileceğinden maskelenmeyen ve maskelenen alıcılar ayrı gruplanır.
            if (isFavoriteOnlyThread)
            {
                var unrestrictedRecipients = new List<Guid>();
                var restrictedRecipients = new List<Guid>();
                foreach (var uid in recipients)
                {
                    if (uid == senderUserId) { unrestrictedRecipients.Add(uid); continue; }
                    if (await HasActiveFavoriteFromUserAsync(uid, senderUserId))
                        unrestrictedRecipients.Add(uid);
                    else
                        restrictedRecipients.Add(uid);
                }
                if (unrestrictedRecipients.Count > 0)
                    await realtime.PushChatMessageToUsersAsync(unrestrictedRecipients, dto);
                if (restrictedRecipients.Count > 0)
                    await realtime.PushChatMessageToUsersAsync(restrictedRecipients, MaskChatMessageForFavoriteRecipientWithoutMutualFavorite(dto));
            }
            else
            {
                await realtime.PushChatMessageToUsersAsync(recipients, dto);
            }

            var recipientsForBadge = recipients.Where(u => u != senderUserId).ToList();
            if (recipientsForBadge.Any())
                await badgeService.NotifyBadgeChangeBatchAsync(recipientsForBadge, BadgeChangeReason.MessageReceived);

            if (thread.IsSocialThread)
            {
                await PushSocialChatNotificationForFirstUnreadAsync(thread, senderUserId, displayText);
            }
            else
            {
                await PushChatNotificationForFirstUnreadAsync(
                    thread,
                    senderUserId,
                    displayText,
                    "Yeni medya mesajı",
                    thread.AppointmentId);
            }

            // B1: Thread güncellemesini tüm katılımcılara push et (LastMessagePreview, LastMessageAt, UnreadCount değişti)
            if (thread.IsSocialThread)
            {
                await PushSocialThreadUpdatedAsync(thread.FavoriteFromUserId!.Value, thread.FavoriteToUserId!.Value, thread.Id);
            }
            else if (thread.AppointmentId.HasValue)
            {
                await PushAppointmentThreadUpdatedAsync(thread.AppointmentId.Value);
            }
            else if (isFavoriteOnlyThread)
            {
                await PushFavoriteThreadUpdatedAsync(thread.FavoriteFromUserId!.Value, thread.FavoriteToUserId!.Value, thread.Id);
            }

            await auditService.RecordAsync(AuditAction.ChatMediaMessageSent, senderUserId, msg.Id, threadId, true);

            return new SuccessDataResult<ChatMessageDto>(dto);
        }

        // ─────────────────────────────────────────────────────────────────────
        // DELETE MESSAGE (per-user gizleme: yalnızca thread katılımcıları)
        // ─────────────────────────────────────────────────────────────────────

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        public async Task<IResult> DeleteMessageAsync(Guid requestingUserId, Guid messageId)
        {
            var msg = await messageDal.Get(x => x.Id == messageId && !x.IsDeleted);
            if (msg is null) return new ErrorResult(Messages.ChatMessageNotFound);

            var thread = await threadDal.Get(t => t.Id == msg.ThreadId);
            if (thread is null) return new ErrorResult(Messages.ChatThreadNotFound);

            var isParticipant =
                thread.CustomerUserId == requestingUserId ||
                thread.StoreOwnerUserId == requestingUserId ||
                thread.FreeBarberUserId == requestingUserId ||
                thread.FavoriteFromUserId == requestingUserId ||
                thread.FavoriteToUserId == requestingUserId;
            if (!isParticipant) return new ErrorResult(Messages.NotAParticipant);

            await messageDal.AddUserDeletionAsync(messageId, requestingUserId);

            try { await realtime.PushChatMessageRemovedAsync(requestingUserId, msg.ThreadId, messageId); } catch { }

            var allParticipants = GetThreadParticipants(thread);

            await messageDal.CleanupFullyDeletedMessagesAsync(new[] { messageId }, allParticipants);

            // B2: Global olarak silindiyse ve bu thread'in LastMessage'ı ise thread preview'ını yeniden hesapla
            // ve diğer kullanıcılara chat.messageRemoved ile thread güncellemesini bildir
            var refreshed = await messageDal.Get(x => x.Id == messageId);
            if (refreshed != null && refreshed.IsDeleted)
            {
                // Tüm katılımcılara messageRemoved broadcast (zaten cleanup ile global olarak silindi)
                var otherParticipants = allParticipants.Where(u => u != requestingUserId).ToList();
                if (otherParticipants.Count > 0)
                {
                    try { await realtime.PushChatMessageRemovedToUsersAsync(otherParticipants, msg.ThreadId, messageId); } catch { }
                }

                // Eğer thread'in last message'ı buysa preview'ı yeniden hesapla
                if (thread.LastMessageAt.HasValue && msg.CreatedAt == thread.LastMessageAt.Value)
                {
                    var latest = await messageDal.GetQueryable()
                        .AsNoTracking()
                        .Where(m => m.ThreadId == thread.Id && !m.IsDeleted)
                        .OrderByDescending(m => m.CreatedAt)
                        .FirstOrDefaultAsync();

                    if (latest is null)
                    {
                        thread.LastMessagePreview = null;
                        thread.LastMessageAt = null;
                    }
                    else
                    {
                        var decrypted = messageEncryption.Decrypt(latest.Text) ?? string.Empty;
                        if (decrypted.Length > 60) decrypted = decrypted[..60];
                        thread.LastMessagePreview = messageEncryption.Encrypt(decrypted);
                        thread.LastMessageAt = latest.CreatedAt;
                    }
                    thread.UpdatedAt = DateTime.UtcNow;
                    await threadDal.Update(thread);

                    if (thread.AppointmentId.HasValue)
                        await PushAppointmentThreadUpdatedAsync(thread.AppointmentId.Value);
                    else if (thread.FavoriteFromUserId.HasValue && thread.FavoriteToUserId.HasValue)
                        await PushFavoriteThreadUpdatedAsync(thread.FavoriteFromUserId.Value, thread.FavoriteToUserId.Value, thread.Id);
                }
            }

            await auditService.RecordAsync(AuditAction.ChatMessageHiddenForUser, requestingUserId, messageId, msg.ThreadId, true);

            return new SuccessResult(Messages.ChatMessageDeletedSuccess);
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        [ExceptionHandlingAspect]
        public async Task<IResult> DeleteMessageForEveryoneAsync(Guid requestingUserId, Guid messageId)
        {
            var msg = await messageDal.Get(x => x.Id == messageId && !x.IsDeleted);
            if (msg is null) return new ErrorResult(Messages.ChatMessageNotFound);

            // Yalnızca mesaj sahibi herkesten silebilir
            if (msg.SenderUserId != requestingUserId)
                return new ErrorResult(Messages.ChatDeleteForEveryoneOnlyOwn);

            var thread = await threadDal.Get(t => t.Id == msg.ThreadId);
            if (thread is null) return new ErrorResult(Messages.ChatThreadNotFound);

            // Global soft-delete: içerik (admin moderasyonu için) DB'de kalır; kullanıcı sorguları !IsDeleted ile gizler.
            msg.IsDeleted = true;
            msg.DeletedAt = DateTime.UtcNow;
            msg.DeletedByUserId = requestingUserId;
            await messageDal.Update(msg);

            var allParticipants = GetThreadParticipants(thread);
            try { await realtime.PushChatMessageRemovedToUsersAsync(allParticipants, msg.ThreadId, messageId); } catch { }

            // Son mesaj buysa thread preview'ını tazele
            if (thread.LastMessageAt.HasValue && msg.CreatedAt == thread.LastMessageAt.Value)
            {
                var latest = await messageDal.GetQueryable()
                    .AsNoTracking()
                    .Where(m => m.ThreadId == thread.Id && !m.IsDeleted)
                    .OrderByDescending(m => m.CreatedAt)
                    .FirstOrDefaultAsync();

                if (latest is null)
                {
                    thread.LastMessagePreview = null;
                    thread.LastMessageAt = null;
                }
                else
                {
                    var decrypted = messageEncryption.Decrypt(latest.Text) ?? string.Empty;
                    if (decrypted.Length > 60) decrypted = decrypted[..60];
                    thread.LastMessagePreview = messageEncryption.Encrypt(decrypted);
                    thread.LastMessageAt = latest.CreatedAt;
                }
                thread.UpdatedAt = DateTime.UtcNow;
                await threadDal.Update(thread);

                if (thread.AppointmentId.HasValue)
                    await PushAppointmentThreadUpdatedAsync(thread.AppointmentId.Value);
                else if (thread.FavoriteFromUserId.HasValue && thread.FavoriteToUserId.HasValue)
                    await PushFavoriteThreadUpdatedAsync(thread.FavoriteFromUserId.Value, thread.FavoriteToUserId.Value, thread.Id);
            }

            await auditService.RecordAsync(AuditAction.ChatMessageHiddenForUser, requestingUserId, messageId, msg.ThreadId, true);
            return new SuccessResult(Messages.ChatMessageDeletedSuccess);
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        [ExceptionHandlingAspect]
        public async Task<IResult> EditMessageAsync(Guid requestingUserId, Guid messageId, string newText)
        {
            if (string.IsNullOrWhiteSpace(newText) || newText.Length > 500)
                return new ErrorResult(Messages.ChatInvalidMessageText);

            newText = newText.Trim();

            // B7: Düzenleme de content moderation'dan geçmeli
            var moderationCheck = await contentModeration.CheckContentAsync(newText);
            if (!moderationCheck.Success)
                return new ErrorResult(moderationCheck.Message);

            var msg = await messageDal.Get(x => x.Id == messageId && !x.IsDeleted);
            if (msg is null) return new ErrorResult(Messages.ChatMessageNotFound);

            if (msg.SenderUserId != requestingUserId)
                return new ErrorResult(Messages.ChatEditOnlyOwnMessages);

            if (msg.MessageType != Entities.Concrete.Entities.ChatMessageType.Text)
                return new ErrorResult(Messages.ChatEditOnlyTextMessages);

            var thread = await threadDal.Get(t => t.Id == msg.ThreadId);
            if (thread is null) return new ErrorResult(Messages.ChatThreadNotFound);

            msg.Text = messageEncryption.Encrypt(newText);
            await messageDal.Update(msg);

            var participants = GetThreadParticipants(thread);

            // B8: tek round-trip ile tüm katılımcılara edit event'i
            try { await realtime.PushChatMessageEditedToUsersAsync(participants, msg.ThreadId, messageId, newText); } catch { }

            // B2: Düzenlenen mesaj thread'in son mesajı ise LastMessagePreview'ı güncelle ve thread-updated push
            if (thread.LastMessageAt.HasValue && msg.CreatedAt == thread.LastMessageAt.Value)
            {
                var preview = newText.Length > 60 ? newText[..60] : newText;
                thread.LastMessagePreview = messageEncryption.Encrypt(preview);
                thread.UpdatedAt = DateTime.UtcNow;
                await threadDal.Update(thread);

                if (thread.AppointmentId.HasValue)
                    await PushAppointmentThreadUpdatedAsync(thread.AppointmentId.Value);
                else if (thread.FavoriteFromUserId.HasValue && thread.FavoriteToUserId.HasValue)
                    await PushFavoriteThreadUpdatedAsync(thread.FavoriteFromUserId.Value, thread.FavoriteToUserId.Value, thread.Id);
            }

            return new SuccessResult(Messages.ChatMessageEditedSuccess);
        }

        [LogAspect]
        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        public async Task RedactUserContentForAccountClosureAsync(Guid userId)
        {
            var msgs = await messageDal.GetAll(m => m.SenderUserId == userId && !m.IsDeleted);
            if (msgs.Count == 0) return;

            var emptyEnc = messageEncryption.Encrypt(string.Empty);
            foreach (var m in msgs)
            {
                m.Text = emptyEnc;
                m.MediaUrl = null;
                m.ReplyToTextPreview = null;
                await messageDal.Update(m);
            }

            var threadIds = msgs.Select(x => x.ThreadId).Distinct().ToList();
            foreach (var tid in threadIds)
            {
                var thread = await threadDal.Get(t => t.Id == tid);
                if (thread is null) continue;

                var latest = await messageDal.GetQueryable()
                    .AsNoTracking()
                    .Where(m => m.ThreadId == tid && !m.IsDeleted)
                    .OrderByDescending(m => m.CreatedAt)
                    .FirstOrDefaultAsync();

                if (latest is null)
                {
                    thread.LastMessagePreview = emptyEnc;
                    thread.LastMessageAt = null;
                }
                else
                {
                    var decrypted = messageEncryption.Decrypt(latest.Text) ?? string.Empty;
                    if (decrypted.Length > 100)
                        decrypted = decrypted[..100];
                    thread.LastMessagePreview = messageEncryption.Encrypt(decrypted);
                    thread.LastMessageAt = latest.CreatedAt;
                }

                thread.UpdatedAt = DateTime.UtcNow;
                await threadDal.Update(thread);
            }
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        public async Task<IResult> DeleteThreadForUserAsync(Guid requestingUserId, Guid threadId)
        {
            var thread = await threadDal.Get(t => t.Id == threadId);
            if (thread is null) return new ErrorResult(Messages.ChatNotFound);

            var isParticipant =
                thread.CustomerUserId == requestingUserId ||
                thread.StoreOwnerUserId == requestingUserId ||
                thread.FreeBarberUserId == requestingUserId ||
                thread.FavoriteFromUserId == requestingUserId ||
                thread.FavoriteToUserId == requestingUserId;

            if (!isParticipant) return new ErrorResult(Messages.NotAParticipant);

            // B3: Tek sorguda messageId'leri de getirerek ek round-trip'ten kaçın
            var (addedCount, allMessageIds) = await messageDal.AddUserDeletionForThreadWithIdsAsync(threadId, requestingUserId);

            var allParticipants = GetThreadParticipants(thread);

            if (addedCount > 0 && allMessageIds.Count > 0)
            {
                await messageDal.CleanupFullyDeletedMessagesAsync(allMessageIds, allParticipants);
            }

            if (thread.IsSocialThread)
            {
                SetThreadHiddenForUser(thread, requestingUserId, true);
                thread.UpdatedAt = DateTime.UtcNow;
                await threadDal.Update(thread);
            }

            logger.LogInformation("Thread {ThreadId} deleted for user {UserId}. {Count} messages marked.", threadId, requestingUserId, addedCount);

            // B3: SignalR: talep eden kullanıcıya thread'i kaldır (bütün açık oturumlarında listeden düşsün)
            try { await realtime.PushChatThreadRemovedAsync(requestingUserId, threadId); } catch { }

            await auditService.RecordAsync(AuditAction.ChatThreadHiddenForUser, requestingUserId, threadId, null, true);

            return new SuccessResult(Messages.ChatThreadDeletedSuccess);
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        public async Task<IResult> DeleteThreadForEveryoneAsync(Guid requestingUserId, Guid threadId)
        {
            var thread = await threadDal.Get(t => t.Id == threadId);
            if (thread is null) return new ErrorResult(Messages.ChatNotFound);

            var isParticipant =
                thread.CustomerUserId == requestingUserId ||
                thread.StoreOwnerUserId == requestingUserId ||
                thread.FreeBarberUserId == requestingUserId ||
                thread.FavoriteFromUserId == requestingUserId ||
                thread.FavoriteToUserId == requestingUserId;
            if (!isParticipant) return new ErrorResult(Messages.NotAParticipant);

            var allParticipants = GetThreadParticipants(thread);
            var others = allParticipants.Where(u => u != requestingUserId).ToList();

            // 1) Kullanıcının KENDİ mesajlarını herkesten sil (global soft-delete)
            var ownMsgs = await messageDal.GetAll(m =>
                m.ThreadId == threadId && m.SenderUserId == requestingUserId && !m.IsDeleted);
            foreach (var m in ownMsgs)
            {
                m.IsDeleted = true;
                m.DeletedAt = DateTime.UtcNow;
                m.DeletedByUserId = requestingUserId;
                await messageDal.Update(m);
                if (others.Count > 0)
                {
                    try { await realtime.PushChatMessageRemovedToUsersAsync(others, threadId, m.Id); } catch { }
                }
            }

            // 2) Kalan (karşı tarafın) mesajları YALNIZCA bu kullanıcıdan gizle + thread'i listeden düşür
            var (addedCount, allMessageIds) = await messageDal.AddUserDeletionForThreadWithIdsAsync(threadId, requestingUserId);
            if (addedCount > 0 && allMessageIds.Count > 0)
                await messageDal.CleanupFullyDeletedMessagesAsync(allMessageIds, allParticipants);

            try { await realtime.PushChatThreadRemovedAsync(requestingUserId, threadId); } catch { }

            // 3) Thread preview tazele (kendi mesajların son mesaj olabilirdi) + karşı tarafa thread güncellemesi
            var latest = await messageDal.GetQueryable()
                .AsNoTracking()
                .Where(m => m.ThreadId == threadId && !m.IsDeleted)
                .OrderByDescending(m => m.CreatedAt)
                .FirstOrDefaultAsync();
            if (latest is null)
            {
                thread.LastMessagePreview = null;
                thread.LastMessageAt = null;
            }
            else
            {
                var decrypted = messageEncryption.Decrypt(latest.Text) ?? string.Empty;
                if (decrypted.Length > 60) decrypted = decrypted[..60];
                thread.LastMessagePreview = messageEncryption.Encrypt(decrypted);
                thread.LastMessageAt = latest.CreatedAt;
            }
            thread.UpdatedAt = DateTime.UtcNow;
            await threadDal.Update(thread);
            if (thread.AppointmentId.HasValue)
                await PushAppointmentThreadUpdatedAsync(thread.AppointmentId.Value);
            else if (thread.FavoriteFromUserId.HasValue && thread.FavoriteToUserId.HasValue)
                await PushFavoriteThreadUpdatedAsync(thread.FavoriteFromUserId.Value, thread.FavoriteToUserId.Value, thread.Id);

            await auditService.RecordAsync(AuditAction.ChatThreadHiddenForUser, requestingUserId, threadId, null, true);
            return new SuccessResult(Messages.ChatThreadDeletedSuccess);
        }

        // ====================================================================
        // ADMIN VIEW — bir thread'deki TÜM mesajlar (per-user silinmiş dahil),
        // decrypt edilmiş, sender display name + soft-delete bilgisi ile.
        // ====================================================================
        public async Task<IDataResult<PagedResultDto<AdminChatMessageDto>>> GetThreadMessagesForAdminAsync(Guid threadId, int page, int pageSize)
        {
            var thread = await threadDal.Get(t => t.Id == threadId);
            if (thread == null)
                return new ErrorDataResult<PagedResultDto<AdminChatMessageDto>>(null!, Messages.AdminChatThreadNotFound);

            var (messages, total) = await messageDal.GetThreadMessagesForAdminAsync(threadId, page, pageSize);
            if (messages.Count == 0)
            {
                return new SuccessDataResult<PagedResultDto<AdminChatMessageDto>>(new PagedResultDto<AdminChatMessageDto>
                {
                    Items = new List<AdminChatMessageDto>(),
                    Total = total,
                    Page = page < 1 ? 1 : page,
                    PageSize = pageSize < 1 ? 50 : pageSize
                });
            }

            // Sender id lookup (display name)
            var senderIds = messages.Select(m => m.SenderUserId).Distinct().ToList();
            var senderMap = new Dictionary<Guid, string>();
            foreach (var id in senderIds)
            {
                var u = await userDal.Get(x => x.Id == id);
                if (u != null)
                {
                    var fn = (u.FirstName ?? string.Empty).Trim();
                    var ln = (u.LastName ?? string.Empty).Trim();
                    senderMap[id] = string.IsNullOrWhiteSpace(fn) && string.IsNullOrWhiteSpace(ln) ? "Bilinmiyor" : $"{fn} {ln}".Trim();
                }
            }

            // Per-user soft-delete map
            var msgIds = messages.Select(m => m.Id).ToList();
            var deletionMap = await messageDal.GetDeletionsByMessageIdsAsync(msgIds);

            var items = messages.Select(m =>
            {
                // Sadece text mesajları encrypted; media/system mesajlarda Text farklı kullanılabilir.
                // Decrypt başarısız olursa raw text fallback gösterilir.
                string text = m.Text ?? string.Empty;
                if (!m.IsSystem && (int)m.MessageType == 0)
                {
                    try { text = messageEncryption.Decrypt(text) ?? text; }
                    catch { /* yut — raw göster */ }
                }

                return new AdminChatMessageDto
                {
                    MessageId = m.Id,
                    ThreadId = m.ThreadId,
                    SenderUserId = m.SenderUserId,
                    SenderDisplayName = senderMap.TryGetValue(m.SenderUserId, out var n) ? n : null,
                    Text = text,
                    MessageType = (int)m.MessageType,
                    MediaUrl = m.MediaUrl,
                    ReplyToMessageId = m.ReplyToMessageId,
                    ReplyToTextPreview = m.ReplyToTextPreview,
                    IsSystem = m.IsSystem,
                    CreatedAt = m.CreatedAt,
                    IsDeletedGlobally = m.IsDeleted,
                    DeletedByUserId = m.DeletedByUserId,
                    DeletedAt = m.DeletedAt,
                    HiddenForUserIds = deletionMap.TryGetValue(m.Id, out var ids) ? ids : new List<Guid>()
                };
            }).ToList();

            return new SuccessDataResult<PagedResultDto<AdminChatMessageDto>>(new PagedResultDto<AdminChatMessageDto>
            {
                Items = items,
                Total = total,
                Page = page < 1 ? 1 : page,
                PageSize = pageSize < 1 ? 50 : pageSize
            });
        }
    }
}
