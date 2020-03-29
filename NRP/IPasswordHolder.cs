using System;
using System.Collections.Generic;
using System.Text;

namespace NRP
{
    public interface IPasswordFormer : IDisposable
    {
        int Length { get; }

        void AddFirstCharacter(char character);
        void AddRandomCharacter(char[] fromGroup, int howMany);

        void ClearPasswordFromMemory(ref byte[] plainText);
        byte[] CreateAsByteArray();
        string CreateAsString();
    }
}
