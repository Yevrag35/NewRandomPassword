using System;

namespace NRP
{
    public interface IPasswordGenerator
    {
        int CreateLength { get; }
        char FirstCharacter { get; set; }
        bool HasFirstCharacter { get; }
        int? MaxLength { get; set; }
        int? MinLength { get; set; }
        int NumberToCreate { get; set; }

        byte[] Generate(IPasswordFormer former, int length, char[] fromChars);
        string[] Generate();
        byte[][] GenerateAsByteArrays();
        int GetRandomLength(ISeeder seeder);
    }
}
