using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZoppaSyncLibrary.Models
{
    public sealed class StorageInformation
    {
        public StorageInformation(string fullName)
        {
            IsFile = false;
            FullName = fullName;
            LastWriteTime = new DateTime();
            Length = 0;
            SHA256 = string.Empty;
        }

        public StorageInformation(string fullName, DateTime lastWriteTime, long length)
        {
            IsFile = true;
            FullName = fullName;
            LastWriteTime = lastWriteTime;
            Length = length;
            SHA256 = string.Empty;
        }

        public StorageInformation(string fullName, string sha256)
        {
            IsFile= true;
            FullName = fullName;
            LastWriteTime = new DateTime();
            Length = 0;
            SHA256 = sha256;
        }

        public bool IsFile { get; }

        public bool IsDirectory { get { return !this.IsFile; } }

        public string FullName { get; }

        public DateTime LastWriteTime { get; }

        public long Length { get; }

        public string SHA256 { get; set; } = "";
    }
}
