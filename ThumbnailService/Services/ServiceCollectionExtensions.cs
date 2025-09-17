using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ThumbnailService.Services
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddGoogleCloudServices(this IServiceCollection services, IConfiguration configuration)
        {
            // Storage client via ADC or a service account json if configured via env var.
            services.AddSingleton<StorageClient>(_ => GoogleStorageService.CreateClientFromEnvironment());
            services.AddSingleton<IStorageService, GoogleStorageService>();
            // Register PubSubService with topic from config
            var topic = configuration.GetValue<string>("Gcp:ThumbnailJobTopic") ?? string.Empty;
            services.AddSingleton(new PubSubService(topic));
            return services;
        }
    }
}


