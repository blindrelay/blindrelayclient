using System.IO;

namespace Blindrelay.Core
{
    public static class Serializers
    {
        public static byte[] SerializeProtoBuf(object value)
        {
            using (var ms = new MemoryStream())
            {
                ProtoBuf.Serializer.Serialize(ms, value);
                return ms.ToArray();
            }
        }
        public static T DeserializeProtoBuf<T>(byte[] value)
        {
            using (var ms = new MemoryStream(value))
            {
                return ProtoBuf.Serializer.Deserialize<T>(ms);
            }
        }
    }
}
