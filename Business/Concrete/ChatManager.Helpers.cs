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

        /// <summary>
        /// Generic helper method to fetch images for multiple owners based on owner type.
        /// Consolidates previous GetImagesForUsersAsync, GetImagesForStoresAsync, and GetImagesForFreeBarberAsync methods.
        /// Returns a dictionary mapping owner ID to their most recent image URL.
        /// </summary>
        /// <param name="ownerIds">List of owner IDs to fetch images for</param>
        private string? DecryptOptionalPreview(string? encryptedPreview)
        {
            if (string.IsNullOrEmpty(encryptedPreview)) return null;
            return messageEncryption.Decrypt(encryptedPreview);
        }

        /// <summary>
        /// Reply'de alıntılanan orijinal mesajın plaintext önizlemesini (max 100 char) döndürür.
        /// Decrypt başarısızsa (eski plaintext veri) olduğu gibi döner.
        /// </summary>
        private async Task<string?> BuildReplyPreviewAsync(Guid? replyToMessageId)
        {
            if (!replyToMessageId.HasValue) return null;
            var replied = await messageDal.Get(x => x.Id == replyToMessageId.Value && !x.IsDeleted);
            if (replied is null) return null;
            var raw = messageEncryption.Decrypt(replied.Text) ?? string.Empty;
            if (raw.Length > 100) raw = raw[..100];
            return raw;
        }

        private static readonly HashSet<string> _allowedMediaSchemes = new(StringComparer.OrdinalIgnoreCase) { "http", "https" };
        private const int MaxMediaUrlLength = 2048;
        private const int MaxFileNameLength = 200;
        private static readonly char[] _invalidFileNameChars =
            System.IO.Path.GetInvalidFileNameChars().Concat(new[] { '/', '\\', '\0' }).Distinct().ToArray();

        /// <summary>
        /// mediaUrl'yi validate eder. Image/File/Audio için http(s) URL, Location için {"lat":..,"lng":..} JSON beklenir.
        /// </summary>
        private static (bool ok, string? error) ValidateMediaUrl(Entities.Concrete.Entities.ChatMessageType type, string mediaUrl)
        {
            if (string.IsNullOrWhiteSpace(mediaUrl)) return (false, "Medya URL'si boş olamaz.");
            if (mediaUrl.Length > MaxMediaUrlLength) return (false, "Medya URL'si çok uzun.");

            if (type == Entities.Concrete.Entities.ChatMessageType.Location)
            {
                try
                {
                    using var doc = JsonDocument.Parse(mediaUrl);
                    if (doc.RootElement.ValueKind != JsonValueKind.Object) return (false, "Geçersiz konum verisi.");
                    if (!doc.RootElement.TryGetProperty("lat", out var latEl) ||
                        !doc.RootElement.TryGetProperty("lng", out var lngEl)) return (false, "Konum: lat/lng zorunlu.");
                    if (!latEl.TryGetDouble(out var lat) || !lngEl.TryGetDouble(out var lng)) return (false, "Konum: lat/lng sayısal olmalı.");
                    if (lat < -90 || lat > 90 || lng < -180 || lng > 180) return (false, "Konum: koordinat aralık dışı.");
                    return (true, null);
                }
                catch
                {
                    return (false, "Konum formatı geçersiz (JSON bekleniyor).");
                }
            }

            if (!Uri.TryCreate(mediaUrl, UriKind.Absolute, out var uri) ||
                !_allowedMediaSchemes.Contains(uri.Scheme))
            {
                return (false, "Geçersiz medya URL'si.");
            }
            return (true, null);
        }

        private static string? SanitizeFileName(string? fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return null;
            var trimmed = fileName.Trim();
            if (trimmed.Length > MaxFileNameLength) trimmed = trimmed[..MaxFileNameLength];
            var chars = trimmed.Where(c => !_invalidFileNameChars.Contains(c)).ToArray();
            var clean = new string(chars).Trim();
            return string.IsNullOrWhiteSpace(clean) ? null : clean;
        }

        /// <summary>
        /// Thread katılımcılarının benzersiz userId listesini döndürür (sender dahil).
        /// </summary>
        private static List<Guid> GetThreadParticipants(Entities.Concrete.Entities.ChatThread thread)
        {
            var set = new HashSet<Guid>();
            if (thread.CustomerUserId.HasValue) set.Add(thread.CustomerUserId.Value);
            if (thread.StoreOwnerUserId.HasValue) set.Add(thread.StoreOwnerUserId.Value);
            if (thread.FreeBarberUserId.HasValue) set.Add(thread.FreeBarberUserId.Value);
            if (thread.FavoriteFromUserId.HasValue) set.Add(thread.FavoriteFromUserId.Value);
            if (thread.FavoriteToUserId.HasValue) set.Add(thread.FavoriteToUserId.Value);
            return set.ToList();
        }

        /// <summary>
        /// Karşı tarafı favorilememiş kullanıcıya SignalR ile şifreli mesaj içeriği/medya URL'si gönderilmez.
        /// </summary>
        private static ChatMessageDto MaskChatMessageForFavoriteRecipientWithoutMutualFavorite(ChatMessageDto source)
        {
            return new ChatMessageDto
            {
                ThreadId = source.ThreadId,
                MessageId = source.MessageId,
                SenderUserId = source.SenderUserId,
                Text = string.Empty,
                CreatedAt = source.CreatedAt,
                MessageType = 0,
                MediaUrl = null,
                ReplyToMessageId = null,
                ReplyToTextPreview = null
            };
        }

        /// <summary>
        /// fromUserId'nin toUserId'ye yönelik aktif favorisi var mı?
        /// Hem kullanıcı bazlı hem de mağaza bazlı favoriyi kontrol eder.
        /// </summary>
        private async Task<bool> HasActiveFavoriteFromUserAsync(Guid fromUserId, Guid toUserId)
        {
            // 1. Doğrudan kullanıcı bazlı favori (fromUserId → toUserId)
            var directFav = await favoriteDal.GetByUsersAsync(fromUserId, toUserId);
            if (directFav?.IsActive == true) return true;

            // 2. Mağaza bazlı favori (fromUserId → toUserId'nin sahip olduğu herhangi bir mağaza)
            var toUserStores = await barberStoreDal.GetAll(x => x.BarberStoreOwnerId == toUserId);
            if (toUserStores.Any())
            {
                var storeIds = toUserStores.Select(s => s.Id).ToList();
                var storeFav = await favoriteDal.Get(x =>
                    x.FavoritedFromId == fromUserId &&
                    storeIds.Contains(x.FavoritedToId) &&
                    x.IsActive);
                if (storeFav != null) return true;
            }

            return false;
        }

        /// <summary>
        /// Favori thread görünür mü? (en az bir yönde aktif favori olmalı)
        /// </summary>
        private async Task<bool> IsFavoriteThreadActiveAsync(Entities.Concrete.Entities.ChatThread thread)
        {
            if (!thread.FavoriteFromUserId.HasValue || !thread.FavoriteToUserId.HasValue)
                return false;

            var fromUserId = thread.FavoriteFromUserId.Value;
            var toUserId = thread.FavoriteToUserId.Value;
            return await HasActiveFavoriteFromUserAsync(fromUserId, toUserId) ||
                   await HasActiveFavoriteFromUserAsync(toUserId, fromUserId);
        }

        /// <param name="ownerType">Type of owner (User, Store, FreeBarber)</param>
        /// <returns>Dictionary mapping owner ID to latest image URL (null if no image found)</returns>
        private async Task<Dictionary<Guid, string?>> GetImagesForOwnersAsync(
            List<Guid> ownerIds,
            ImageOwnerType ownerType)
        {
            if (ownerIds == null || ownerIds.Count == 0)
                return new Dictionary<Guid, string?>();

            var images = await imageDal.GetAll(img =>
                ownerIds.Contains(img.ImageOwnerId) &&
                img.OwnerType == ownerType);

            return images
                .GroupBy(img => img.ImageOwnerId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(i => i.CreatedAt).FirstOrDefault()?.ImageUrl);
        }

        /// <summary>
        /// Thread'deki mesajları kullanıcı için okundu işaretler ve tam okunan mesajları
        /// diğer katılımcılara SignalR ile bildirir (çift tik).
        /// </summary>
        private async Task ProcessReadReceiptsAsync(Entities.Concrete.Entities.ChatThread thread, Guid userId)
        {
            try
            {
                var newlyReadIds = await receiptDal.MarkThreadMessagesReadAsync(thread.Id, userId);
                if (newlyReadIds.Count == 0) return;

                var allParticipantIds = new List<Guid>();
                if (thread.CustomerUserId.HasValue) allParticipantIds.Add(thread.CustomerUserId.Value);
                if (thread.StoreOwnerUserId.HasValue) allParticipantIds.Add(thread.StoreOwnerUserId.Value);
                if (thread.FreeBarberUserId.HasValue) allParticipantIds.Add(thread.FreeBarberUserId.Value);

                var fullyReadIds = await receiptDal.GetFullyReadMessageIdsAsync(thread.Id, allParticipantIds);
                var newlyFullyRead = newlyReadIds.Where(id => fullyReadIds.Contains(id)).ToList();

                if (newlyFullyRead.Count == 0) return;

                // Diğer katılımcılara (gönderenler) bildir - çift tik görebilsinler
                var otherParticipants = allParticipantIds.Where(id => id != userId).ToList();
                foreach (var participantId in otherParticipants)
                {
                    await realtime.PushChatMessagesReadAsync(participantId, thread.Id, userId, newlyFullyRead);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[ChatManager] ProcessReadReceiptsAsync failed for thread {ThreadId}, user {UserId}", thread.Id, userId);
            }
        }

        /// <summary>
        /// Chat push yalnızca "ilk okunmamış mesajda" gönderilir (thread başına spam önleme).
        /// unreadCount == 1 ise kullanıcı thread'i henüz açmamışken ilk yeni mesaj gelmiştir.
        /// unreadCount > 1 ise bu thread için zaten bekleyen mesaj/push vardır, tekrar push atılmaz.
        /// </summary>
        private async Task PushChatNotificationForFirstUnreadAsync(
            Entities.Concrete.Entities.ChatThread thread,
            Guid senderUserId,
            string previewText,
            string titleFallback,
            Guid? appointmentId)
        {
            if (thread.IsSocialThread) return;
            if (pushNotificationService == null) return;

            var recipients = new[] { thread.CustomerUserId, thread.StoreOwnerUserId, thread.FreeBarberUserId, thread.FavoriteFromUserId, thread.FavoriteToUserId }
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .Distinct()
                .Where(u => u != senderUserId)
                .ToList();

            if (recipients.Count == 0) return;

            var sender = await userDal.Get(x => x.Id == senderUserId);
            var senderNum = sender?.CustomerNumber?.Trim();
            var senderName = $"{sender?.FirstName} {sender?.LastName}".Trim();
            if (sender?.UserType == UserType.FreeBarber)
            {
                var fb = await freeBarberDal.Get(x => x.FreeBarberUserId == senderUserId);
                var panelName = fb != null ? $"{fb.FirstName} {fb.LastName}".Trim() : "";
                if (!string.IsNullOrWhiteSpace(panelName))
                    senderName = panelName;
            }
            if (string.IsNullOrWhiteSpace(senderName))
                senderName = "Bir kullanıcı";

            var senderLabel = string.IsNullOrWhiteSpace(senderNum)
                ? senderName
                : $"{senderName} · No:{senderNum}";

            var contextLine = await BuildChatPushThreadContextAsync(thread);

            string BuildBodyCore(bool includePreview)
            {
                if (!includePreview)
                {
                    return string.IsNullOrWhiteSpace(contextLine)
                        ? $"{senderLabel} size yeni bir mesaj gönderdi."
                        : $"{senderLabel} · {contextLine} — yeni mesaj.";
                }
                if (string.IsNullOrWhiteSpace(previewText))
                {
                    return string.IsNullOrWhiteSpace(contextLine)
                        ? $"{senderLabel} size yeni bir mesaj gönderdi."
                        : $"{senderLabel} · {contextLine} — yeni mesaj.";
                }
                return string.IsNullOrWhiteSpace(contextLine)
                    ? $"{senderLabel}: {previewText}"
                    : $"{senderLabel} · {contextLine} — {previewText}";
            }

            var body = TruncateChatPushText(BuildBodyCore(includePreview: true));
            var pushTitle = TruncateChatPushText(
                string.IsNullOrWhiteSpace(contextLine)
                    ? $"{titleFallback}: {senderName}"
                    : $"{titleFallback}: {senderName} · {contextLine}",
                maxLen: 96);

            foreach (var recipientId in recipients)
            {
                var unreadCount = GetUnreadCountForUser(thread, recipientId);
                if (unreadCount != 1) continue;

                var bodyForRecipient = body;
                if (!thread.AppointmentId.HasValue && thread.FavoriteFromUserId.HasValue && thread.FavoriteToUserId.HasValue)
                {
                    if (!await HasActiveFavoriteFromUserAsync(recipientId, senderUserId))
                        bodyForRecipient = TruncateChatPushText(BuildBodyCore(includePreview: false));
                }

                var payloadObj = new
                {
                    kind = "chat",
                    threadId = thread.Id,
                    appointmentId = appointmentId,
                    senderUserId = senderUserId
                };

                var notificationDto = new NotificationDto
                {
                    Id = Guid.NewGuid(),
                    Type = NotificationType.AppointmentReminder,
                    AppointmentId = appointmentId,
                    Title = pushTitle,
                    Body = bodyForRecipient,
                    PayloadJson = JsonSerializer.Serialize(payloadObj),
                    CreatedAt = DateTime.UtcNow,
                    IsRead = false
                };

                try
                {
                    await pushNotificationService.SendPushNotificationAsync(recipientId, notificationDto);
                }
                catch
                {
                    // Push failure chat akışını kesmemeli.
                }
            }
        }

        /// <summary>Sosyal DM push — sosyal profil adı ve ayrı payload kind kullanır.</summary>
        private async Task PushSocialChatNotificationForFirstUnreadAsync(
            Entities.Concrete.Entities.ChatThread thread,
            Guid senderUserId,
            string previewText)
        {
            if (!thread.IsSocialThread || pushNotificationService == null) return;

            var otherUserId = thread.FavoriteFromUserId == senderUserId
                ? thread.FavoriteToUserId
                : thread.FavoriteFromUserId;
            if (!otherUserId.HasValue || otherUserId.Value == senderUserId) return;

            var recipientId = otherUserId.Value;
            var unreadCount = GetUnreadCountForUser(thread, recipientId);
            if (unreadCount != 1) return;

            var sender = await userDal.Get(x => x.Id == senderUserId);
            if (sender is null) return;

            var (displayName, _, _, _) = await ResolveSocialParticipantInfoAsync(sender);
            var senderLabel = string.IsNullOrWhiteSpace(displayName)
                ? "Bir kullanıcı"
                : displayName;

            var body = string.IsNullOrWhiteSpace(previewText)
                ? $"{senderLabel} size yeni bir mesaj gönderdi."
                : $"{senderLabel}: {previewText}";
            body = TruncateChatPushText(body);
            var pushTitle = TruncateChatPushText($"Sosyal mesaj: {senderLabel}", maxLen: 96);

            var payloadObj = new
            {
                kind = "socialChat",
                threadId = thread.Id,
                senderUserId = senderUserId
            };

            var notificationDto = new NotificationDto
            {
                Id = Guid.NewGuid(),
                Type = NotificationType.AppointmentReminder,
                Title = pushTitle,
                Body = body,
                PayloadJson = JsonSerializer.Serialize(payloadObj),
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            };

            try
            {
                await pushNotificationService.SendPushNotificationAsync(recipientId, notificationDto);
            }
            catch
            {
                // Push failure chat akışını kesmemeli.
            }
        }

        /// <summary>Randevu veya favori mağaza bağlamı — push gövdesinde boş görünmemesi için.</summary>
        private async Task<string?> BuildChatPushThreadContextAsync(Entities.Concrete.Entities.ChatThread thread)
        {
            try
            {
                if (thread.AppointmentId.HasValue)
                {
                    var appt = await appointmentDal.Get(x => x.Id == thread.AppointmentId.Value);
                    if (appt is null) return null;

                    if (appt.BarberStoreUserId.HasValue)
                    {
                        var store = await barberStoreDal.Get(x => x.BarberStoreOwnerId == appt.BarberStoreUserId.Value);
                        if (store is not null)
                        {
                            var name = store.StoreName?.Trim();
                            var sn = store.StoreNo?.Trim();
                            var bits = new List<string>();
                            if (!string.IsNullOrWhiteSpace(name)) bits.Add(name);
                            if (!string.IsNullOrWhiteSpace(sn)) bits.Add($"Dükkan No:{sn}");
                            if (bits.Count > 0) return string.Join(" · ", bits);
                        }
                    }

                    if (appt.FreeBarberUserId.HasValue)
                    {
                        var u = await userDal.Get(x => x.Id == appt.FreeBarberUserId.Value);
                        var fb = await freeBarberDal.Get(x => x.FreeBarberUserId == appt.FreeBarberUserId.Value);
                        var fn = fb != null ? $"{fb.FirstName} {fb.LastName}".Trim() : $"{u?.FirstName} {u?.LastName}".Trim();
                        if (string.IsNullOrWhiteSpace(fn)) fn = "Serbest berber";
                        var cno = u?.CustomerNumber?.Trim();
                        return string.IsNullOrWhiteSpace(cno) ? $"Serbest: {fn}" : $"Serbest: {fn} · No:{cno}";
                    }

                    return "Randevu sohbeti";
                }

                if (thread.StoreId.HasValue)
                {
                    var store = await barberStoreDal.Get(x => x.Id == thread.StoreId.Value);
                    if (store is null) return null;
                    var name = store.StoreName?.Trim();
                    var sn = store.StoreNo?.Trim();
                    var parts = new List<string> { "Favori" };
                    if (!string.IsNullOrWhiteSpace(name)) parts.Add(name);
                    if (!string.IsNullOrWhiteSpace(sn)) parts.Add($"Dükkan No:{sn}");
                    return string.Join(" · ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
                }

                return null;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[ChatManager] BuildChatPushThreadContextAsync failed for thread {ThreadId}", thread.Id);
                return null;
            }
        }

        private static string TruncateChatPushText(string? text, int maxLen = 360)
        {
            if (string.IsNullOrEmpty(text)) return "";
            if (text.Length <= maxLen) return text;
            return text[..(maxLen - 1)] + "…";
        }

        private static int GetUnreadCountForUser(Entities.Concrete.Entities.ChatThread thread, Guid userId)
        {
            if (thread.CustomerUserId == userId) return thread.CustomerUnreadCount;
            if (thread.StoreOwnerUserId == userId) return thread.StoreUnreadCount;
            if (thread.FreeBarberUserId == userId) return thread.FreeBarberUnreadCount;
            return 0;
        }
    }
}
