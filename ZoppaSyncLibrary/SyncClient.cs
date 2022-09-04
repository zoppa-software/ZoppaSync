using Microsoft.VisualBasic;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using ZoppaSyncLibrary.Helper;
using ZoppaSyncLibrary.Models;
using ZstdNet;

namespace ZoppaSyncLibrary
{
    public static class SyncClient
    {
        public const int BUF_SIZE = 4096;


        public static async Task SyncRequest(string targetName, string sourcePath, string dirPath, CancellationTokenSource cancellation, ILogger logger)
        {
            var addr = await Dns.GetHostEntryAsync(targetName);

            try {
                // 同期用のポートを開く
                TcpListener listener = new TcpListener(IPAddress.Any, 0);
                listener.Start();
                var port = (ushort)((IPEndPoint)listener.LocalEndpoint).Port;

                // サーバーにパスとポートを通知
                using var client = new TcpClient(addr.HostName, SyncDefine.SERVICE_PORT);
                using (var stream = client.GetStream()) {
                    stream.WriteString(sourcePath);

                    stream.WriteUInt16(port);
                }

                // ダウンロードカタログを取得
                var fileInfos = new List<FileInformation>();
                TcpClient revclient = await listener.AcceptTcpClientAsync(cancellation.Token);
                using (var stream = revclient.GetStream()) {
                    foreach (var catalog in stream.ReadString().Split(SyncDefine.CATALOG_SPLIT_CHAR)) {
                        var prm = (catalog ?? "").Split(SyncDefine.CATALOG_PARAM_SPLIT_CHAR);
                        if (prm.Length > 1) {
                            fileInfos.Add(new FileInformation($"{dirPath}\\{prm[0]}", prm[1]));
                        }
                    }

                    // 存在しないファイルを取得
                    var regInfo = new ConcurrentBag<(FileInformation info, bool isRegisted)>();
                    var tasks = new List<Task>();
                    foreach (var info in fileInfos) {
                        var tk = Task.Factory.StartNew(() => {
                            if (File.Exists(info.FullName)) {
                                SHA256 crypto = SHA256.Create();
                                var bytes = File.ReadAllBytes(info.FullName);
                                var hash = BitConverter.ToString(crypto.ComputeHash(bytes));
                                regInfo.Add((info, info.SHA256 == hash));
                            }
                            else {
                                regInfo.Add((info, false));
                            }
                        });
                        tasks.Add(tk);
                    }
                    Task.WaitAll(tasks.ToArray());

                    // ファイルをダウンロードする
                    foreach (var downFile in regInfo.Where((v) => { return !v.isRegisted; })) {
                        var srcPath = $"{sourcePath}{downFile.info.FullName.Substring(dirPath.Length)}";
                        stream.WriteString(srcPath);
                        await stream.FlushAsync();

                        var numbuf = new byte[8];
                        stream.Read(numbuf, 0, numbuf.Length);

                        var len = BitConverter.ToInt64(numbuf, 0);

                        var ans = new List<byte>(BUF_SIZE);
                        var buf = new byte[BUF_SIZE < len ? BUF_SIZE : len];
                        while (len > 0) {
                            int l = stream.Read(buf, 0, buf.Length);
                            ans.AddRange(buf.Take(l));
                            len -= BUF_SIZE;
                        }

                        var dInfo = new FileInfo(downFile.info.FullName);
                        if (dInfo.Directory != null && !dInfo.Directory.Exists) {
                            Directory.CreateDirectory(dInfo.Directory.FullName);
                        }

                        using var decompressor = new Decompressor();
                        var decompressedData = (ans.Count > 0 ? decompressor.Unwrap(ans.ToArray()) : new byte[] { });

                        using (var fs = new FileStream(downFile.info.FullName, FileMode.Create, FileAccess.Write)) {
                            await fs.WriteAsync(decompressedData);
                        }
                    }

                    stream.WriteString("");
                    await stream.FlushAsync();
                }
            }
            catch (Exception ex) {
                throw;
            }
        }
    }
}
