using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;

namespace MG.NewRandomPassword
{
    public partial class CryptoDictionary : IEnumerable<ICryptoEntry>
    {
        #region FIELDS/CONSTANTS
        private List<ICryptoEntry> _list;

        #endregion

        #region INDEXERS
        public ICryptoEntry this[int index] => _list[index];

        #endregion

        #region PROPERTIES
        public int Count => _list.Count;

        #endregion

        #region CONSTRUCTORS
        public CryptoDictionary() => _list = new List<ICryptoEntry>();
        public CryptoDictionary(int passLength) => _list = new List<ICryptoEntry>(passLength);

        #endregion

        #region PUBLIC METHODS
        public void AddEntry(char character)
        {
            ICryptoEntry ice = CryptoEntry.GenerateEntry(character);
            while (_list.Exists(x => x.Seed.Equals(ice.Seed)) || ice.Seed.Equals(uint.MinValue))
            {
                ice = CryptoEntry.GenerateEntry(character);
            }
            _list.Add(ice);
        }

        public void AddFirstEntry(char character)
        {
            if (_list.Exists(x => x.Seed.Equals(uint.MinValue)))
                throw new InvalidOperationException("Only one 1st character can be specified per dictionary.");

            ICryptoEntry ice = CryptoEntry.GenerateFirstEntry(character);
            _list.Add(ice);
        }

        public void AddRandom(char[] characterGroup)
        {
            char oneChar = CryptoEntry.RandomCharFromGroup(characterGroup);
            this.AddEntry(oneChar);
        }

        public string FormPassword()
        {
            if (_list.Count <= 0)
                return null;

            _list.Sort(new CryptoEntryComparer());
            return string.Join(string.Empty, _list.Select(x => x.Character));
        }

        public IEnumerator<ICryptoEntry> GetEnumerator() => _list.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _list.GetEnumerator();

        public char GetIndex(uint index)
        {
            if (!_list.Exists(x => x.Seed.Equals(index)))
                throw new Exception(string.Format("No character entry with a seed of \"{0}\" was found in the dictionary.", index));

            return _list.Find(x => x.Seed.Equals(index)).Character;
        }

        #endregion

        #region BACKEND/PRIVATE METHODS


        #endregion
    }
}