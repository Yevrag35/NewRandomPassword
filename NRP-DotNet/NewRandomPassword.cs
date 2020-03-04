using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Management.Automation;
using System.Reflection;

namespace MG.NewRandomPassword
{
    [Cmdlet(VerbsCommon.New, "RandomPassword", ConfirmImpact = ConfirmImpact.None)]
    [OutputType(typeof(string))]
    [CmdletBinding(PositionalBinding = false)]
    public class NewRandomPassword : PSCmdlet
    {
        #region FIELDS/CONSTANTS
        private List<string> _inStrs;
        private bool _onlyec;
        private bool _randStatic;

        #endregion

        #region PARAMETERS
        [Parameter(Mandatory = false, Position = 0, ParameterSetName = "StaticLength")]
        [ValidateRange(1, int.MaxValue)]
        [Alias("l", "length")]
        public int PasswordLength = 8;

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

        [Parameter(Mandatory = false, ParameterSetName = "RandomLength")]
        public SwitchParameter AllStringsTheSameLength
        {
            get => _randStatic;
            set => _randStatic = value;
        }

        [Parameter(Mandatory = false)]
        [Alias("fc", "FirstChar")]
        public char FirstCharacter { get; set; }

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
            char[][] charGroups = this.MakeCharGroups(this.InputStrings);
            // Create a single char array encompassing all possible characters.
            char[] allChars = this.AllCharactersOfStrings(charGroups);

            if (_randStatic)
                this.PasswordLength = this.NewRandomLength();

            for (int i = 0; i < this.Count; i++)
            {
                int length = this.PasswordLength;
                if (this.ParameterSetName == "RandomLength" && !_randStatic)
                {
                    if (!_randStatic)
                    {
                        if (this.MinimumLength == this.MaximumLength)
                            length = this.MinimumLength;

                        else if (this.MaximumLength < this.MinimumLength)
                            throw new ArgumentException("The minimum length can NOT be larger than the maximum length!");

                        else
                            length = this.NewRandomLength();
                    }
                }

                // new password object
                var password = new CryptoDictionary(length);


                // If 'FirstChar' is defined, randomize first char in password from that string.
                if (MyInvocation.BoundParameters.ContainsKey("FirstCharacter"))
                    password.AddFirstEntry(this.FirstCharacter);

                // Randomize one character from each group
                for (int g = 0; g < charGroups.Length; g++)
                {
                    char[] grp = charGroups[i];
                    if (password.Count < length)
                    {
                        password.AddRandom(grp);
                    }
                }

                // Fill out with chars from allChars...

                for (int p = password.Count; p < length; p++)
                {
                    password.AddRandom(allChars);
                }

                // ... and put it all back together again in order.
                base.WriteObject(password.FormPassword());
            }
        }

        #endregion

        #region BACKEND METHODS
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

        private int NewRandomLength() => Convert.ToInt32((CryptoEntry.GetSeed() % (this.MaximumLength + 1 - this.MinimumLength)) + this.MinimumLength);

        #endregion
    }
}