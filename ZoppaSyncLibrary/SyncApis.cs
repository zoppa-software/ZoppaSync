using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZstdNet;

namespace ZoppaSyncLibrary
{
    public static  class SyncApis
    {
        public const int BUF_SIZE = 4096;

        public static ushort ReadUind16(this Stream stream)
        {
            var numbuf = new byte[2];
            stream.Read(numbuf, 0, numbuf.Length);

            return BitConverter.ToUInt16(numbuf, 0);
        }

        public static void WriteUInt16(this Stream stream, ushort value)
        {
            stream.Write(BitConverter.GetBytes(value));
            stream.Flush();
        }

        public static string ReadString(this Stream stream)
        {
            var numbuf = new byte[2];
            stream.Read(numbuf, 0, numbuf.Length);

            int len = BitConverter.ToUInt16(numbuf, 0);

            var ans = new List<byte>(BUF_SIZE);
            var buf = new byte[BUF_SIZE < len ? BUF_SIZE : len];
            while (len > 0) {
                int l = stream.Read(buf, 0, buf.Length);
                ans.AddRange(buf.Take(l));
                len -= BUF_SIZE;
            }

            if (ans.Count > 0) {
                using var decompressor = new Decompressor();
                var decompressedData = decompressor.Unwrap(ans.ToArray());
                return Encoding.UTF8.GetString(decompressedData, 0, decompressedData.Length);
            }
            else {
                return "";
            }
        }

        public static void WriteString(this Stream stream, string str)
        {
            var chs = Encoding.UTF8.GetBytes(str);

            using var compressor = new Compressor();
            var compressedData = compressor.Wrap(chs);

            stream.Write(BitConverter.GetBytes((ushort)compressedData.Length));
            stream.Write(compressedData);
            stream.Flush();
        }

        public static byte[] ReadBytes(this Stream stream)
        {
            var numbuf = new byte[8];
            stream.Read(numbuf, 0, numbuf.Length);

            var len = BitConverter.ToInt64(numbuf, 0);

            var ans = new List<byte>(SyncApis.BUF_SIZE);
            var buf = new byte[SyncApis.BUF_SIZE < len ? SyncApis.BUF_SIZE : len];
            while (len > 0) {
                int l = stream.Read(buf, 0, buf.Length);
                ans.AddRange(buf.Take(l));
                len -= SyncApis.BUF_SIZE;
            }

            using var decompressor = new Decompressor();
            return (ans.Count > 0 ? decompressor.Unwrap(ans.ToArray()) : new byte[] { });
        }

        public static string ConvertToString(this byte[] inbytes)
        {
            var buf = new StringBuilder(64);
            foreach (var v in inbytes) {
                buf.Append(string.Format("{0:X2}", v));
            }
            return buf.ToString();
        }
    }
}
