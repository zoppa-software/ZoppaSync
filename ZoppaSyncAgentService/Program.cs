namespace ZoppaSyncAgentService
{
    public class Program
    {
        public static void Main(string[] args)
        {
            IHost host = Host.CreateDefaultBuilder(args)
                .ConfigureServices(services => {
                    services.AddHostedService<Worker>();
                })
                .UseWindowsService()
                .Build();

            host.Run();
        }
    }
}