using Entities.Abstract;

namespace Entities.Concrete.Dto
{
    public class AdminAIChatRequestDto : IDto
    {
        public string Message { get; set; } = null!;
        /// <summary>Önceki tur mesajları (yalnızca user/assistant metin).</summary>
        public List<AdminAIChatMessageDto>? History { get; set; }
    }

    public class AdminAIConfirmRequestDto : IDto
    {
        public List<AdminAIPendingActionDto> Actions { get; set; } = new();
    }

    public class AdminAIChatMessageDto : IDto
    {
        /// <summary>user | assistant</summary>
        public string Role { get; set; } = null!;
        public string Content { get; set; } = null!;
    }

    public class AdminAIChatResponseDto : IDto
    {
        public string Reply { get; set; } = null!;
        /// <summary>Gemini — hangi model sağlayıcısı kullanıldı.</summary>
        public string? ProviderUsed { get; set; }
        public List<AdminAIActionResultDto> ActionsExecuted { get; set; } = new();
        public bool RequiresConfirmation { get; set; }
        public List<AdminAIPendingActionDto> PendingActions { get; set; } = new();
    }

    public class AdminAIPendingActionDto : IDto
    {
        public string Id { get; set; } = null!;
        public string Tool { get; set; } = null!;
        public string Summary { get; set; } = null!;
        /// <summary>Tool argümanları (JSON string).</summary>
        public string InputJson { get; set; } = null!;
    }

    public class AdminAIActionResultDto : IDto
    {
        public string Tool { get; set; } = null!;
        public bool Success { get; set; }
        public string Summary { get; set; } = null!;
    }
}
