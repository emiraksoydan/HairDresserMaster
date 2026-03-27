using Entities.Abstract;
using Entities.Attributes;

namespace Entities.Concrete.Dto
{
    public class UpdateLocationDto : IDto
    {
        [LogIgnore]
        public double Latitude { get; set; }
        [LogIgnore]
        public double Longitude { get; set; }
    }
}
