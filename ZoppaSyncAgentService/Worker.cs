using ZoppaSyncLibrary;
using ZoppaSyncLibrary.Helper;

namespace ZoppaSyncAgentService
{
    public class Worker : BackgroundService
    {
        private readonly ServiceLogger _logger;

        private readonly CancellationTokenSource _cancellation;

        //private SqliteHelper? _sqliteHelper;

        public Worker(ILogger<Worker> logger)
        {
            _logger = new ServiceLogger(logger);
            this._cancellation = new CancellationTokenSource();
        }
        public override Task StartAsync(CancellationToken cancellationToken)
        {
            //var task = SqliteHelper.Create("zoppa_sync.db", this._logger);
            //task.Wait();
            //this._sqliteHelper = task.Result;
            return base.StartAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested) {
                try {
                    await SyncServer.ListenRequest(this._cancellation, this._logger);
                }
                catch (OperationCanceledException) {
                    _logger.WriteInformation("agent stop");
                    break;
                }
                catch (Exception ex) {
                    _logger.WriteError(ex);
                }
            }
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            this._cancellation?.Cancel();
            return base.StopAsync(cancellationToken);
        }

        private sealed class ServiceLogger : ZoppaSyncLibrary.Helper.ILogger
        {
            private readonly ILogger<Worker> _logger;

            public ServiceLogger(ILogger<Worker> logger)
            {
                this._logger = logger;
            }

            public void WriteInformation(string message)
            {
                this._logger.LogInformation(message);
            }

            public void WriteError(Exception ex)
            {
                Exception? pex = ex;
                while (pex != null) {
                    this._logger.LogInformation($"error! : {pex.Message}");
                    this._logger.LogInformation(pex.StackTrace);
                    pex = pex.InnerException;
                }
            }
        }
    }
}