using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Utilities.Security.PhoneSetting
{
    public interface IPhoneService
    {
        string NormalizeToE164(string raw);
        string Mask(string e164);
        string HashForLookup(string normalizedE164);
        string EncryptForStorage(string normalizedE164);
        string DecryptForRead(string? encryptedOrPlainValue);
    }
}
