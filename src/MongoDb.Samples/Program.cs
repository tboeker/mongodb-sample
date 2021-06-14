using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MongoDb.Samples
{
    class Program
    {
        static Task Main(string[] args)
        {
            var host = Host.CreateDefaultBuilder(args)
                    .ConfigureAppConfiguration(builder =>
                    {
                        builder.AddInMemoryCollection(new[]
                        {
                            new KeyValuePair<string, string>("MongoDb__DatabaseId", "tb1"),
                        });
                    })
                    .ConfigureLogging(c =>
                    {
                        c.ClearProviders();
                        c.AddConsole();
                        c.SetMinimumLevel(LogLevel.Debug);
                    })
                    .ConfigureServices((context, services) =>
                    {
                        //       services.AddOpen();

                        services.ConfigureMongoDb(context.Configuration);

                        services.PostConfigure<MongoDbOptions>(options =>
                        {
                            options.DebugLog = true;
                            options.ApplicationName = "myapp";
                            options.DatabaseId = DateTime.Now.Ticks.ToString();
                        });

                        services
                            .AddHostedService<Runner>()
                            ;
                    })
                ;

            return host.Build().RunAsync();
        }
    }
}