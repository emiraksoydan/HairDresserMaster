using Entities.Abstract;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entities.Concrete.Dto
{
    public class ChatMessageDto : IDto
    {
        public Guid ThreadId { get; set; }
        public Guid MessageId { get; set; }
        public Guid SenderUserId { get; set; }
        public string Text { get; set; } = default!;
        public DateTime CreatedAt { get; set; }

        /// <summary>0=Text, 1=Image, 2=Location</summary>
        public int MessageType { get; set; } = 0;
        public string? MediaUrl { get; set; }

        public Guid? ReplyToMessageId { get; set; }
        public string? ReplyToTextPreview { get; set; }
    }
}
