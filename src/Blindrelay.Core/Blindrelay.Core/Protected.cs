
using System;

using System.Security.Cryptography;
using System.Text;

namespace Blindrelay.Core
{
    public class ProtectedString : IEquatable<ProtectedString>
    {
        readonly byte[] data;
        public ProtectedString(string value)
        {
            data = ProtectedData.Protect(Encoding.Default.GetBytes(value), null, DataProtectionScope.CurrentUser);
        }

        public override string ToString()
        {
            return Encoding.Default.GetString(ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser));
        }

        public byte[] HexStringToBytes()
        {
            return Crypto.ConvertHexStringToBinary(ToString());
        }

        public override int GetHashCode()
        {
            return ToString().GetHashCode();
        }

        public bool Equals(ProtectedString other)
        {
            return ToString() == other.ToString();
        }
    }

    public class ProtectedBytes
    {
        readonly byte[] data;
        public ProtectedBytes(byte[] value)
        {
            data = ProtectedData.Protect(value, null, DataProtectionScope.CurrentUser);
        }

        public byte[] ToArray()
        {
            return ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser);
        }
    }
}
