using System;
using System.Collections;
using System.Collections.Generic;
using System.Security;
using System.Security.Cryptography;

namespace NRP
{
    public class PasswordGenerator : IDisposable, IEnumerable<char>
    {
        private bool disposed;
        private List<char> _list;
        private RandomNumberGenerator _rng;

        public int Length => _list.Count;

        private PasswordGenerator(bool internalConst)
        {
            if (internalConst)
            {
                _rng = RandomNumberGenerator.Create();
            }
        }
        public PasswordGenerator() : this(true) => _list = new List<char>(4);
        public PasswordGenerator(int capacity) : this(true) => _list = new List<char>(capacity);

        publci 


        #region IDISPOSABLE
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                _rng.Dispose();
                disposed = true;
            }
        }

        #endregion

        #region ENUMERATORS
        public IEnumerator<char> GetEnumerator() => ((IEnumerable<char>)_list).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<char>)_list).GetEnumerator();

        #endregion
    }
}
