using GIMcompress;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GIMtransfer
{
    internal abstract class JsonReader
    {
        public abstract JsonGimObject ReadJson(string jsonText);
    }
}
