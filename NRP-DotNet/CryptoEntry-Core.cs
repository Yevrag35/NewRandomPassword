using System;
using System.Security.Cryptography;

namespace MG.NewRandomPassword
{
    internal partial class CryptoEntry
    {
        internal static uint GetSeed()
        {
            using (var rng = RandomNumberGenerator.Create())
            {
                byte[] rBytes = new byte[4];
                rng.GetBytes(rBytes);
                return BitConverter.ToUInt32(rBytes, 0);
            }
        }
    }
}
