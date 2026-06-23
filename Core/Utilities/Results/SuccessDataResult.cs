using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Utilities.Results
{
    public class SuccessDataResult<T>:DataResult<T>
    {
        public SuccessDataResult(T data,string message) : base(data,true, message)
        {
        }
        public SuccessDataResult(T data) : base(data,true)
        {
        }
        /// <summary>
        /// Yalnızca mesaj döndür (data yok). T=string için tek parametreli ctor ile karışmaması adına ayrı isim.
        /// </summary>
        public static SuccessDataResult<T> WithMessage(string message) => new(default!, message);
        public SuccessDataResult() : base(default, true)
        {
        }

    }
}
