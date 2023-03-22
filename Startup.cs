using GraphNotifications.Services;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using GraphNotifications.Models;
using Microsoft.Extensions.Logging;

[assembly: FunctionsStartup(typeof(GraphNotifications.Startup))]

namespace GraphNotifications
{
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            // set min threads to reduce chance for Redis timeoutes
            // https://docs.microsoft.com/en-us/azure/azure-cache-for-redis/cache-troubleshoot-client#traffic-burst
            ThreadPool.SetMinThreads(200, 200);
            
            builder.Services.AddOptions<AppSettings>()
                .Configure<IConfiguration>((settings, configuration) =>
                {
                    configuration.GetSection("AppSettings").Bind(settings);
                });

            builder.Services.AddApplicationInsightsTelemetry();

            builder.Services.AddSingleton<ITokenValidationService, TokenValidationService>();
            builder.Services.AddSingleton<IGraphClientService, GraphClientService>();
            builder.Services.AddSingleton<IGraphNotificationService, GraphNotificationService>();

            builder.Services.AddSingleton<ICertificateService, CertificateService>();

            builder.Services.AddSingleton<IRedisFactory, RedisFactory>();
            builder.Services.AddSingleton<ICacheService>(x =>
                new CacheService(
                    x.GetRequiredService<IRedisFactory>(),
                    x.GetRequiredService<ILogger<CacheService>>()));
        }
    }
}