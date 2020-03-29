using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Security.Cryptography;
using System.Text;

namespace NRP
{
    public abstract class PasswordGenerator
    {
        public static IPasswordGenerator Create() => new NRPasswordGenerator();
        public static IPasswordGenerator Create(string[] inputStrings) => new NRPasswordGenerator(inputStrings);
        public static IPasswordGenerator Create(int createLength) => new NRPasswordGenerator(createLength);
        public static IPasswordGenerator Create(int createLength, string[] inputStrings) => new NRPasswordGenerator(createLength, inputStrings);
    }

    internal sealed class NRPasswordGenerator : PasswordGenerator, IPasswordGenerator
    {
        private char[][] _charGroups;
        private bool UseRandomLengths => this.MaxLength.HasValue && this.MinLength.HasValue;

        public int CreateLength { get; private set; } = 4;
        public char FirstCharacter { get; set; }
        public bool HasFirstCharacter => this.FirstCharacter > 0;
        public int? MaxLength { get; set; }
        public int? MinLength { get; set; }
        public int NumberToCreate { get; set; } = 1;


        private NRPasswordGenerator(char[][] charGroups)
        {
            _charGroups = charGroups;
        }
        internal NRPasswordGenerator() : this(Characters.GetDefaultCharGroups())
        {
        }
        internal NRPasswordGenerator(string[] inputStrings) : this(Characters.MakeCharGroups(inputStrings))
        {
        }
        internal NRPasswordGenerator(int length) : this(Characters.GetDefaultCharGroups())
        {
            this.CreateLength = length;
        }
        internal NRPasswordGenerator(int length, string[] inputStrings) : this(Characters.MakeCharGroups(inputStrings))
        {
            this.CreateLength = length;
        }

        public byte[] Generate(IPasswordFormer former, int length, char[] allChars)
        {
            if (this.HasFirstCharacter)
                former.AddFirstCharacter(this.FirstCharacter);

            this.AddRandomFromEachGroup(former);
            this.FillOutRestOfPassword(former, length, allChars);

            return former.CreateAsByteArray();
        }
        public string[] Generate()
        {
            string[] passes = new string[this.NumberToCreate];
            byte[][] byteArrays = this.GenerateAsByteArrays();
            for (int i = 0; i < byteArrays.Length; i++)
            {
                passes[i] = Encoding.ASCII.GetString(byteArrays[i]);
            }
            return passes;
        }
        public byte[][] GenerateAsByteArrays()
        {
            char[] allChars = Characters.ToSingleArray(_charGroups);
            int length = this.CreateLength;
            byte[][] arrays = new byte[this.NumberToCreate][];
            for (int i = 0; i < this.NumberToCreate; i++)
            {
                if (this.UseRandomLengths)
                    length = this.GetRandomLength();

                using (var holder = new PasswordHolder(length))
                {
                    arrays[i] = this.Generate(holder, length, allChars);
                }
            }
            return arrays;
        }

        private void AddRandomFromEachGroup(IPasswordFormer holder)
        {
            for (int i = 0; i < _charGroups.Length; i++)
            {
                holder.AddRandomCharacter(_charGroups[i], 1);
            }
        }
        private void FillOutRestOfPassword(IPasswordFormer holder, int totalLength, char[] allChars)
        {
            holder.AddRandomCharacter(allChars, totalLength - holder.Length);
        }

        public int GetRandomLength(ISeeder seeder)
        {
            if (!this.UseRandomLengths)
                throw new ArgumentException("Generating random lengths requires min and max values as seeds.");

            return Convert.ToInt32(seeder.GetSeed() % (this.MaxLength + 1 - this.MinLength) + this.MinLength);
        }
        public int GetRandomLength()
        {
            if (!this.UseRandomLengths)
                throw new ArgumentException("Generating random lengths requires min and max values as seeds.");

            using (var rng = RandomNumberGenerator.Create())
            {
                return Convert.ToInt32(PasswordHolder.GetSeed(rng) % (this.MaxLength + 1 - this.MinLength) + this.MinLength);
            }
        }
    }
}
