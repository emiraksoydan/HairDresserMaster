namespace Entities.Concrete.Dto
{
    public class SocialPostMediaDto
    {
        public Guid Id { get; set; }
        public int SortOrder { get; set; }
        public string MediaUrl { get; set; } = string.Empty;
        public string? ThumbnailUrl { get; set; }
        public int? DurationSec { get; set; }
    }
}
