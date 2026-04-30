using Entities.Abstract;
using Entities.Concrete.Dto;

namespace Entities.Concrete.Entities
{
    public class SavedFilter : IEntity
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Name { get; set; }
        /// <summary>
        /// BackendFilterCriteria JSON (frontend tarafından serialize edilmiş hâliyle saklanır)
        /// </summary>
        public string FilterCriteriaJson { get; set; }

        /// <summary>Şema sürümü (FilterConstants.CurrentFilterSchemaVersion ile uyumlu).</summary>
        public int FilterSchemaVersion { get; set; } = FilterConstants.CurrentFilterSchemaVersion;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
