using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Newtonsoft.Json;
using System;
using System.Globalization;

namespace Blindrelay.Core
{
    public class Crypto
    {
        public static string ConvertToHexString(byte[] value)
        {
            return BitConverter.ToString(value).Replace("-", "");
        }

        public static byte[] ConvertHexStringToBinary(string value)
        {
            if (value.Length % 2 != 0)
            {
                throw new ArgumentException("Invalid hex string.");
            }

            byte[] data = new byte[value.Length / 2];
            for (int i = 0; i < data.Length; ++i)
            {
                string byteValue = value.Substring(i * 2, 2);
                data[i] = byte.Parse(byteValue, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }

            return data;
        }

        public static byte[] HashPbkdf2Sha256(string value, byte[] salt, int sizeInBytes)
        {
            return KeyDerivation.Pbkdf2(value, salt, KeyDerivationPrf.HMACSHA256, 10000, sizeInBytes);
        }
    }


    public class EncryptedObject
    {
        [JsonProperty("c")]
        public byte[] CipherText { get; set; }

        [JsonProperty("kid")]
        public string KeyId { get; set; }

        [JsonProperty("et")]
        public long EncryptedTime { get; set; }

        [JsonProperty("cid")]
        public string CorrelationId { get; set; }

        public static EncryptedObject Empty
        {
            get => new EncryptedObject();
        }

        public static EncryptedObject FromAesBytes(byte[] cipherText)
        {
            var aes = new Aes256CryptoBuffer(cipherText);
            aes.UnpackMetadata();

            var r = new EncryptedObject
            {
                CipherText = cipherText,
                KeyId = aes.KeyId,
                EncryptedTime = aes.Created.ToUnixTimeMilliseconds()
            };

            return r;
        }
    }

    public class EncryptionInfo
    {
        public string Id { get; internal set; }
        public string Purpose { get; internal set; }
        public string CreatedByUserId { get; internal set; }
        public string KeyId { get; internal set; }
        public DateTimeOffset Created { get; internal set; }
        public string MimeType { get; set; }
        public string EncryptionAlgorithm { get; set; }
    }

    public class AesKey : IDisposable
    {
        Aes256CryptoBuffer cb;

        EncryptionInfo info = null;
        public EncryptionInfo Info
        {
            get
            {
                if (info == null)
                {
                    var x = cb.Clone();
                    x.UnpackMetadata();
                    info = new EncryptionInfo
                    {
                        Id = x.Id,
                        CreatedByUserId = Crypto.ConvertToHexString(x.CreatedBy),
                        Purpose = x.Purpose,
                        Created = x.Created,
                        KeyId = x.KeyId,
                        MimeType = x.MimeType,
                        EncryptionAlgorithm = x.EncryptionAlgorithm
                    };
                }
                return info;
            }
        }

        public byte[] ToBytes() { return cb.Data; }
        public static AesKey FromBytes(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                throw new Exception("Not a key.");

            return new AesKey
            {
                cb = new Aes256CryptoBuffer(bytes)
            };
        }

        public T Decrypt<T>(byte[] password, EncryptedObject encrypted, out string metadata)
        {
            var plainText = Decrypt(password, encrypted, out metadata);
            return Serializers.DeserializeProtoBuf<T>(plainText);
        }

        public byte[] Decrypt(byte[] password, EncryptedObject encrypted, out string metadata)
        {
            return Decrypt(password, encrypted.CipherText, out metadata);
        }

        public byte[] Decrypt(byte[] password, byte[] cipherText, out string metadata)
        {
            using (var cbc = cb.Clone())
            using (var ct = new Aes256CryptoBuffer(cipherText))
            {
                var cb = Aes256CryptoBuffer.UnpackAndDecrypt(password, Info.Purpose, cbc, ct);
                metadata = cb.PlainMetaData;
                return cb.PlainText;
            }
        }

        public byte[] Expose(byte[] password, string purpose)
        {
            using (var cbc = cb.Clone())
            {
                return Aes256CryptoBuffer.UnpackAndDecrypt(password, purpose, cbc, out string _);
            }
        }

        #region IDisposable Support
        private bool disposedValue = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    if (cb != null)
                    {
                        cb.Dispose();
                        cb = null;
                    }
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);

        }
        #endregion
    }
}
