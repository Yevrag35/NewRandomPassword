using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MG.NewRandomPassword
{
    public interface ICryptoEntry
    {
        uint Seed { get; }
        char Character { get; }
    }
}
