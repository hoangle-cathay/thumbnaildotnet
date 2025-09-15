using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace ThumbnailService.Models
{
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var basePath = Directory.GetCurrentDirectory();
            var config = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            var cs = config.GetConnectionString("CloudSqlPostgres")
                     ?? System.Environment.GetEnvironmentVariable("ConnectionStrings__CloudSqlPostgres")
                     ?? "Host=localhost;Port=5432;Database=thumbnaildb;Username=appuser;Password=dev;Ssl Mode=Disable";

            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            optionsBuilder.UseNpgsql(cs);
            return new AppDbContext(optionsBuilder.Options);
        }
    }
}


