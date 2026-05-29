using Entities.Abstract;

namespace Entities.Concrete.Dto
{
    public class AdminMediaFileDto : IDto
    {
        public Guid Id { get; set; }
        public string MediaUrl { get; set; } = string.Empty;
        /// <summary>image | audio | file | video</summary>
        public string MediaKind { get; set; } = "image";
        /// <summary>user | store | freebarber | manuelbarber | chat-image | chat-audio | chat-file</summary>
        public string Category { get; set; } = string.Empty;
        public string CategoryLabel { get; set; } = string.Empty;
        public string? ContextTitle { get; set; }
        public string? SenderDisplayName { get; set; }
        public Guid? SenderUserId { get; set; }
        public Guid? OwnerId { get; set; }
        public Guid? ThreadId { get; set; }
        public string? FileName { get; set; }
        /// <summary>Diskten okunan dosya boyutu (byte). Bulunamazsa null.</summary>
        public long? SizeBytes { get; set; }
        /// <summary>Medyanın sahibi (kullanıcı/salon/berber adı veya sohbette gönderen).</summary>
        public string? OwnerName { get; set; }
        /// <summary>Sahip numarası (StoreNo / CustomerNumber). Yoksa null.</summary>
        public string? OwnerNumber { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
