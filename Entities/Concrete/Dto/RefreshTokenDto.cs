using Entities.Abstract;
using Entities.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entities.Concrete.Dto
{
    public class RefreshTokenDto : IDto
    {
        [LogIgnore]
        public string RefreshToken { get; set; }
    }
}
