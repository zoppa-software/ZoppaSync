using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZoppaSyncLibrary
{
    public static class SyncDefine
    {
        public const int SERVICE_PORT = 9304;

        public const char CATALOG_SPLIT_CHAR = '\n';

        public const char CATALOG_PARAM_SPLIT_CHAR = '|';

        public enum Command
        {
            Request = 1,
        }
    }
}
