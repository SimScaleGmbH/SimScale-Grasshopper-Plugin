using System;
using System.Security.Cryptography;
using System.Text;

namespace External_Building_Aerodynamics
{
    public static class GuidUtility
    {
        public static Guid CreateDeterministicGuid(string input)
        {
            using (var sha1 = SHA1.Create())
            {
                byte[] hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(input));
                byte[] guidBytes = new byte[16];

                Array.Copy(hash, guidBytes, 16);
                guidBytes[7] &= 0x0F;
                guidBytes[7] |= 0x50;

                return new Guid(guidBytes);
            }
        }
    }
}
