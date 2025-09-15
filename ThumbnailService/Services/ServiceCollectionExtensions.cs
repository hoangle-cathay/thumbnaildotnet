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
            return services;
        }
    }
}


