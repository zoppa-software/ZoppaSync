using ZoppaSyncLibrary;
using ZoppaSyncLibrary.Helper;

namespace ZoppaSyncCommand
{
    internal class Program
    {
        private static CancellationTokenSource? _cancellation;

        static void Main(string[] args)
        {
            var logger = new ClientLogger();

            Console.CancelKeyPress += Console_CancelKeyPress;
            _cancellation = new CancellationTokenSource();
            
            SyncClient.SyncRequest(
                "localhost",
                @"D:\GitRepos\ZoppaSync",
                @"D:\GitRepos\ZoppaSync_copy",
                _cancellation, 
                logger
            ).Wait();
        }

        private static void Console_CancelKeyPress(object? sender, ConsoleCancelEventArgs e)
        {
            _cancellation?.Cancel();
        }

        private sealed class ClientLogger : ZoppaSyncLibrary.Helper.ILogger
        {
            public void WriteInformation(string message)
            {
                Console.Out.WriteLine(message);
            }

            public void WriteError(Exception ex)
            {
                Exception? pex = ex;
                while (pex != null) {
                    Console.Error.WriteLine($"error! : {pex.Message}");
                    Console.Error.WriteLine(pex.StackTrace);
                    pex = pex.InnerException;
                }
            }
        }
    }
}