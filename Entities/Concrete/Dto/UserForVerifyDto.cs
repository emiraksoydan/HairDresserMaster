using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Entities.Abstract;
using Entities.Attributes;
using Entities.Concrete.Enums;

namespace Entities.Concrete.Dto
{
    public class UserForVerifyDto : IDto
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        [LogIgnore]
        public string PhoneNumber { get; set; }
        [LogIgnore]
        public string Code { get; set; }
        public string? Device {  get; set; }
        public UserType UserType { get; set; }
        public string Mode { get; set; }
        [LogIgnore]
        public string? Password { get; set; }
    }
}
