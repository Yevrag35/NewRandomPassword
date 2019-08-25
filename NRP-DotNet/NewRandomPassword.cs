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
        #region PARAMETERS

        [Parameter(Mandatory = false, Position = 0, ParameterSetName = "StaticLength")]
        [ValidateRange(1, int.MaxValue)]
        [Alias("l", "length")]
        public int PasswordLength = CryptoDictionary.DEFAULT_PASS_LENGTH;

        [Parameter(Mandatory = false, Position = 1)]
        [ValidateRange(1, int.MaxValue)]
        public int Count = 1;

        [Parameter(Mandatory = true, ParameterSetName = "RandomLength")]
        [ValidateRange(1, int.MaxValue - 1)]
        [Alias("min", "minlength")]
        public int MinimumLength { get; set; }

        [Parameter(Mandatory = true, ParameterSetName = "RandomLength")]
        [ValidateRange(2, int.MaxValue)]
        [Alias("max", "maxlength")]
        public int MaximumLength { get; set; }

        [Parameter(Mandatory = false)]
        [Alias("strings")]
        [ValidateSet(
            "abcdefghijkmnpqrstuvwxyz",
            "ABCEFGHJKLMNPQRSTUVWXYZ",
            "1234567890",
            "234567890",
            "23456789",
            "!@$#%&",
            "!#%&")]
        public string[] InputStrings
        {
            get
            {
                if (_inStrs == null)
                    _inStrs = new List<string>(4);

                return _inStrs.ToArray();
            }
            set
            {
                if (_inStrs == null)
                    _inStrs = new List<string>(4);

                _inStrs.Clear();
                _inStrs.AddRange(value);
            }
        }
        //public string[] InputStrings = new string[4]
        //{
            //"abcdefghijkmnpqrstuvwxyz",
            //"ABCEFGHJKLMNPQRSTUVWXYZ",
            //"23456789",
            //"!@$#%&"
        //};

        [Parameter(Mandatory = false)]
        [Alias("chars", "ec")]
        public object ExtraCharacters { get; set; }

        [Parameter(Mandatory = false)]
        [Alias("onlyec")]
        public SwitchParameter OnlyUseExtraCharacters
        {
            get => _onlyec;
            set => _onlyec = value;
        }

        [Parameter(Mandatory = false)]
        [Alias("fc", "FirstChar")]
        public char FirstCharacter { get; set; }

        #endregion

        #region FIELDS/CONSTANTS
        private List<string> _inStrs;
        private bool _onlyec;

        #endregion

        #region CMDLET PROCESSING

        protected override void BeginProcessing()
        {
            if (!this.MyInvocation.BoundParameters.ContainsKey("InputStrings"))
            {
                this.InputStrings = new string[4]
                {
                    "abcdefghijkmnpqrstuvwxyz",
                    "ABCEFGHJKLMNPQRSTUVWXYZ",
                    "23456789",
                    "!@$#%&"
                };
            }

            // If RandomLength is desired, then set the password length per iteration.
            if (ParameterSetName == "RandomLength")
            {
                if (this.MinimumLength.Equals(MaximumLength))
                    this.PasswordLength = MinimumLength;

                else if (MaximumLength > MinimumLength)
                    this.PasswordLength = Convert.ToInt32((CryptoDictionary.GetSeed() % (MaximumLength + 1 - MinimumLength)) + MinimumLength);

                else
                    throw new ArgumentException("The minimum length can NOT be larger than the maximum length!");
            }

            

            if (this.MyInvocation.BoundParameters.ContainsKey("ExtraCharacters"))
            {
                if (_onlyec)
                    _inStrs.Clear();

                if (!(this.ExtraCharacters is object[]))
                {
                    string ecStr = null;

                    if (this.ExtraCharacters is char[] ecArray)
                        ecStr = new string(ecArray);

                    else if (this.ExtraCharacters is string oneEcStr)
                        ecStr = oneEcStr;

                    else
                        throw new ArgumentException("The argument for \"ExtraCharacters\" must only be a string, string[], or char[].");

                    _inStrs.Add(ecStr);
                }
                else
                {
                    object[] objArr = this.ExtraCharacters as object[];
                    for (int i = 0; i < objArr.Length; i++)
                    {
                        if (objArr[i] is string oneStr)
                            _inStrs.Add(oneStr);
                    }
                }
            }
        }
        
        protected override void ProcessRecord()
        {
            // Create char arrays containing groups of possible characters.
            char[][] charGroups = MakeCharGroups(this.InputStrings);
            // Create a single char array encompassing all possible characters.
            char[] allChars = AllCharactersOfStrings(charGroups);

            for (int i = 0; i < Count; i++)
            {
                var Password = new Dictionary<uint, char>();

                // If 'FirstChar' is defined, randomize first char in password from that string.
                if (MyInvocation.BoundParameters.ContainsKey("FirstCharacter"))
                    Password.Add(0, FirstCharacter);

                // Randomize one character from each group
                for (int g = 0; g < charGroups.Length; g++)
                {
                    char[] grp = charGroups[g];
                    if (Password.Count < PasswordLength)
                    {
                        uint index = CryptoDictionary.GetSeed();
                        while (Password.ContainsKey(index))
                            index = CryptoDictionary.GetSeed();

                        Password.Add(index, GetRandomChar(CryptoDictionary.GetSeed(), grp));
                    }
                }

                // Fill out with chars from allChars...
                for (int p = Password.Count; p < PasswordLength; p++)
                {
                    uint index = CryptoDictionary.GetSeed();
                    while (Password.ContainsKey(index))
                        index = CryptoDictionary.GetSeed();

                    Password.Add(index, GetRandomChar(CryptoDictionary.GetSeed(), allChars));
                }

                // ... and put it all back together again in order.
                var passChars = new List<KeyValuePair<uint, char>>(Password);
                var comparer = this.EitherOr();
                passChars.Sort(comparer);

                char[] outPass = new char[passChars.Count];
                for (int c = 0; c < passChars.Count; c++)
                {
                    outPass[c] = passChars[c].Value;
                }
                base.WriteObject(new string(outPass));
            }
        }

        #endregion

        #region PRIVATE/BACKEND METHODS

        private char[] AllCharactersOfStrings(char[][] charGroups)
        {
            var allChars = new List<char>();
            for (int i = 0; i < charGroups.Length; i++)
            {
                char[] g = charGroups[i];
                for (int c = 0; c < g.Length; c++)
                {
                    allChars.Add(g[c]);
                }
            }
            return allChars.ToArray();
        }

        private IComparer<KeyValuePair<uint, char>> EitherOr()
        {
            var seed1 = CryptoDictionary.GetSeed();
            var seed2 = CryptoDictionary.GetSeed();
            if (seed1 == seed2) // wow...
                return EitherOr();

            else return seed1 < seed2
                    ? new LetterAscendingComparer() 
                    : (IComparer<KeyValuePair<uint, char>>)new LetterDescendingComparer();
        }

        private char GetRandomChar(uint seed, char[] group) => group[seed % group.Length];

        [Obsolete]
        private uint GetSeed()
        {
            using (var rng = new RNGCryptoServiceProvider())
            {
                byte[] rBytes = new byte[4];
                rng.GetBytes(rBytes);
                return BitConverter.ToUInt32(rBytes, 0);
            }
        }

        private char[][] MakeCharGroups(string[] strings)
        {
            char[][] charGroups = new char[strings.Length][];
            for (int i = 0; i < strings.Length; i++)
            {
                string s = strings[i];
                charGroups[i] = s.ToCharArray();
            }
            return charGroups;
        }

        private class LetterAscendingComparer : IComparer<KeyValuePair<uint, char>>
        {
            public int Compare(KeyValuePair<uint, char> x, KeyValuePair<uint, char> y)
            {
                return x.Key.CompareTo(y.Key);
            }
        }

        private class LetterDescendingComparer : IComparer<KeyValuePair<uint, char>>
        {
            public int Compare(KeyValuePair<uint, char> x, KeyValuePair<uint, char> y)
            {
                return x.Key.CompareTo(y.Key) * -1;
            }
        }

        #endregion
    }
}
