using System;
using System.Collections.Generic;

namespace Entities.Concrete.Dto
{
    /// <summary>
    /// Admin görünümünde chat mesajı — şifresi çözülmüş metin, soft-delete bilgisi,
    /// ve hangi kullanıcılar tarafından silindiği listesi.
    /// </summary>
    public class AdminChatMessageDto
    {
        public Guid MessageId { get; set; }
        public Guid ThreadId { get; set; }
        public Guid SenderUserId { get; set; }
        public string? SenderDisplayName { get; set; }
        public string Text { get; set; } = string.Empty;
        public int MessageType { get; set; } // 0=Text, 1=Image, 2=Location, 3=File, 4=Audio
        public string? MediaUrl { get; set; }
        public Guid? ReplyToMessageId { get; set; }
        public string? ReplyToTextPreview { get; set; }
        public bool IsSystem { get; set; }
        public DateTime CreatedAt { get; set; }

        // Soft-delete bilgisi
        public bool IsDeletedGlobally { get; set; }
        public Guid? DeletedByUserId { get; set; }
        public DateTime? DeletedAt { get; set; }
        /// <summary>Bu mesajı kişisel olarak silmiş kullanıcı id'leri (ChatMessageUserDeletion).</summary>
        public List<Guid> HiddenForUserIds { get; set; } = new();
    }
}
