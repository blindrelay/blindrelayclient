using Blindrelay.Core.Api;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace Blindrelay.Core
{
    public partial class Client
    {
        internal T DecryptAes<T>(EncryptedObject encrypted, out string metadata)
        {
            metadata = "";
            if (encrypted == null || encrypted.CipherText == null || string.IsNullOrWhiteSpace(encrypted.KeyId))
                return default;

            using (var cb = new Aes256CryptoBuffer(encrypted.CipherText))
            {
                cb.UnpackMetadata();
                string purpose = cb.Purpose;
                var kc = GetAesKeychain(cb.Purpose);
                var key = kc.GetAesKey(cb.Purpose + "-aes+" + cb.KeyId);

                return key.Aes256.Decrypt<T>(keyKey.ToArray(), encrypted, out metadata);
            }
        }      

        internal Keychain GetAesKeychain(string purpose)
        {
            if (LoggedInUser == null)
                return null;

            var k = purpose + "-aes";
            
            if (keychains.TryGetValue(k, out Keychain kc) == false)
                return null;
            return kc;
        }            

        public static byte[] ComputeMD5Hash(Stream s)
        {
            using (var md5 = MD5.Create())
            {
                return md5.ComputeHash(s);
            }
        }

        public async Task DownloadFileAsync(string fileId, string groupId, Func<Stream, Task> downloadHandler)
        {
            await apiService.UserFileGetAsync(new Api.UserFileGetRequest
            {
                FileId = fileId,
                GroupId = groupId
            }, authToken.ToString(), downloadHandler);
        }      

        internal void ParseFullyQualifiedGroupName(string fqGroupName, out string organizer, out string groupName)
        {
            organizer = "";
            groupName = "";
            var s = fqGroupName.Split(':');
            if (s == null || s.Length != 2)
                return;

            organizer = s[0];
            groupName = s[1];
        }

        public async Task<IEnumerable<UserFileInfo>> GetDownloadableFilesInfoAsync(string groupId, string groupOrganizerUserId, int maxCount)
        {
            var response = await apiService.UserFilesInfoGetAsync(new UserFilesGetRequest
            {
                GroupId = groupId,
                GroupOrganizerUserId = groupOrganizerUserId,
                MaxCount = maxCount

            }, authToken.ToString());

            if (response.UserFiles == null)
                return new List<UserFileInfo>();

            var fi = new List<UserFileInfo>();
            foreach (var uf in response.UserFiles)
            {
                var properties = DecryptAes<UserFileProperties>(uf.Properties, out string metadata);

                ParseFullyQualifiedGroupName(properties.FullyQualifiedGroupName, out string organizer, out string groupName);

                var ufi = new UserFileInfo
                {
                    FileId = uf.FileId,
                    FileName = properties.FileName,
                    FileSize = properties.FileSize,
                    GroupId = uf.GroupId,
                    PublisherEmail = properties.PublisherEmail,
                    PublisherUserId = properties.PublisherUserId,
                    UploadedTime = DateTimeOffset.FromUnixTimeMilliseconds(uf.CreatedTime),
                    OrganizerEmail = organizer,
                    GroupName = groupName
                };

                fi.Add(ufi);
            }

            return fi;
        }

        public async Task<FileMetadata> UnprotectGroupFileAsync(Stream cipherTextStream, Stream tempUncompressStream, Stream plainTextStream)
        {
            cipherTextStream.Position = 0;
            tempUncompressStream.Position = 0;
            plainTextStream.Position = 0;

            var header = Aes256CryptoBuffer.ReadHeader(cipherTextStream, true);
            cipherTextStream.Position = 0;

            var keychain = GetAesKeychain(header.Purpose);
            if (keychain == null)
                throw new Exception("Keychain for file purpose not found.");

            var groupKey = keychain.GetAesKey($"{header.Purpose}-aes+{header.KeyId}");
            if (groupKey == null)
                throw new Exception("Encryption key for file purpose not found.");

            var key = groupKey.Aes256.Expose(keyKey.ToArray(), header.Purpose);

            var metadataJson = await Aes256CryptoBuffer.DecryptAndUncompressStreamAsync(key, cipherTextStream, tempUncompressStream, plainTextStream);

            if (string.IsNullOrWhiteSpace(metadataJson))
                return null;
            return FileMetadata.FromJson(metadataJson);
        }
    }
}
