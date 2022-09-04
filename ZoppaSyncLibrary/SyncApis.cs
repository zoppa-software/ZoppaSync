using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using ZstdNet;

namespace ZoppaSyncLibrary
{
    public static  class SyncApis
    {
        public static readonly byte[] AES_KEY = { 
            73, 141, 132, 50, 137, 250, 171, 31, 
            36, 190,  41, 32, 105, 227,  15, 12,
            73, 141, 132, 50, 137, 250, 171, 31,
            36, 190,  41, 32, 105, 227,  15, 12
        };

        public static readonly byte[] AES_IV = {
            150, 221, 247, 40, 230,  24, 126, 31,
             61,  99, 101, 24,   8, 190, 130, 64,
        };

        public const int BUF_SIZE = 1 * 1024 * 1024;

        public static ushort ReadUind16(this Stream stream)
        {
            var numbuf = new byte[2];
            stream.Read(numbuf, 0, numbuf.Length);

            return BitConverter.ToUInt16(numbuf, 0);
        }
        private static void CopyToStream(this Stream instream, Stream outstream, long len)
        {
            var buf = new byte[BUF_SIZE < len ? BUF_SIZE : len];
            while (len > 0) {
                var l = instream.Read(buf, 0, (int)(buf.Length < len ? buf.Length : len));
                outstream.Write(buf, 0, l);
                len -= l;
            }
        }

        private static void CopyToStream(this Stream instream, Stream outstream)
        {
            var buf = new byte[BUF_SIZE];
            while (true) {
                int l = instream.Read(buf, 0, buf.Length);
                if (l > 0) {
                    outstream.Write(buf, 0, l);
                }
                else {
                    break;
                }
            }
        }

        private static Aes GetAes()
        {
            var aes = Aes.Create();
            aes.BlockSize = 128;
            aes.KeySize = 128;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            return aes;
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
            if (len > 0) {
                using var aes = GetAes();
                using var inmem = new MemoryStream();
                stream.CopyToStream(inmem, len);
                inmem.Position = 0;
                using (var crypto = new CryptoStream(inmem, aes.CreateDecryptor(AES_KEY, AES_IV), CryptoStreamMode.Read)) {
                    using var outmem = new MemoryStream();
                    using var decompressionStream = new DecompressionStream(crypto);
                    decompressionStream.CopyTo(outmem);
                    return Encoding.UTF8.GetString(outmem.ToArray(), 0, (int)outmem.Length);
                }
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

            using var aes = GetAes();
            using var mem = new MemoryStream();
            using (var crypto = new CryptoStream(mem, aes.CreateEncryptor(AES_KEY, AES_IV), CryptoStreamMode.Write)) {
                crypto.Write(compressedData);
                crypto.FlushFinalBlock();

                stream.Write(BitConverter.GetBytes((ushort)mem.Length));
                stream.Write(mem.ToArray());
            }
            stream.Flush();
        }

        public static async Task WriteFileAsync(this Stream stream, string path)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read)) {
                using var aes = GetAes();
                using Stream outmem =
                    fs.Length < 512 * 1024 * 1024 ?
                    new MemoryStream() :
                    new FileStream($"{Path.GetTempPath()}\\tmp.dat", FileMode.OpenOrCreate, FileAccess.ReadWrite);
                using (var crypto = new CryptoStream(outmem, aes.CreateEncryptor(AES_KEY, AES_IV), CryptoStreamMode.Write)) {
                    await using var compressionStream = new CompressionStream(crypto);
                    fs.CopyToStream(compressionStream);
                    compressionStream.Flush();
                    crypto.FlushFinalBlock();

                    await stream.WriteAsync(BitConverter.GetBytes((long)outmem.Length));
                    outmem.Position = 0;
                    outmem.CopyToStream(stream);
                }
                stream.Flush();
            }
        }

        /*
        public static byte[] ReadBytes(this Stream stream)
        {
            var numbuf = new byte[8];
            stream.Read(numbuf, 0, numbuf.Length);

            var len = BitConverter.ToInt64(numbuf, 0);
            if (len > 0) {
                using var aes = GetAes();
                using var inmem = new MemoryStream();
                stream.CopyToStream(inmem, len);
                inmem.Position = 0;
                using (var crypto = new CryptoStream(inmem, aes.CreateDecryptor(AES_KEY, AES_IV), CryptoStreamMode.Read)) {
                    using var outmem = new MemoryStream();
                    using var decompressionStream = new DecompressionStream(crypto);
                    decompressionStream.CopyTo(outmem);
                    return outmem.ToArray();
                }
            }
            else {
                return new byte[] { };
            }
        }
        */

        public static async Task ReadFileAsync(this Stream stream, string path)
        {
            var numbuf = new byte[8];
            stream.Read(numbuf, 0, numbuf.Length);

            var len = BitConverter.ToInt64(numbuf, 0);

            //using Stream inmem = new MemoryStream();
            using (var fs = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write)) {
                if (len > 0) {
                    using var aes = GetAes();
                    using Stream inmem =
                        len < 512 * 1024 * 1024 ?
                        new MemoryStream() :
                        new FileStream($"{Path.GetTempPath()}\\tmp2.dat", FileMode.OpenOrCreate, FileAccess.ReadWrite);
                    stream.CopyToStream(inmem, len);

                    inmem.Position = 0;
                    using (var crypto = new CryptoStream(inmem, aes.CreateDecryptor(AES_KEY, AES_IV), CryptoStreamMode.Read)) {
                        using var decompressionStream = new DecompressionStream(crypto);
                        await decompressionStream.CopyToAsync(fs);
                    }
                }

                /*
                using var aes = GetAes();
                using Stream outmem =
                    fs.Length < 512 * 1024 * 1024 ?
                    new MemoryStream() :
                    new FileStream($"{Path.GetTempPath()}\\tmp.dat", FileMode.OpenOrCreate, FileAccess.ReadWrite);
                using (var crypto = new CryptoStream(outmem, aes.CreateEncryptor(AES_KEY, AES_IV), CryptoStreamMode.Write)) {
                    await using var compressionStream = new CompressionStream(crypto);
                    fs.CopyToStream(compressionStream);
                    compressionStream.Flush();
                    crypto.FlushFinalBlock();

                    await stream.WriteAsync(BitConverter.GetBytes((long)outmem.Length));
                    outmem.Position = 0;
                    outmem.CopyToStream(stream);
                }
                stream.Flush();
                */
            }
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
