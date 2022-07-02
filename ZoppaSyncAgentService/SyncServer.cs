using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using ZoppaSyncLibrary;
using ZoppaSyncLibrary.Helper;
using ZoppaSyncLibrary.Models;
using ZstdNet;

namespace ZoppaSyncAgentService
{
    public static class SyncServer
    {
        internal static async Task ListenRequest(CancellationTokenSource cancellation, 
                                                 ZoppaSyncLibrary.Helper.ILogger logger)
        {
            TcpListener baseListener = new TcpListener(IPAddress.Any, SyncDefine.SERVICE_PORT);
            baseListener.Start();

            try {
                TcpClient client = await baseListener.AcceptTcpClientAsync(cancellation.Token);
                using (var stream = client.GetStream()) {
                    var command = (SyncDefine.Command)stream.ReadByte();
                    switch(command) {
                        case SyncDefine.Command.Request:
                            // 同期リクエストコマンド処理
                            {
                                var useAddress = (client.Client.RemoteEndPoint as IPEndPoint)?.Address;
                                logger.WriteInformation($"sync client address : {useAddress}");

                                var usePath = stream.ReadString();
                                logger.WriteInformation($"sync path : {usePath}");

                                var usePort = stream.ReadUind16();
                                logger.WriteInformation($"sync connect port : {usePort}");

                                var rans = Task.Factory.StartNew(
                                    () => {
                                        RunSync(useAddress, usePath, usePort, logger).Wait();
                                    },
                                    TaskCreationOptions.LongRunning
                                );
                            }
                            break;
                    }
                }
            }
            catch (Exception) {
                throw;
            }
            finally {
                baseListener.Stop();
            }
        }

        private static async Task RunSync(IPAddress? useAddress, 
                                          string usePath, 
                                          ushort usePort, 
                                          ZoppaSyncLibrary.Helper.ILogger logger)
        {
            var sqlHelper = await SqliteHelper.Create("zoppa_sync.db", logger);

            var fileInfos = CollectFileInformation(sqlHelper, usePath, usePort, logger);

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

        private static List<FileInformation> CollectFileInformation(SqliteHelper sqlHelper,
                                                                    string usePath,
                                                                    ushort usePort,
                                                                    ZoppaSyncLibrary.Helper.ILogger logger)
        {
            var fileInfos = new List<FileInformation>();
            var dirInfo = new DirectoryInfo(usePath);
            if (dirInfo.Exists && usePort != 0) {
                var coninfos = new ConcurrentBag<FileInformation>();

                var tasks = new List<Task>();
                foreach (var info in dirInfo.EnumerateFiles("*", SearchOption.AllDirectories)) {
                    var tk = Task.Factory.StartNew(() => {
                        coninfos.Add(
                            new FileInformation(info.FullName, info.LastWriteTime, info.Length)
                        );
                    });
                    tasks.Add(tk);
                }
                Task.WaitAll(tasks.ToArray());

                SHA256 crypto = SHA256.Create();
                fileInfos = sqlHelper.GetFileInformation(
                    new List<FileInformation>(coninfos), 
                    logger, 
                    (inbytes) => {
                        return crypto.ComputeHash(inbytes).ConvertToString();
                    }
                );
            }
            return fileInfos;
        }
    }
}
