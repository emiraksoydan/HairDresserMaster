using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entities.Concrete.Enums
{
    public enum AppointmentFilter
    {
        /// <summary>Admin: durum filtresi uygulanmaz.</summary>
        All = 0,
        Active = 1,
        Completed = 2,
        Cancelled = 3,
        Pending = 4,
    }
}
