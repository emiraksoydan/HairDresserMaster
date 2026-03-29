using Entities.Abstract;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entities.Concrete.Entities
{
    /// <summary>0=Text, 1=Image, 2=Location, 3=File</summary>
    public enum ChatMessageType { Text = 0, Image = 1, Location = 2, File = 3 }

    public class ChatMessage : IEntity
    {
        public Guid Id { get; set; }
        public Guid ThreadId { get; set; }
        public Guid? AppointmentId { get; set; } // Nullable: favori thread'lerde null

        public Guid SenderUserId { get; set; }
        public string Text { get; set; } = default!;
        public bool IsSystem { get; set; }

        // Media / Location
        public ChatMessageType MessageType { get; set; } = ChatMessageType.Text;
        /// <summary>Image: CDN URL. Location: JSON {"lat":0.0,"lng":0.0}</summary>
        public string? MediaUrl { get; set; }

        // Reply
        public Guid? ReplyToMessageId { get; set; }
        /// <summary>Cached plain-text preview of the replied message (max 100 chars, not encrypted)</summary>
        public string? ReplyToTextPreview { get; set; }

        // Soft-delete
        public bool IsDeleted { get; set; } = false;
        public Guid? DeletedByUserId { get; set; }
        public DateTime? DeletedAt { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
