using Entities.Abstract;

namespace Entities.Concrete.Dto
{
    public class AdminMediaStatsDto : IDto
    {
        public int TotalFiles { get; set; }
        public int ImageCount { get; set; }
        public int VideoCount { get; set; }
        public int AudioCount { get; set; }
        public int FileCount { get; set; }
        public long? TotalSizeBytes { get; set; }
        public long? ImageSizeBytes { get; set; }
        public long? VideoSizeBytes { get; set; }
        public long? AudioSizeBytes { get; set; }
        public long? FileSizeBytes { get; set; }
        public List<AdminMediaCategoryStatDto> Categories { get; set; } = new();
    }

    public class AdminMediaCategoryStatDto : IDto
    {
        public string CategoryId { get; set; } = "";
        public string Label { get; set; } = "";
        public int Count { get; set; }
        public long? SizeBytes { get; set; }
    }
}
