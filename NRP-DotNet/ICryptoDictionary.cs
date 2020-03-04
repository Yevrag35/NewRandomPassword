using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MG.NewRandomPassword
{
    public interface ICryptoDictionary : IEnumerable<KeyValuePair<uint, char>>
    {
        char this[uint index] { get; }
        int Count { get; }
        uint[] Indicies { get; }
        char[] Values { get; }

        void Add(char character);
        void AddFirst(char character);
        bool ContainsIndex(uint index);
        bool ContainsValue(char character);
        IComparer<KeyValuePair<uint, char>> EitherOr();
        char GetCharacter(uint index);
        char GetRandomCharacter();
        uint NewSeed();
        Dictionary<uint, char> ToDictionary();
    }
}
