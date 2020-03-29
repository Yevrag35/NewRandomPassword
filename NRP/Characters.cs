using System;
using System.Linq;

namespace NRP
{
    public static class Characters
    {
        public const string AZ_LOWERCASE = "abcdefghijkmnpqrstuvwxyz";
        public const string AZ_UPPERCASE = "ABCEFGHJKLMNPQRSTUVWXYZ";
        public const string NUMBERS_1_0 = "1234567890";
        public const string NUMBERS_2_0 = "234567890";
        public const string NUMBERS_2_9 = "23456789";
        public const string SYMBOLS_1 = "!@$#%&";
        public const string SYMBOLS_2 = "!#%&";

        public static char[][] GetDefaultCharGroups()
        {
            return MakeCharGroups(AZ_LOWERCASE, AZ_UPPERCASE, NUMBERS_2_9, SYMBOLS_1);
        }

        public static char[][] MakeCharGroups(params string[] inputStrings)
        {
            if (inputStrings == null || inputStrings.Length <= 0)
                return null;

            char[][] charGroups = new char[inputStrings.Length][];
            for (int i = 0; i < inputStrings.Length; i++)
            {
                charGroups[i] = inputStrings[i].ToCharArray();
            }
            return charGroups;
        }

        public static char[] ToSingleArray(char[][] charGroups)
        {
            return charGroups.SelectMany(x => x).ToArray();
        }
    }
}
