using Newtonsoft.Json;
using ProtoBuf;
using System;

namespace Blindrelay.Core.Api
{

    #region Remote

    public class RemoteData
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("ct")]
        public long CreatedTime { get; set; }
    }

    #endregion

    #region User

    internal class UserLoginRequest
    {
        [JsonRequired]
        [JsonProperty("e")]
        public string Email { get; set; }

        [JsonRequired]
        [JsonProperty("p")]
        public string Password { get; set; }

        [JsonRequired]
        [JsonProperty("aid")]
        public string ApplicationId { get; set; }

        [JsonRequired]
        [JsonProperty("ak")]
        public string ApiKey { get; set; }
    }

    internal class UserLoginResponse
    {
        [JsonProperty("uid")]
        public string UserId { get; set; }

        [JsonProperty("l")]
        public bool Locked { get; set; }

        [JsonProperty("hl")]
        public bool HardLocked { get; set; }

        [JsonProperty("ec")]
        public bool EmailConfirmed { get; set; }
        [JsonProperty("lm")]
        public string LockedMessage { get; set; }

        [JsonProperty("atk")]
        public string AuthToken { get; set; }

        [JsonProperty("atks")]
        public int AuthTokenExpirationMinutes { get; set; }

        [JsonProperty("ehs")]
        public byte[] EncryptionHashSalt { get; set; }
    }

    public class TokenRenewRequest
    {
    }

    public class TokenRenewResponse
    {
        [JsonProperty("atk")]
        public string AuthToken { get; set; }
    }

    public class UserAesKeysGetRequest
    {
    }

    public class UserAesKeysGetResponse
    {
        [JsonProperty("k")]
        public UserKeychainKeyData[] Keys { get; set; }
    }

    internal class UserGroupPermissionsGetRequest
    {

    }

    public class UserGroupPermissions
    {
        [JsonRequired]
        [JsonProperty("gid")]
        public string GroupId { get; set; }

        [JsonRequired]
        [JsonProperty("uid")]
        public string UserId { get; set; }

        [JsonRequired]
        [JsonProperty("p")]
        public bool CanPublish { get; set; }

        [JsonRequired]
        [JsonProperty("pt")]
        public long BecamePublisherTime { get; set; }

        [JsonRequired]
        [JsonProperty("d")]
        public bool CanSubscribe { get; set; }

        [JsonRequired]
        [JsonProperty("st")]
        public long BecameSubscriberTime { get; set; }

        [JsonRequired]
        [JsonProperty("oid")]
        public string GroupOrganizerUserId { get; set; }
    }

    internal class UserGroupPermissionsGetRespose
    {
        [JsonProperty("p")]
        public UserGroupPermissions[] UserGroupPermissions { get; set; }
    }

    #endregion

    #region UserFiles   

    public class UserFileData : RemoteData
    {
        [JsonRequired]
        [JsonProperty("fileId")]
        public string FileId { get; set; }

        [JsonRequired]
        [JsonProperty("gid")]
        public string GroupId { get; set; }

        [JsonRequired]
        [JsonProperty("p")]
        public EncryptedObject Properties { get; set; }
    }

    [ProtoContract]
    public class UserFileProperties
    {
        [ProtoMember(1)]
        public string FileName { get; set; }
        [ProtoMember(2)]
        public long FileSize { get; set; }
        [ProtoMember(3)]
        public string PublisherEmail { get; set; }
        [ProtoMember(4)]
        public string PublisherUserId { get; set; }
        [ProtoMember(5)]
        public string GroupId { get; set; }
        [ProtoMember(6)]
        public string FullyQualifiedGroupName { get; set; }

        public byte[] ToBytes()
        {
            return Serializers.SerializeProtoBuf(this);
        }
        public static UserFileProperties FromBytes(byte[] value)
        {
            return Serializers.DeserializeProtoBuf<UserFileProperties>(value);
        }
    }

    public class UserFileInfo
    {
        public string FileName { get; set; }
        public long FileSize { get; set; }
        public string FileId { get; set; }
        public string GroupId { get; set; }
        public string PublisherEmail { get; set; }
        public string PublisherUserId { get; set; }
        public DateTimeOffset UploadedTime { get; set; }
        public string GroupName { get; set; }
        public string OrganizerEmail { get; set; }
    }

    public class FileMetadata
    {
        [JsonRequired]
        [JsonProperty("fn")]
        public string FileName { get; set; }

        [JsonRequired]
        [JsonProperty("fs")]
        public long FileSize { get; set; }

        [JsonRequired]
        [JsonProperty("md5")]
        public byte[] MD5Hash { get; set; }

        public static FileMetadata FromJson(string json)
        {
            return JsonConvert.DeserializeObject<FileMetadata>(json);
        }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }
    }


    public class UserFileIdsGetRequest
    {
        [JsonRequired]
        [JsonProperty("gid")]
        public string GroupId { get; set; }
    }

    public class UserFileIdsGetResponse
    {
        [JsonProperty("fid")]
        public string[] UserFileIds { get; set; }
    }

    public class UserFileGetRequest
    {
        [JsonRequired]
        [JsonProperty("gid")]
        public string GroupId { get; set; }

        [JsonRequired]
        [JsonProperty("fid")]
        public string FileId { get; set; }
    }

    public class UserFilesGetRequest
    {
        [JsonRequired]
        [JsonProperty("gid")]
        public string GroupId { get; set; }

        [JsonRequired]
        [JsonProperty("oid")]
        public string GroupOrganizerUserId { get; set; }

        [JsonRequired]
        [JsonProperty("mc")]
        public int MaxCount { get; set; }
    }

    public class UserFilesGetResponse
    {
        [JsonProperty("f")]
        public UserFileData[] UserFiles { get; set; }
    }


    #endregion

    #region Notifications

    internal class SignalRHubConnectNotificationRequest
    {

    }

    internal class SignalRHubConnectNotificationResponse
    {
        [JsonProperty("at")]
        public string AccessToken { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }
    }

    internal class NotificationsGroupAddRemoveRequest
    {
        [JsonRequired]
        [JsonProperty("gid")]
        public string GroupId { get; set; }
    }

    internal class NotificationsGroupAddRemoveResponse
    {
    }

    [ProtoContract]
    public class FilePublishedNotification
    {
        [ProtoMember(1)]
        public string PublisherUserId { get; set; }

        [ProtoMember(2)]
        public string GroupId { get; set; }

        public byte[] ToBytes()
        {
            return Serializers.SerializeProtoBuf(this);
        }

        public static FilePublishedNotification FromBytes(byte[] b)
        {
            if (b == null)
                return null;
            return Serializers.DeserializeProtoBuf<FilePublishedNotification>(b);
        }

    }


    #endregion

    #region Keys

    public class UserKeychainData : RemoteData
    {
        [JsonRequired]
        [JsonProperty("userId")]
        public string UserId { get; set; }

        [JsonRequired]
        [JsonProperty("p")]
        public string Purpose { get; set; }
    }

    public class UserKeychainKeyData : RemoteData
    {
        [JsonRequired]
        [JsonProperty("userId")]
        public string UserId { get; set; }

        [JsonProperty("aes256")]
        public byte[] Aes256 { get; set; }

        public UserKeychainKeyData Clone()
        {
            return new UserKeychainKeyData
            {
                Aes256 = this.Aes256,
                CreatedTime = this.CreatedTime,
                Id = this.Id,
                UserId = this.UserId
            };
        }
    }


    #endregion
}
