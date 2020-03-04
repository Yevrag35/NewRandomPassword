using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MG.NewRandomPassword
{
    internal partial class CryptoEntry : ICryptoEntry
    {
        private readonly uint _seed;
        private readonly char _char;

        uint ICryptoEntry.Seed => _seed;
        char ICryptoEntry.Character => _char;

        private CryptoEntry(char oneChar, bool first)
        {
            _char = oneChar;
            _seed = !first ? GetSeed() : uint.MinValue;
        }

        internal static ICryptoEntry GenerateEntry(char oneChar) => new CryptoEntry(oneChar, false);
        internal static ICryptoEntry GenerateFirstEntry(char oneChar) => new CryptoEntry(oneChar, true);

        internal static char RandomCharFromGroup(char[] group)
        {
            uint seed = GetSeed();
            return group[seed % group.Length];
        }
    }

    internal class CryptoEntryComparer : IComparer<ICryptoEntry>
    {
        public int Compare(ICryptoEntry x, ICryptoEntry y) => x.Seed.CompareTo(y.Seed);
    }
}
