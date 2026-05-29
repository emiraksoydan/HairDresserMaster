using System;

namespace Entities.Concrete.Dto
{
    /// <summary>Admin tarafında yeni help guide oluştururken kullanılır.</summary>
    public class HelpGuideCreateDto
    {
        public int UserType { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string TranslationKey { get; set; } = string.Empty;
        public int Order { get; set; } = 0;
        public bool IsActive { get; set; } = true;
    }

    /// <summary>Admin tarafında mevcut help guide güncellemesi.</summary>
    public class HelpGuideUpdateDto
    {
        public int UserType { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string TranslationKey { get; set; } = string.Empty;
        public int Order { get; set; } = 0;
        public bool IsActive { get; set; } = true;
    }
}
