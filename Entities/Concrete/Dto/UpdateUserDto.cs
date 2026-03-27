using Entities.Abstract;
using Entities.Attributes;

namespace Entities.Concrete.Dto
{
    public class UpdateUserDto : IDto
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        [LogIgnore]
        public string PhoneNumber { get; set; }
    }
}