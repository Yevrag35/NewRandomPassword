using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;

namespace NRP
{
    internal sealed class PasswordHolder : IPasswordFormer, ISeeder
    {
        private IComparer<(uint, char)> _comparer;
        private bool disposed;
        private GCHandle _handle;
        private short _hasFirst;
        private List<(uint, char)> _list;
        private RandomNumberGenerator _rng;
        private SecureString _secStr;

        public int Length => _list.Count + _hasFirst;

        private PasswordHolder(bool internalConst)
        {
            if (internalConst)
            {
                _comparer = new TupleComparer();
                _rng = RandomNumberGenerator.Create();
                _secStr = new SecureString();
            }
        }
        //public PasswordHolder() : this(true) => _list = new List<(uint, char)>(8);
        public PasswordHolder(int capacity) : this(true) => _list = new List<(uint, char)>(capacity);

        private void AddCharacter(char character)
        {
            uint seed = this.GetSeed();
            while (seed == 0u || _list.Exists(x => x.Item1 == seed))
            {
                seed = this.GetSeed();
            }
            _list.Add((seed, character));
        }
        public void AddFirstCharacter(char character)
        {
            if (_hasFirst < 1)
            {
                _secStr.AppendChar(character);
                _hasFirst = 1;
            }
        }
        public void AddRandomCharacter(char[] group, int howMany = 1)
        {
            for (int i = 0; i < howMany; i++)
            {
                this.AddCharacter(group[this.GetSeed() % group.Length]);
            }
        }

        public byte[] CreateAsByteArray()
        {
            _list.Sort(_comparer);
            _list.ForEach((x) =>
            {
                _secStr.AppendChar(x.Item2);
            });

            byte[] plainText = null;
            _handle = GCHandle.Alloc(plainText, GCHandleType.Pinned);

            IntPtr unManagedPtr = IntPtr.Zero;

            SecureStringToByteArray(unManagedPtr, _secStr, ref plainText);
            Marshal.ZeroFreeBSTR(unManagedPtr);

            return plainText;
        }
        public string CreateAsString()
        {
            if (this.Length <= 0)
                return null;

            _list.Sort(_comparer);
            char[] chars = new char[_list.Count];
            for (int i = 0; i < _list.Count; i++)
            {
                chars[i] = _list[i].Item2;
            }

            return new string(chars);
        }

        public void ClearPasswordFromMemory(ref byte[] plainText)
        {
            this.Dispose();
            Array.Clear(plainText, 0, plainText.Length);
            _handle.Free();
            //if (ptr != IntPtr.Zero)
            //    Marshal.ZeroFreeBSTR(ptr);
        }
        private static void SecureStringToByteArray(IntPtr ptr, SecureString cipherText, ref byte[] plainText)
        {
            ptr = Marshal.SecureStringToBSTR(cipherText);

            plainText = new byte[cipherText.Length];

            unsafe
            {
                // Copy without null bytes
                byte* bstr = (byte*)ptr;
                for (int i = 0; i < plainText.Length; i++)
                {
                    plainText[i] = *bstr++;
                    *bstr = *bstr++;
                }
            }
        }

        #region COMPARER
        private class TupleComparer : IComparer<(uint, char)>
        {
            public int Compare((uint, char) x, (uint, char) y) => x.Item1.CompareTo(y.Item1);
        }

        #endregion

        #region IDISPOSABLE
        public void Dispose()
        {
            if (disposed)
                return;

            _list.Clear();
            _rng.Dispose();
            _secStr.Dispose();
            disposed = true;
            GC.SuppressFinalize(this);
        }

        #endregion

        #region ENUMERATORS
        //public IEnumerator<(uint, char)> GetEnumerator() => _list.GetEnumerator();
        //IEnumerator IEnumerable.GetEnumerator() => _list.GetEnumerator();

        #endregion

        #region SEEDER
        private uint GetSeed()
        {
            return GetSeed(_rng);
        }
        uint ISeeder.GetSeed() => this.GetSeed();
        public static uint GetSeed(RandomNumberGenerator rng)
        {
            byte[] bytes = new byte[4];
            rng.GetBytes(bytes);
            return BitConverter.ToUInt32(bytes, 0);
        }

        #endregion
    }
}
