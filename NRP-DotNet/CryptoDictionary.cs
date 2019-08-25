using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Security.Cryptography;

namespace MG.NewRandomPassword
{
    public partial class CryptoDictionary : IEnumerable
    {
        #region FIELDS/CONSTANTS
        internal const int DEFAULT_PASS_LENGTH = 8;
        //private List<KeyValuePair<uint, char>> _list;
        private List<IEnumerable<DictionaryEntry>> _list;
        private Hashtable _ht;

        #endregion

        #region INDEXERS
        //public char this[uint index]
        //{
        //    get => Convert.ToChar(_ht[index]);
        //}

        #endregion

        #region PROPERTIES
        public int Count => _list.Count;

        #endregion

        #region CONSTRUCTORS
        public CryptoDictionary() => _ht = new Hashtable(DEFAULT_PASS_LENGTH);

        public CryptoDictionary(int capacity) => _ht = new Hashtable(capacity);
        private CryptoDictionary(IDictionary pairs) => _ht = new Hashtable(pairs);

        #endregion

        #region PUBLIC METHODS
        public void Add(char character)
        {
            uint seed = GetSeed();
            while (_ht.ContainsKey(seed))
                seed = GetSeed();

            _ht.Add(seed, character);
        }
        //public IEnumerator<KeyValuePair<uint, char>> GetEnumerator() => this.ToDictionary().GetEnumerator();
        public char GetCharacter(uint index)
        {
            if (_ht.ContainsKey(index))
            {
                object obj = _ht[index];
                return Convert.ToChar(obj);
            }
            else
                throw new ArgumentException(string.Format("No character with a unique index of \"{0}\" exists within this dictionary.", index));
        }
        IEnumerator IEnumerable.GetEnumerator() => _ht.GetEnumerator();
        public char GetRandomCharacter()
        {
            uint seed = GetSeed();
            char[] chars = _ht.Values.Cast<char>().ToArray();
            return chars[seed % chars.Length];
        }

        #endregion

        #region OPERATORS/CASTS
        public static implicit operator CryptoDictionary(Dictionary<uint, char> dict) => new CryptoDictionary(dict);

        #endregion

        #region PRIVATE CLASSES
        private class EntryEquality : IEqualityComparer, IEqualityComparer<DictionaryEntry>
        {
            public bool Equals(DictionaryEntry x, DictionaryEntry y)
            {
                bool result = false;
                if (x.Key is uint numberX && x.Value is char charX && y.Key is uint numberY && y.Value is char charY)
                    result = numberX.Equals(numberY);

                return result;
            }
            public new bool Equals(object x, object y)
            {
                bool result = false;
                if (x is DictionaryEntry deX && y is DictionaryEntry deY)
                    result = this.Equals(deX, deY);

                return result;
            }
            public int GetHashCode(DictionaryEntry obj)
            {
                int hash = obj.GetHashCode();
                if (obj.Key is uint number)
                    hash = number.GetHashCode();

                return hash;
            }
            public int GetHashCode(object obj)
            {
                int hash = obj.GetHashCode();
                if (obj is DictionaryEntry de)
                {
                    hash = this.GetHashCode(de);
                }
                return hash;
            }
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

        #region BACKEND/PRIVATE METHODS
        private Dictionary<uint, char> ToDictionary()
        {
            var dict = new Dictionary<uint, char>(_ht.Count);
            foreach (DictionaryEntry de in _ht)
            {
                dict.Add(Convert.ToUInt32(de.Key), Convert.ToChar(de.Value));
            }
            return dict;
        }

        #endregion
    }
}