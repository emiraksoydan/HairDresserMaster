using System;
using Entities.Abstract;

namespace Entities.Concrete.Dto
{
    /// <summary>Admin global arama sonucu — kind ile entity türü belirtilir.</summary>
    public class AdminSearchResultDto : IDto
    {
        /// <summary>User | Store | FreeBarber | Service | ManuelBarber | Category</summary>
        public string Kind { get; set; } = null!;

        public Guid EntityId { get; set; }
        public string Title { get; set; } = null!;
        public string? Subtitle { get; set; }
    }
}
