using Blindrelay.Core.Api;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Blindrelay.Core
{
    public partial class Client
    {
        private static string GetKeychainIdFromKeyId(string keyId)
        {
            var i = keyId.IndexOf("-aes+");
            if (i > 0)
                return keyId.Substring(0, i + 4);
            return "";
        }

        private static string GetPurposeFromKeyId(string keyId)
        {
            var i = keyId.IndexOf("-");
            if (i > 0)
                return keyId.Substring(0, i);
            return "";
        }

        private async Task RefreshKeyCacheAsync()
        {
            keychains.Clear();
            var uid = userId.ToString();

            var response = await apiService.UserAesKeysGetAsync(new UserAesKeysGetRequest
            {
            }, authToken.ToString());

            if (response.Keys == null || response.Keys.Any() == false)
                return;

            var keychainData = new List<UserKeychainData>();

            foreach (var k in response.Keys)
            {
                var purpose = GetPurposeFromKeyId(k.Id);
                var kcid = GetKeychainIdFromKeyId(k.Id);

                keychainData.Add(new UserKeychainData
                {
                    CreatedTime = 0,
                    Id = kcid,
                    Purpose = purpose,
                    UserId = uid
                });
            }

            var dbKeychainKeys = response.Keys;

            foreach (var kc in keychainData)
            {
                var kck = dbKeychainKeys
                    .Where(x => GetKeychainIdFromKeyId(x.Id) == kc.Id)
                    .Select(x => new KeychainKey(x, kc.Purpose))
                    .ToArray();

                keychains[kc.Id] = new Keychain(kc, kck);
            }
        }
    }

    public class Keychain
    {
        readonly UserKeychainData data;

        public Keychain(UserKeychainData data, KeychainKey[] keys)
        {
            this.data = data;
            Keys = new List<KeychainKey>(keys);
        }

        public IEnumerable<KeychainKey> Keys { get; }
        public string Purpose { get => data.Purpose; }


        public KeychainKey CurrentKey
        {
            get => Keys.OrderByDescending(x => x.CreatedTime).First();
        }

        public KeychainKey GetKey(string keyId)
        {
            return Keys.Where(x => x.KeyId == keyId).SingleOrDefault();
        }

        public KeychainKey GetAesKey(string keyId)
        {
            return Keys.Where(x => x.KeyId == keyId).SingleOrDefault();
        }

        public byte[] Decrypt(byte[] password, EncryptedObject encrypted, out string metadata)
        {
            metadata = "";
            if (encrypted == null || string.IsNullOrWhiteSpace(encrypted.KeyId))
                return default;

            var k = GetKey(encrypted.KeyId);
            if (k == null)
                return default;
            if (k.Aes256 != null)
                return k.Aes256.Decrypt(password, encrypted, out metadata);

            return default;
        }
    }

    public class KeychainKey
    {
        public KeychainKey(UserKeychainKeyData data, string purpose)
        {
            this.data = data;
            this.Purpose = purpose;
        }

        private readonly UserKeychainKeyData data;
        public AesKey Aes256 { get => data.Aes256 != null ? AesKey.FromBytes(data.Aes256) : null; }
        public string Purpose { get; }
        public string KeyId { get => data.Id; }
        public long CreatedTime { get => data.CreatedTime; }
    }
}
