using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZoppaSyncLibrary.Models
{
    public sealed class FileInformation
    {
        public FileInformation(string fullName, DateTime lastWriteTime, long length)
        {
            FullName = fullName;
            LastWriteTime = lastWriteTime;
            Length = length;
        }

        public FileInformation(string fullName, string sha256)
        {
            FullName = fullName;
            SHA256 = sha256;
        }

        public string FullName { get; }

        public DateTime LastWriteTime { get; }

        public long Length { get; }

        public string SHA256 { get; set; } = "";
    }
}
