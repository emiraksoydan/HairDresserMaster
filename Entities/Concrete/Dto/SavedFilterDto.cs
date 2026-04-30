using Entities.Abstract;

namespace Entities.Concrete.Dto
{
    public class SavedFilterGetDto : IDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string FilterCriteriaJson { get; set; }
        public int FilterSchemaVersion { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class SavedFilterCreateDto : IDto
    {
        public string Name { get; set; }
        public string FilterCriteriaJson { get; set; }
    }

    public class SavedFilterUpdateDto : IDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string FilterCriteriaJson { get; set; }
    }
}
