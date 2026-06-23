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
    public partial class ChatManager(
             IAppointmentDal appointmentDal,
             IChatThreadDal threadDal,
             IChatMessageDal messageDal,
             IBarberStoreDal barberStoreDal,
             IUserDal userDal,
             IFreeBarberDal freeBarberDal,
             IImageDal imageDal,
             IFavoriteDal favoriteDal,
             ISocialProfileDal socialProfileDal,
             ISocialFollowDal socialFollowDal,
             IMessageReadReceiptDal receiptDal,
             IRealTimePublisher realtime,
             FavoriteHelper favoriteHelper,
             BlockedHelper blockedHelper,
             BadgeService badgeService,
             IContentModerationService contentModeration,
             IMessageEncryptionService messageEncryption,
             ILogger<ChatManager> logger,
             IAuditService auditService,
             IPushNotificationService? pushNotificationService = null
     ) : IChatService
    {

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        public async Task<IDataResult<ChatMessageDto>> SendMessageAsync(Guid senderUserId, Guid appointmentId, string text, Guid? replyToMessageId = null)
        {
            text = (text ?? "").Trim();
            if (text.Length == 0) return new ErrorDataResult<ChatMessageDto>(Messages.EmptyMessage);

            var moderationCheck = await contentModeration.CheckContentAsync(text);
            if (!moderationCheck.Success)
                return new ErrorDataResult<ChatMessageDto>(moderationCheck.Message);

            var appt = await appointmentDal.Get(x => x.Id == appointmentId);
            if (appt is null) return new ErrorDataResult<ChatMessageDto>(Messages.AppointmentNotFound);

            // yetki: sender katılımcı mı?
            var isParticipant =
                appt.CustomerUserId == senderUserId ||
                appt.FreeBarberUserId == senderUserId ||
                appt.BarberStoreUserId == senderUserId;

            if (!isParticipant) return new ErrorDataResult<ChatMessageDto>(Messages.NotAParticipant);

            // Mesaj gönderme kontrolü: Randevu status kontrolü VEYA favori kontrolü
            bool canSendMessage = false;

            // 1. Randevu status kontrolü: Pending veya Approved ise mesaj gönderilebilir
            if (appt.Status is AppointmentStatus.Pending or AppointmentStatus.Approved)
            {
                canSendMessage = true;
            }
            // 2. Randevu status uygun değilse, favori kontrolü yap
            else
            {
                // Katılımcılar arasında aktif favori var mı kontrol et
                var participants = new[] { appt.CustomerUserId, appt.FreeBarberUserId, appt.BarberStoreUserId }
                    .Where(x => x.HasValue && x.Value != senderUserId)
                    .Select(x => x!.Value)
                    .ToList();

                // Store ID'yi al (eğer varsa)
                Guid? storeId = null;
                if (appt.BarberStoreUserId.HasValue)
                {
                    var storeForFavoriteCheck = await barberStoreDal.Get(x => x.BarberStoreOwnerId == appt.BarberStoreUserId.Value);
                    if (storeForFavoriteCheck != null)
                    {
                        storeId = storeForFavoriteCheck.Id;
                    }
                }

                // Optimized: Tek query ile tüm favori kontrolü
                canSendMessage = await favoriteHelper.IsFavoriteActiveMultipleParticipantsAsync(
                    senderUserId,
                    participants,
                    storeId
                );
            }

            if (!canSendMessage)
                return new ErrorDataResult<ChatMessageDto>(Messages.MessageRequiresActiveAppointmentOrFavorite);

            // Performance: Use Get instead of GetAll().FirstOrDefault()
            var thread = await threadDal.Get(t => t.AppointmentId == appointmentId);
            BarberStore? barberStore = null;
            if (appt.BarberStoreUserId.HasValue)
            {
                barberStore = await barberStoreDal.Get(x => x.BarberStoreOwnerId == appt.BarberStoreUserId);
                if (barberStore is null)
                    return new ErrorDataResult<ChatMessageDto>(Messages.StoreNotFound);
            }

            if (thread is null)
            {
                thread = new ChatThread
                {
                    Id = Guid.NewGuid(),
                    AppointmentId = appointmentId,
                    CustomerUserId = appt.CustomerUserId,
                    StoreOwnerUserId = appt.BarberStoreUserId,
                    FreeBarberUserId = appt.FreeBarberUserId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                await threadDal.Add(thread);
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
                AppointmentId = appointmentId,
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

            // unread arttır (sender dışındaki katılımcılara)
            if (thread.CustomerUserId.HasValue && thread.CustomerUserId != senderUserId) thread.CustomerUnreadCount++;
            if (thread.StoreOwnerUserId.HasValue && thread.StoreOwnerUserId != senderUserId) thread.StoreUnreadCount++;
            if (thread.FreeBarberUserId.HasValue && thread.FreeBarberUserId != senderUserId) thread.FreeBarberUnreadCount++;

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

            // B8: push -> tüm katılımcılara tek SignalR round-trip ile
            var recipients = new[] { thread.CustomerUserId, thread.StoreOwnerUserId, thread.FreeBarberUserId }
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .Distinct()
                .ToList();

            await realtime.PushChatMessageToUsersAsync(recipients, dto);

            // Thread güncellemesini tüm katılımcılara push et (LastMessagePreview, LastMessageAt, UnreadCount değişti)
            await PushAppointmentThreadUpdatedAsync(appointmentId);

            // Badge update: sender dışındaki katılımcılar için badge count'u güncelle
            var recipientsForBadgeUpdate = recipients.Where(u => u != senderUserId).ToList();
            if (recipientsForBadgeUpdate.Any())
            {
                await badgeService.NotifyBadgeChangeBatchAsync(recipientsForBadgeUpdate, BadgeChangeReason.MessageReceived);
            }

            await PushChatNotificationForFirstUnreadAsync(
                thread,
                senderUserId,
                text,
                "Yeni mesaj",
                appt.Id);

            await auditService.RecordAsync(AuditAction.ChatMessageSentAppointmentThread, senderUserId, msg.Id, thread.Id, true);

            return new SuccessDataResult<ChatMessageDto>(dto);
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        public async Task<IDataResult<int>> GetUnreadTotalAsync(Guid userId)
        {
            var allowed = new[] { AppointmentStatus.Pending, AppointmentStatus.Approved };
            var threads = await threadDal.GetThreadsForUserAsync(userId, allowed);
            if (threads.Count == 0)
                return new SuccessDataResult<int>(0);

            var favoriteThreadIds = threads
                .Where(t => t.IsFavoriteThread)
                .Select(t => t.ThreadId)
                .Distinct()
                .ToList();

            var visibleFavoriteThreadIds = new HashSet<Guid>();
            if (favoriteThreadIds.Count > 0)
            {
                var favoriteEntities = await threadDal.GetAll(t => favoriteThreadIds.Contains(t.Id));
                foreach (var favoriteEntity in favoriteEntities)
                {
                    if (await IsFavoriteThreadActiveAsync(favoriteEntity))
                        visibleFavoriteThreadIds.Add(favoriteEntity.Id);
                }
            }

            var total = threads
                .Where(t => !t.IsSocialThread && (!t.IsFavoriteThread || visibleFavoriteThreadIds.Contains(t.ThreadId)))
                .Sum(t => t.UnreadCount);

            return new SuccessDataResult<int>(total);
        }

        [SecuredOperation("Customer,FreeBarber,BarberStore")]
        [LogAspect]
        [TransactionScopeAspect(IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted)]
        public async Task<IDataResult<bool>> MarkThreadReadByAppointmentAsync(Guid userId, Guid appointmentId)
        {
            return await MarkThreadReadByAppointmentInternalAsync(userId, appointmentId);
        }

        /// <summary>
        /// System/Background worker kullanımı için - TransactionScope olmadan
        /// Worker kendi transaction'ını yönettiği için nested transaction hatası vermez
        /// </summary>
        [LogAspect]
        public async Task<IDataResult<bool>> MarkThreadReadByAppointmentSystemAsync(Guid userId, Guid appointmentId)
        {
            return await MarkThreadReadByAppointmentInternalAsync(userId, appointmentId);
        }

        [ExceptionHandlingAspect]
        private async Task<IDataResult<bool>> MarkThreadReadByAppointmentInternalAsync(Guid userId, Guid appointmentId)
        {
            // Randevu thread'i için okundu işaretleme (geriye dönük uyumluluk)
            var thread = await threadDal.Get(t => t.AppointmentId == appointmentId);
            if (thread is null) return new ErrorDataResult<bool>(false, Messages.ChatNotFound);

            bool needsUpdate = false;

            if (thread.CustomerUserId == userId)
            {
                if (thread.CustomerUnreadCount > 0)
                {
                    thread.CustomerUnreadCount = 0;
                    needsUpdate = true;
                }
            }
            else if (thread.StoreOwnerUserId == userId)
            {
                if (thread.StoreUnreadCount > 0)
                {
                    thread.StoreUnreadCount = 0;
                    needsUpdate = true;
                }
            }
            else if (thread.FreeBarberUserId == userId)
            {
                if (thread.FreeBarberUnreadCount > 0)
                {
                    thread.FreeBarberUnreadCount = 0;
                    needsUpdate = true;
                }
            }
            else return new ErrorDataResult<bool>(false, Messages.ParticipantNotFound);

            if (needsUpdate)
            {
                await threadDal.Update(thread);

                // Read receipt'leri işle ve çift tik bildirimi gönder
                await ProcessReadReceiptsAsync(thread, userId);

                // Badge count'u güncelle (normal + sosyal ayrı)
                await badgeService.NotifyBadgeChangeAsync(userId, BadgeChangeReason.MessageRead);
            }

            return new SuccessDataResult<bool>(true);
        }
    }
}
