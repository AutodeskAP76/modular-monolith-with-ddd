using Autofac.Extensions.DependencyInjection;

namespace CompanyName.MyMeetings.API
{
    // Application entry point. Builds the ASP.NET Core host and swaps the built-in DI container for Autofac
    // (UseServiceProviderFactory). With that factory plugged in, the runtime also calls Startup.ConfigureContainer,
    // where the Autofac modules are registered. UseStartup<Startup> then hands control to Startup, which runs
    // its constructor, ConfigureServices, ConfigureContainer and Configure in that order.
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateWebHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateWebHostBuilder(string[] args)
        {
            return Host.CreateDefaultBuilder(args)
                .UseServiceProviderFactory(new AutofacServiceProviderFactory())
                .ConfigureWebHostDefaults(
                    webBuilder => { webBuilder.UseStartup<Startup>(); });
        }
    }
}