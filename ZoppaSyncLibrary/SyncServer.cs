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
    public static class SyncServer
    {
        public static async Task ListenRequest(SqliteHelper sqlHelper, CancellationTokenSource cancellation, ILogger logger)
        {
            string usePath = "";
            ushort usePort = 0;
            IPAddress? useAddress = null;

            TcpListener baseListener = new TcpListener(IPAddress.Any, SyncDefine.SERVICE_PORT);
            baseListener.Start();

            try {
                TcpClient baseClient = await baseListener.AcceptTcpClientAsync(cancellation.Token);
                using (var stream = baseClient.GetStream()) {
                    usePath = stream.ReadString();
                    logger?.WriteInformation($"request path : {usePath}");

                    usePort = stream.ReadUind16();
                    logger?.WriteInformation($"return port : {usePort}");

                    useAddress = (baseClient.Client.RemoteEndPoint as IPEndPoint)?.Address;
                }
            }
            catch (Exception) {
                throw;
            }
            finally {
                baseListener.Stop();
            }

            var fileInfos = await Task.Factory.StartNew<List<FileInformation>>(
                () => {
                    var dirInfo = new DirectoryInfo(usePath);
                    if (dirInfo.Exists && usePort != 0) {
                        var fileInfos = new ConcurrentBag<FileInformation>();

                        var tasks = new List<Task>();
                        foreach (var info in dirInfo.EnumerateFiles("*", SearchOption.AllDirectories)) {
                            var tk = Task.Factory.StartNew(() => {
                                fileInfos.Add(
                                    new FileInformation(info.FullName, info.LastWriteTime, info.Length)
                                );
                            });
                            tasks.Add(tk);
                        }
                        Task.WaitAll(tasks.ToArray());

                        SHA256 crypto = SHA256.Create();
                        return sqlHelper.GetFileInformation(
                            new List<FileInformation>(fileInfos), 
                            logger, 
                            (inbytes) => {
                                return crypto.ComputeHash(inbytes).ConvertToString();
                            }
                        );
                    }
                    else {
                        return new List<FileInformation>();
                    }
                }, 
                TaskCreationOptions.LongRunning
            );

            var catalog = new StringBuilder();
            foreach (var file in fileInfos) {
                catalog.Append($"{file.FullName.Substring(usePath.Length + 1)}{SyncDefine.CATALOG_PARAM_SPLIT_CHAR}{file.SHA256}{SyncDefine.CATALOG_SPLIT_CHAR}");
            }

            // ダウンロードカタログを返す
            using var client = new TcpClient(useAddress?.ToString() ?? "127.0.0.1", usePort);
            using (var stream = client.GetStream()) {
                stream.WriteString(catalog.ToString());
                await stream.FlushAsync();

                while (true) {
                    var path = stream.ReadString() ?? "";
                    if (path == "") {
                        break;
                    }

                    if (File.Exists(path)) {
                        using var compressor = new Compressor();
                        using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read)) {
                            var bytes = new byte[fs.Length];
                            fs.Read(bytes, 0, bytes.Length);

                            var compressedData = compressor.Wrap(bytes);
                            await stream.WriteAsync(BitConverter.GetBytes((long)compressedData.Length));
                            await stream.WriteAsync(compressedData);
                        }
                    }
                    else {
                        int a = 50;
                    }
                }
            }
        }
    }
}
