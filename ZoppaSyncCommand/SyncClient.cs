﻿using System;
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

namespace ZoppaSyncCommand
{
    internal static class SyncClient
    {
        public static async Task SyncRequest(string targetName, 
                                             string sourcePath, 
                                             string dirPath, 
                                             CancellationTokenSource cancellation, 
                                             ILogger logger)
        {
            var addr = await Dns.GetHostEntryAsync(targetName);

            TcpListener? listener = null;
            try {
                // 同期用のポートを開く
                listener = new TcpListener(IPAddress.Any, 0);
                listener.Start();
                var port = (ushort)((IPEndPoint)listener.LocalEndpoint).Port;

                // サーバーにパスとポートを通知
                logger.WriteInformation($"request sync path:{sourcePath} port:{port}");
                using var client = new TcpClient(addr.HostName, SyncDefine.SERVICE_PORT);
                using (var stream = client.GetStream()) {
                    stream.WriteByte((byte)SyncDefine.Command.Request);
                    stream.WriteString(sourcePath);
                    stream.WriteUInt16(port);
                }

                TcpClient revclient = await listener.AcceptTcpClientAsync(cancellation.Token);
                using (var stream = revclient.GetStream()) {
                    // ダウンロードカタログを取得
                    var fileInfos = CollectCatalog(stream.ReadString());
                    logger.WriteInformation($"sync file count : {fileInfos.Count}");

                    // 存在しないファイルを取得
                    var regInfo = new ConcurrentBag<(StorageInformation info, bool isRegisted)>();
                    var tasks = new List<Task>();
                    foreach (var info in (fileInfos.Where((v) => { return v.IsFile; }))) {
                        var tk = Task.Factory.StartNew(() => {
                            if (File.Exists($"{dirPath}\\{info.FullName}")) {
                                SHA256 crypto = SHA256.Create();
                                //var bytes = File.ReadAllBytes($"{dirPath}\\{info.FullName}");

                                using var fs = new FileStream($"{dirPath}\\{info.FullName}", FileMode.Open, FileAccess.Read, FileShare.Read);
                                var hash = crypto.ComputeHash(fs).ConvertToString();
                                regInfo.Add((info, info.SHA256 == hash));
                            }
                            else {
                                regInfo.Add((info, false));
                            }
                        });
                        tasks.Add(tk);
                    }
                    Task.WaitAll(tasks.ToArray());

                    // ディレクトリを作成する
                    var downDir = fileInfos.Where((v) => { return !v.IsFile; }).ToList();
                    foreach (var downFile in downDir) {
                        var dInfo = new DirectoryInfo($"{dirPath}\\{downFile.FullName}");
                        if (!dInfo.Exists) {
                            Directory.CreateDirectory(dInfo.FullName);
                        }
                    }

                    // ファイルをダウンロードする
                    double cnt = 1;
                    var downFiles = regInfo.Where((v) => { return v.info.IsFile && !v.isRegisted; }).ToList();
                    foreach (var downFile in downFiles) {
                        logger.WriteInformation($"copy {Math.Round((cnt / downFiles.Count) * 100, 1)}% {downFile.info.FullName}");

                        var dInfo = new FileInfo($"{dirPath}\\{downFile.info.FullName}");
                        if (dInfo.Directory != null && !dInfo.Directory.Exists) {
                            Directory.CreateDirectory(dInfo.Directory.FullName);
                        }

                        var srcPath = $"{sourcePath}\\{downFile.info.FullName}";
                        stream.WriteString(srcPath);
                        await stream.FlushAsync();

                        //var decompressedData = stream.ReadBytes();
                        //using (var fs = new FileStream(dInfo.FullName, FileMode.Create, FileAccess.Write)) {
                        //    await fs.WriteAsync(decompressedData);
                        //}
                        await stream.ReadFileAsync(dInfo.FullName);
                        cnt += 1;
                    }

                    logger.WriteInformation($"copy finish");

                    stream.WriteString("");
                    await stream.FlushAsync();
                }
            }
            catch (Exception ex) {
                logger.WriteError(ex);
            }
            finally {
                listener?.Stop();
            }
        }

        private static List<StorageInformation> CollectCatalog(string catalogStr)
        {
            var fileInfos = new List<StorageInformation>();
            foreach (var catalog in catalogStr.Split(SyncDefine.CATALOG_SPLIT_CHAR)) {
                var prm = (catalog ?? "").Split(SyncDefine.CATALOG_PARAM_SPLIT_CHAR);
                if (prm.Length > 1) {
                    if (prm[0][0] == '*') {
                        fileInfos.Add(new StorageInformation(prm[0].Substring(1)));
                    }
                    else {
                        fileInfos.Add(new StorageInformation(prm[0].Substring(1), prm[1]));
                    }
                }
            }
            return fileInfos;
        }
    }
}
