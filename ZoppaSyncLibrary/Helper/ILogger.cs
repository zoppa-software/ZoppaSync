using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZoppaSyncLibrary.Helper
{
    public interface ILogger
    {
        void WriteInformation(string message);

        void WriteError(Exception ex);
    }
}
