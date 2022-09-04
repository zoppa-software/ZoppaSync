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
            catch (Exception ex) {
                logger.WriteError(ex);
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
            using var sqlHelper = await SqliteHelper.Create("zoppa_sync.db", logger);

            var fileInfos = CollectFileInformation(sqlHelper, usePath, usePort, logger);

            var catalog = new StringBuilder();
            foreach (var file in fileInfos) {
                catalog.Append($"{(file.IsFile ? ' ' : '*' )}{file.FullName.Substring(usePath.Length + 1)}{SyncDefine.CATALOG_PARAM_SPLIT_CHAR}{file.SHA256}{SyncDefine.CATALOG_SPLIT_CHAR}");
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
                        await stream.WriteFileAsync(path);
                    }
                    else {
                        await stream.WriteAsync(BitConverter.GetBytes((long)0));
                    }
                }
            }
        }

        private static List<StorageInformation> CollectFileInformation(SqliteHelper sqlHelper,
                                                                    string usePath,
                                                                    ushort usePort,
                                                                    ZoppaSyncLibrary.Helper.ILogger logger)
        {
            var fileInfos = new List<StorageInformation>();
            var dirInfo = new DirectoryInfo(usePath);
            if (dirInfo.Exists && usePort != 0) {
                var coninfos = new ConcurrentBag<StorageInformation>();

                foreach (var info in dirInfo.EnumerateDirectories("*", SearchOption.AllDirectories)) {
                    fileInfos.Add(new StorageInformation(info.FullName));
                }

                var tasks = new List<Task>();
                foreach (var info in dirInfo.EnumerateFiles("*", SearchOption.AllDirectories)) {
                    var tk = Task.Factory.StartNew(() => {
                        coninfos.Add(
                            new StorageInformation(info.FullName, info.LastWriteTime, info.Length)
                        );
                    });
                    tasks.Add(tk);
                }
                Task.WaitAll(tasks.ToArray());

                SHA256 crypto = SHA256.Create();
                fileInfos.AddRange(sqlHelper.GetFileInformation(
                    new List<StorageInformation>(coninfos), 
                    logger, 
                    (instream) => {
                        return crypto.ComputeHash(instream).ConvertToString();
                    }
                ));
            }
            return fileInfos;
        }
    }
}
