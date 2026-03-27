using Microsoft.Extensions.Options;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Core.Utilities.Security.PhoneSetting
{
    public class PhoneService : IPhoneService
    {
        private readonly byte[] _pepper;
        private readonly byte[] _encKey;
        private readonly bool _hashEnabled;
        private readonly bool _encEnabled;

        public PhoneService(IOptions<SecurityOption> options)
        {
            var securityOptions = options.Value ?? new SecurityOption();

            try
            {
                _pepper = string.IsNullOrWhiteSpace(securityOptions.PhonePepperBase64)
                    ? Array.Empty<byte>()
                    : Convert.FromBase64String(securityOptions.PhonePepperBase64);
                _hashEnabled = _pepper.Length > 0;
            }
            catch
            {
                _pepper = Array.Empty<byte>();
                _hashEnabled = false;
            }

            try
            {
                _encKey = string.IsNullOrWhiteSpace(securityOptions.PhoneEncKeyBase64)
                    ? Array.Empty<byte>()
                    : Convert.FromBase64String(securityOptions.PhoneEncKeyBase64);
                _encEnabled = _encKey.Length == 32;
            }
            catch
            {
                _encKey = Array.Empty<byte>();
                _encEnabled = false;
            }
        }

        public string NormalizeToE164(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            var d = new string(raw.Where(char.IsDigit).ToArray());
            if (d.StartsWith("00")) d = d[2..];
            if (d.Length == 10 && d.StartsWith("5")) d = "90" + d; // TR varsayımı
            if (!d.StartsWith("+")) d = "+" + d;
            return d;
        }

        public string Mask(string e164) =>
            string.IsNullOrEmpty(e164) || e164.Length < 6 ? "****"
            : $"{e164[..4]} {new string('*', e164.Length - 6)} {e164[^2..]}";

        public string HashForLookup(string normalizedE164)
        {
            if (string.IsNullOrWhiteSpace(normalizedE164))
                return string.Empty;
            if (!_hashEnabled)
                return string.Empty;

            using var hmac = new HMACSHA256(_pepper);
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(normalizedE164));
            return Convert.ToBase64String(hash);
        }

        public string EncryptForStorage(string normalizedE164)
        {
            if (string.IsNullOrWhiteSpace(normalizedE164))
                return string.Empty;
            if (!_encEnabled)
                return normalizedE164;

            using var aes = Aes.Create();
            aes.Key = _encKey;
            aes.GenerateIV();
            using var encryptor = aes.CreateEncryptor();
            var plain = Encoding.UTF8.GetBytes(normalizedE164);
            var cipher = encryptor.TransformFinalBlock(plain, 0, plain.Length);

            var result = new byte[aes.IV.Length + cipher.Length];
            Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
            Buffer.BlockCopy(cipher, 0, result, aes.IV.Length, cipher.Length);
            return Convert.ToBase64String(result);
        }

        public string DecryptForRead(string? encryptedOrPlainValue)
        {
            if (string.IsNullOrWhiteSpace(encryptedOrPlainValue))
                return string.Empty;
            if (!_encEnabled)
                return encryptedOrPlainValue;

            try
            {
                var fullCipher = Convert.FromBase64String(encryptedOrPlainValue);
                if (fullCipher.Length < 17)
                    return encryptedOrPlainValue;

                using var aes = Aes.Create();
                aes.Key = _encKey;
                var iv = new byte[16];
                Buffer.BlockCopy(fullCipher, 0, iv, 0, 16);
                aes.IV = iv;

                var cipher = new byte[fullCipher.Length - 16];
                Buffer.BlockCopy(fullCipher, 16, cipher, 0, cipher.Length);

                using var decryptor = aes.CreateDecryptor();
                var plain = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
                return Encoding.UTF8.GetString(plain);
            }
            catch
            {
                return encryptedOrPlainValue;
            }
        }
    }
}
