using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Blindrelay.Core
{
    public class EncryptionType
    {
        public static readonly string Aes256CbcPkcs7 = "AES-256-CBC-PKCS7";
    }

    #region CryptoBuffer
    public abstract class CryptoBuffer : IDisposable
    {
        protected static RandomNumberGenerator rng = RandomNumberGenerator.Create();

        public byte[] Data { get; protected set; }
        public byte[] PlainText { get; protected set; }
        public string PlainMetaData { get; protected set; }
        public string EncryptionAlgorithm { get; protected set; }
        public string Id { get; protected set; }
        public string KeyId { get; protected set; }
        public string Purpose { get; protected set; }
        public string MimeType { get; protected set; }
        public DateTimeOffset Created { get; protected set; }
        public byte[] CreatedBy { get; protected set; }
        public byte[] IV { get; protected set; }
        public byte[] CipherMetaData { get; protected set; }
        public byte[] CipherText { get; protected set; }
        public byte[] Signature { get; protected set; }

        protected CryptoBuffer()
        {

        }

        protected CryptoBuffer(byte[] data)
        {
            Data = data;
        }

        protected static byte[] UnpackBytes(BinaryReader br)
        {
            var length = br.ReadInt32();
            return br.ReadBytes(length);
        }

        protected static string UnpackString(BinaryReader br)
        {
            var bytes = UnpackBytes(br);
            return Encoding.UTF8.GetString(bytes);
        }
        protected static long UnpackInt64(BinaryReader br)
        {
            return br.ReadInt64();
        }

        protected static int UnpackInt32(BinaryReader br)
        {
            return br.ReadInt32();
        }

        protected void ClearUnpacked()
        {
            Id = null;
            KeyId = null;
            EncryptionAlgorithm = null;
            Created = DateTimeOffset.MinValue;
            CreatedBy = null;
            Purpose = null;
            IV = null;
            CipherMetaData = null;
            CipherText = null;
            Signature = null;
            MimeType = null;
        }

        static byte[] HmacSha256Hash(byte[] key, byte[] data, int offset, int count)
        {
            using (var h = new HMACSHA256(key))
            {
                return h.ComputeHash(data, offset, count);
            }
        }

        protected static byte[] brPackMagic = new byte[] { (byte)'B', (byte)'R', (byte)'P', (byte)'K' };

        protected void UnpackAndVerify()
        {
            ClearUnpacked();
            if (Data == null)
                throw new Exception("Nothing to unpack.");

            using (var ms = new MemoryStream(Data, 0, Data.Length, false, true))
            using (var br = new BinaryReader(ms))
            {
                var payloadLength = UnpackInt32(br);
                var magic = UnpackBytes(br);
                if (magic.SequenceEqual(brPackMagic) == false)
                    throw new Exception("Unsupported magic.");
                EncryptionAlgorithm = UnpackString(br);
                CreatedBy = UnpackBytes(br);
                Id = UnpackString(br);
                KeyId = UnpackString(br);
                Purpose = UnpackString(br);
                MimeType = UnpackString(br);
                Created = DateTimeOffset.FromUnixTimeMilliseconds(UnpackInt64(br));
                IV = UnpackBytes(br);
                CipherMetaData = UnpackBytes(br);
                CipherText = UnpackBytes(br);
                Signature = UnpackBytes(br);

                var signingKey = Encoding.UTF8.GetBytes(KeyId);
                var signatureCompare = HmacSha256Hash(signingKey, ms.GetBuffer(), 0, payloadLength);
                if (signatureCompare.SequenceEqual(Signature) == false)
                {
                    ClearUnpacked();
                    throw new Exception("Signature comparison failed.");
                }
            }
            Data = null;
        }

        public void UnpackMetadata() // this method does not verify signature
        {
            ClearUnpacked();
            if (Data == null)
                throw new Exception("Nothing to unpack.");

            using (var ms = new MemoryStream(Data, 0, Data.Length, false, true))
            using (var br = new BinaryReader(ms))
            {
                var payloadLength = UnpackInt32(br);
                var magic = UnpackBytes(br);
                if (magic.SequenceEqual(brPackMagic) == false)
                    throw new Exception("Unsupported magic.");
                EncryptionAlgorithm = UnpackString(br);
                CreatedBy = UnpackBytes(br);
                Id = UnpackString(br);
                KeyId = UnpackString(br);
                Purpose = UnpackString(br);
                MimeType = UnpackString(br);
                Created = DateTimeOffset.FromUnixTimeMilliseconds(UnpackInt64(br));
                IV = UnpackBytes(br);
                CipherMetaData = UnpackBytes(br);
            }
        }

        public abstract void Dispose();
    }

    #endregion


    #region AesCryptoBuffer

    public class Aes256CryptoBuffer : CryptoBuffer
    {
        public Aes256CryptoBuffer()
        {
            EncryptionAlgorithm = "AES-256-CBC-PKCS7";
        }

        public Aes256CryptoBuffer(byte[] data) : base(data)
        {
        }

        static byte[] Aes256Decrypt(byte[] key, byte[] iv, byte[] cipherText)
        {
            try
            {
                using (var aes = Aes.Create())
                {
                    aes.Key = key;
                    aes.IV = iv;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;

                    using (var decryptor = aes.CreateDecryptor(key, iv))
                    using (var ms = new MemoryStream(cipherText))
                    using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                    using (var msr = new MemoryStream())
                    {
                        cs.CopyTo(msr);
                        cs.Close();
                        aes.Clear();
                        return msr.ToArray();
                    }
                }
            }
            catch
            {
                throw new Exception("Decryption failed.");
            }
        }

        public void UnpackAndDecryptX(byte[] key)
        {
            UnpackAndVerify();

            var keyUnpadded = new byte[32];
            Array.Copy(key, 0, keyUnpadded, 0, keyUnpadded.Length);

            if (CipherMetaData != null && CipherMetaData.Length > 0)
                PlainMetaData = Encoding.UTF8.GetString(Aes256Decrypt(keyUnpadded, IV, CipherMetaData)).Trim();
            else
                PlainMetaData = "";
            PlainText = Aes256Decrypt(keyUnpadded, IV, CipherText);
            CipherMetaData = null;
            CipherText = null;
        }

        public static Aes256CryptoBuffer UnpackAndDecrypt(byte[] hashedPassword, string purpose, Aes256CryptoBuffer key, Aes256CryptoBuffer cipherText)
        {
            using (var key1 = new Aes256CryptoBuffer(key.Data))
            {
                key1.UnpackAndDecryptX(hashedPassword);
                if (key1.MimeType != "KeyX")
                    throw new Exception("Not a KeyX.");
                if (key1.Purpose != purpose)
                    throw new Exception("Wrong key for purpose.");

                using (var key2 = new Aes256CryptoBuffer(key1.PlainText))
                {
                    key2.UnpackAndDecryptX(hashedPassword);

                    if (key2.Id != key2.KeyId)
                        throw new Exception("Not a key.");
                    if (key2.Purpose != purpose)
                        throw new Exception("Wrong key for purpose.");

                    cipherText.UnpackAndDecryptX(key2.PlainText);
                    return cipherText;
                }
            }
        }

        public static byte[] UnpackAndDecrypt(byte[] hashedPassword, string purpose, Aes256CryptoBuffer key, out string keyId)
        {
            using (var key1 = new Aes256CryptoBuffer(key.Data))
            {
                key1.UnpackAndDecryptX(hashedPassword);
                if (key1.MimeType != "KeyX")
                    throw new Exception("Not a KeyX.");
                if (key1.Purpose != purpose)
                    throw new Exception("Wrong key for purpose.");

                using (var key2 = new Aes256CryptoBuffer(key1.PlainText))
                {
                    key2.UnpackAndDecryptX(hashedPassword);

                    if (key2.Id != key2.KeyId)
                        throw new Exception("Not a key.");
                    if (key2.Purpose != purpose)
                        throw new Exception("Wrong key for purpose.");
                    keyId = key2.KeyId;

                    var keyUnpadded = new byte[32];
                    Array.Copy(key2.PlainText, 0, keyUnpadded, 0, 32);

                    return keyUnpadded;
                }
            }
        }

        public Aes256CryptoBuffer Clone()
        {
            return new Aes256CryptoBuffer(Data);
        }

        public static byte[] HmacSha256Hash(byte[] key, Stream s)
        {
            using (var h = new HMACSHA256(key))
            {
                return h.ComputeHash(s);
            }
        }

        public static async Task DecompressAsync(Stream input, Stream output, bool leaveOpen)
        {
            using (var zipStream = new GZipStream(input, CompressionMode.Decompress, leaveOpen))
            {
                await zipStream.CopyToAsync(output);
                await zipStream.FlushAsync();
                await output.FlushAsync();
            }
        }

        public class CryptoHeader
        {
            public long PayloadLength { get; set; }
            public byte[] Signature { get; set; }
            public byte[] Magic { get; set; }
            public string EncryptionAlgorithm { get; set; }
            public byte[] CreatedBy { get; set; }
            public string Id { get; set; }
            public string KeyId { get; set; }
            public string Purpose { get; set; }
            public string MimeType { get; set; }
            public DateTimeOffset Created { get; set; }
            public byte[] Iv { get; set; }
            public byte[] CipherMetadata { get; set; }
        }

        public static CryptoHeader ReadHeader(Stream cipherTextStream, bool leaveOpen)
        {
            cipherTextStream.Position = 0;

            var header = new CryptoHeader();

            using (var br = new BinaryReader(cipherTextStream, Encoding.UTF8, leaveOpen))
            {
                header.PayloadLength = UnpackInt64(br);
                header.Magic = UnpackBytes(br);
                if (header.Magic.SequenceEqual(brPackMagic) == false)
                    throw new Exception("Unsupported magic.");
                header.Signature = UnpackBytes(br);
                header.EncryptionAlgorithm = UnpackString(br);
                if (header.EncryptionAlgorithm != EncryptionType.Aes256CbcPkcs7)
                    throw new Exception("Unsupported encryption algorithm.");
                header.CreatedBy = UnpackBytes(br);
                header.Id = UnpackString(br);
                header.KeyId = UnpackString(br);
                header.Purpose = UnpackString(br);
                header.MimeType = UnpackString(br);
                header.Created = DateTimeOffset.FromUnixTimeMilliseconds(UnpackInt64(br));
                header.Iv = UnpackBytes(br);
                header.CipherMetadata = UnpackBytes(br);
            }

            return header;
        }

        public static async Task<string> DecryptAndUncompressStreamAsync(byte[] key, Stream cipherTextStream, Stream tempUncompressStream, Stream plainTextStream)
        {
            if (key == null || key.Length != 32)
                throw new ArgumentNullException("key");

            string metadata = null;

            cipherTextStream.Position = 0;
            tempUncompressStream.Position = 0;
            plainTextStream.Position = 0;

            var header = new CryptoHeader();

            long signaturePosition;

            using (var br = new BinaryReader(cipherTextStream, Encoding.UTF8, true))
            {
                header.PayloadLength = UnpackInt64(br);
                header.Magic = UnpackBytes(br);
                if (header.Magic.SequenceEqual(brPackMagic) == false)
                    throw new Exception("Unsupported magic.");
                signaturePosition = cipherTextStream.Position;
                header.Signature = UnpackBytes(br);
                header.EncryptionAlgorithm = UnpackString(br);
                if (header.EncryptionAlgorithm != EncryptionType.Aes256CbcPkcs7)
                    throw new Exception("Unsupported encryption algorithm.");
                header.CreatedBy = UnpackBytes(br);
                header.Id = UnpackString(br);
                header.KeyId = UnpackString(br);
                header.Purpose = UnpackString(br);
                header.MimeType = UnpackString(br);
                header.Created = DateTimeOffset.FromUnixTimeMilliseconds(UnpackInt64(br));
                header.Iv = UnpackBytes(br);
                header.CipherMetadata = UnpackBytes(br);
                if (header.CipherMetadata != null && header.CipherMetadata.Length > 0)
                    metadata = Encoding.UTF8.GetString(Aes256Decrypt(key, header.Iv, header.CipherMetadata));

                var cipherTextPosition = cipherTextStream.Position;

                cipherTextStream.Seek(signaturePosition + 32 + 4, SeekOrigin.Begin);
                var signingKey = key;
                var signatureCompare = HmacSha256Hash(signingKey, cipherTextStream);
                if (signatureCompare.SequenceEqual(header.Signature) == false)
                    throw new Exception("Signature comparison failed.");

                using (var aes = Aes.Create())
                {
                    aes.Key = key;
                    aes.IV = header.Iv;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;

                    cipherTextStream.Seek((int)cipherTextPosition, SeekOrigin.Begin);

                    using (var decryptor = aes.CreateDecryptor(key, header.Iv))
                    {
                        var cs = new CryptoStream(cipherTextStream, decryptor, CryptoStreamMode.Read);
                        await cs.CopyToAsync(tempUncompressStream);
                        await tempUncompressStream.FlushAsync();
                        aes.Clear();

                        tempUncompressStream.Seek(0, SeekOrigin.Begin);
                        await DecompressAsync(tempUncompressStream, plainTextStream, true);
                    }
                }
            }

            return metadata;
        }

        private bool disposedValue = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    ClearUnpacked();
                }

                disposedValue = true;
            }
        }

        public override void Dispose()
        {
            Dispose(true);
        }

        #endregion
    }
}
