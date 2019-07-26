using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Security.Cryptography;

namespace MG.NewRandomPassword.Cmdlets
{
    [Cmdlet(VerbsCommon.New, "RandomPassword", ConfirmImpact = ConfirmImpact.None,
        DefaultParameterSetName = "StaticLength")]
    [CmdletBinding(PositionalBinding = false)]
    [OutputType(typeof(string))]
    [Alias("rpas")]
    public class NewRandomPassword : PSCmdlet
    {
        [Parameter(Mandatory = false, Position = 0, ParameterSetName = "StaticLength")]
        [ValidateRange(1, 2147483647)]
        [Alias(new string[2] { "l", "length" })]
        public int PasswordLength = 8;

        [Parameter(Mandatory = true, ParameterSetName = "RandomLength")]
        [ValidateRange(1, 2147483646)]
        [Alias("min", "minlength")]
        public int MinimumLength { get; set; }

        [Parameter(Mandatory = true, ParameterSetName = "RandomLength")]
        [ValidateRange(2, 2147483647)]
        [Alias("max", "maxlength")]
        public int MaximumLength { get; set; }

        [Parameter(Mandatory = false)]
        [ValidateSet(
            "abcdefghijkmnpqrstuvwxyz",
            "ABCEFGHJKLMNPQRSTUVWXYZ",
            "1234567890",
            "234567890",
            "23456789",
            "!@$#%&",
            "!#%&")]
        public string[] InputStrings = new string[4]
        {
            "abcdefghijkmnpqrstuvwxyz",
            "ABCEFGHJKLMNPQRSTUVWXYZ",
            "23456789",
            "!@$#%&"
        };

        [Parameter(Mandatory = false)]
        [Alias("fc", "FirstChar")]
        public char FirstCharacter { get; set; }

        [Parameter(Mandatory = false, Position = 1)]
        [ValidateRange(1, 2147483647)]
        public int Count = 1;

        protected override void ProcessRecord()
        {
            base.ProcessRecord();

            // Create char arrays containing groups of possible characters.
            char[][] charGroups = MakeCharGroups(InputStrings);
            // Create a single char array encompassing all possible characters.
            var allChars = AllCharactersOfStrings(charGroups);

            for (int i = 0; i < Count; i++)
            {
                var Password = new Dictionary<uint, char>();

                // If RandomLength is desired, then set the password length per iteration.
                if (ParameterSetName == "RandomLength")
                {
                    if (MinimumLength.Equals(MaximumLength))
                        PasswordLength = MinimumLength;

                    else if (MaximumLength > MinimumLength)
                        PasswordLength = Convert.ToInt32((GetSeed() % (MaximumLength + 1 - MinimumLength)) + MinimumLength);

                    else
                        throw new ArgumentException("The minimum length can NOT be larger than the maximum length!");

                }

                // If 'FirstChar' is defined, randomize first char in password from that string.
                if (MyInvocation.BoundParameters.ContainsKey("FirstCharacter"))
                    Password.Add(0, FirstCharacter);

                // Randomize one character from each group
                for (int g = 0; g < charGroups.Length; g++)
                {
                    var grp = charGroups[g];
                    if (Password.Count < PasswordLength)
                    {
                        uint index = GetSeed();
                        while (Password.ContainsKey(index))
                            index = GetSeed();

                        Password.Add(index, GetRandomChar(GetSeed(), grp));
                    }
                }

                // Fill out with chars from allChars...
                for (int p = Password.Count; p < PasswordLength; p++)
                {
                    uint index = GetSeed();
                    while (Password.ContainsKey(index))
                        index = GetSeed();

                    Password.Add(index, GetRandomChar(GetSeed(), allChars));
                }

                // ... and put it all back together again.
                var passChars = new char[PasswordLength];
                for (int c = 0; c < Password.Count; c++)
                {
                    uint[] keys = Password.Keys.ToArray();
                    for (int k = 0; k < keys.Length; k++)
                    {
                        var key = keys[k];
                        var oneChar = Password[key];
                        passChars[k] = oneChar;
                    }
                }

                WriteObject(new string(passChars));
            }
        }

        private uint GetSeed()
        {
            using (var rng = new RNGCryptoServiceProvider())
            {
                var rBytes = new byte[4];
                rng.GetBytes(rBytes);
                return BitConverter.ToUInt32(rBytes, 0);
            }
        }

        private char[][] MakeCharGroups(string[] strings)
        {
            char[][] charGroups = new char[strings.Length][];
            for (int i = 0; i < strings.Length; i++)
            {
                var s = strings[i];
                charGroups[i] = s.ToCharArray();
            }
            return charGroups;
        }

        private char[] AllCharactersOfStrings(char[][] charGroups)
        {
            var allChars = new List<char>();
            for (int i = 0; i < charGroups.Length; i++)
            {
                var g = charGroups[i];
                for (int c = 0; c < g.Length; c++)
                {
                    allChars.Add(g[c]);
                }
            }
            return allChars.ToArray();
        }

        private char GetRandomChar(uint seed, char[] group) => 
            group[seed % group.Length];
    }
}
