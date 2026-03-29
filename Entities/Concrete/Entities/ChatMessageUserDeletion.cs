using Entities.Abstract;
using System;

namespace Entities.Concrete.Entities
{
    /// <summary>
    /// Per-user soft deletion of a chat message.
    /// A message is globally deleted (IsDeleted=true) when all thread participants have a record here.
    /// </summary>
    public class ChatMessageUserDeletion : IEntity
    {
        public Guid Id { get; set; }
        public Guid MessageId { get; set; }
        public Guid UserId { get; set; }
        public DateTime DeletedAt { get; set; }
    }
}
