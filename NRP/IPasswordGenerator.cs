using System;

namespace NRP
{
    public interface IPasswordGenerator
    {
        int CreateLength { get; }
        char FirstCharacter { get; set; }
        bool HasFirstCharacter { get; }
        int NumberToCreate { get; set; }

        string[] Generate();
        byte[][] GenerateAsByteArrays();
        void SetRandomLength(int minLength, int maxLength);
    }
}
