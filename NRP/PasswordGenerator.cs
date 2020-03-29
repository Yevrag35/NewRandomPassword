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

        public int CreateLength { get; private set; } = 8;
        public char FirstCharacter { get; set; }
        public bool HasFirstCharacter => this.FirstCharacter > 0;
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

            byte[][] arrays = new byte[this.NumberToCreate][];
            for (int i = 0; i < this.NumberToCreate; i++)
            {
                using (var holder = new PasswordHolder(this.CreateLength))
                {
                    if (this.HasFirstCharacter)
                        holder.AddFirstCharacter(this.FirstCharacter);

                    this.AddRandomFromEachGroup(holder);
                    this.FillOutRestOfPassword(holder, allChars);

                    arrays[i] = holder.CreateAsByteArray();
                }
            }
            return arrays;
        }

        private void AddRandomFromEachGroup(PasswordHolder holder)
        {
            for (int i = 0; i < _charGroups.Length; i++)
            {
                holder.AddRandomCharacter(_charGroups[i]);
            }
        }
        private void FillOutRestOfPassword(PasswordHolder holder, char[] allChars)
        {
            holder.AddRandomCharacter(allChars, this.CreateLength - holder.Length);
        }
        public void SetRandomLength(int minLength, int maxLength)
        {
            if (this.CreateLength != 0)
                return;

            using (var rng = RandomNumberGenerator.Create())
            {
                this.CreateLength = Convert.ToInt32(PasswordHolder.GetSeed(rng) % (maxLength + 1 - minLength) + minLength);
            }
        }
    }
}
