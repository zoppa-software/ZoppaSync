using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using ZoppaSyncLibrary.Models;

namespace ZoppaSyncLibrary.Helper
{
    public sealed class SqliteHelper : IDisposable
    {
        private SqliteConnection? _connection;

        public static async Task<SqliteHelper> Create(string dbName, ILogger logger)
        {
            return await Task.Run(() => {
                var newdb = false;

                var dbFolder = $"{System.Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}\\ZoppaSync";
                if (!Directory.Exists(dbFolder)) {
                    Directory.CreateDirectory(dbFolder);
                }

                var dbPath = $"{dbFolder}\\{dbName}";
                if (!File.Exists(dbPath)) {
                    newdb = true;
                }

                var connection = new SqliteConnection($"Data Source={dbPath};");
                if (newdb) {
                    InitializeTable(connection, logger);
                }

                return new SqliteHelper(connection);
            });
        }

        private static void InitializeTable(SqliteConnection connection, ILogger logger)
        {
            try {
                connection.Open();

                using (var command = connection.CreateCommand()) {
                    command.CommandText = @"
CREATE TABLE ""Targets"" (
    ""Path"" TEXT,
	""UpdateDateTime"" TEXT NOT NULL,
	""Size"" INTEGER NOT NULL,
	""SHA256"" INTEGER NOT NULL,
	PRIMARY KEY(""Path"")
); ";
                    command.ExecuteNonQuery();
                }
            }
            catch (Exception ex) {
                logger.WriteError(ex);
            }
            finally {
                connection.Close();
            }
        }

        private SqliteHelper(SqliteConnection connection)
        {
            this._connection = connection;
        }

        public void Dispose()
        {
            if (this._connection != null) {
                this._connection.Dispose();
                this._connection = null;
            }
        }

        public List<FileInformation> GetFileInformation(List<FileInformation> infos, ILogger logger, Func<byte[], string> calcHash)
        {
            var ans = new List<(FileInformation info, bool isRegisted)>();

            if (this._connection != null) {
                //SHA256 crypto = SHA256.Create();
                this._connection.Open();

                SqliteTransaction? transaction = null;
                try {
                    using (var command = this._connection.CreateCommand()) {
                        command.CommandText = "select * from Targets where Path=@path and UpdateDateTime=@upDateTime and Size=@sz";

                        command.Parameters.Add(new SqliteParameter("@path", ""));
                        command.Parameters.Add(new SqliteParameter("@upDateTime", ""));
                        command.Parameters.Add(new SqliteParameter("@sz", (long)0));

                        foreach (var info in infos) {
                            command.Parameters["@path"].Value = info.FullName;
                            command.Parameters["@upDateTime"].Value = info.LastWriteTime.ToString("yyyyMMddHHmmssfff");
                            command.Parameters["@sz"].Value = info.Length;

                            using (var result = command.ExecuteReader()) {
                                if (result.Read()) {
                                    info.SHA256 = result["SHA256"].ToString() ?? "";
                                    ans.Add((info, true));
                                }
                                else {
                                    //var hash = BitConverter.ToString(crypto.ComputeHash(File.ReadAllBytes(info.FullName)));
                                    info.SHA256 = calcHash(File.ReadAllBytes(info.FullName));
                                    ans.Add((info, false));
                                }
                            }
                        }
                    }

                    using (var command = this._connection.CreateCommand()) {
                        command.CommandText = @"
insert into Targets (Path, UpdateDateTime, Size, SHA256) values (@path, @upDateTime, @sz, @hash)
on conflict(Path) do
update set UpdateDateTime = @upDateTime, Size = @sz, SHA256 = @hash";

                        command.Parameters.Add(new SqliteParameter("@path", ""));
                        command.Parameters.Add(new SqliteParameter("@upDateTime", ""));
                        command.Parameters.Add(new SqliteParameter("@sz", (long)0));
                        command.Parameters.Add(new SqliteParameter("@hash", ""));

                        foreach (var aw in ans.Where((v) => !v.isRegisted)) {
                            command.Parameters["@path"].Value = aw.info.FullName;
                            command.Parameters["@upDateTime"].Value = aw.info.LastWriteTime.ToString("yyyyMMddHHmmssfff");
                            command.Parameters["@sz"].Value = aw.info.Length;
                            command.Parameters["@hash"].Value = aw.info.SHA256;
                            command.ExecuteNonQuery();
                        }
                    }

                    transaction?.Commit();
                }
                catch (Exception ex) {
                    if (transaction != null) {
                        transaction.Rollback();
                    }
                    logger.WriteError(ex);
                }
                finally {
                    this._connection?.Close();
                }
            }

            return ans.Select((v) => { return v.info; }).ToList();
        }
    }
}
