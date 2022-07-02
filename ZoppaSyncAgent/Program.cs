using ZoppaSyncLibrary;

namespace ZoppaSyncAgent
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var cancellation = new CancellationTokenSource();
            SyncServer.ListenDownload(8080, cancellation).Wait();
        }
    }
}