using ThumbnailService.Services;
using Google.Cloud.Kms.V1;
using Google.Cloud.Storage.V1;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using ThumbnailService.Models;
using ThumbnailService.Services;

var builder = WebApplication.CreateBuilder(args);

// 1️⃣ MVC & Razor Pages
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// 2️⃣ Kestrel port config (Cloud Run)
var portEnv = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(portEnv) && int.TryParse(portEnv, out var port))
{
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.ListenAnyIP(port);
        options.Limits.MaxRequestBodySize = 104857600; // 100 MB
    });
}

// 3️⃣ Storage & Thumbnail services
builder.Services.AddSingleton(StorageClient.Create());
builder.Services.AddSingleton<IStorageService, GoogleStorageService>();
builder.Services.AddSingleton<IThumbnailService, ThumbnailServiceImpl>();

// 4️⃣ PostgreSQL (Cloud SQL)
// Detect môi trường
var isDevelopment = builder.Environment.IsDevelopment();

string connectionString;

if (isDevelopment)
{
    // Dùng config local
    connectionString = builder.Configuration.GetConnectionString("CloudSqlPostgres");
}
else
{
    // Prod: fetch secret bằng SecretFetcher
    using (var scope = builder.Services.BuildServiceProvider().CreateScope())
    {
        var logger = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ThumbnailService.Services.SecretFetcher>>();
        var fetcher = new ThumbnailService.Services.SecretFetcher(logger);
        var dbPassword = fetcher.GetDecryptedPassword();
        connectionString = builder.Configuration.GetConnectionString("CloudSqlPostgres")
            .Replace("{PLACEHOLDER}", dbPassword);
    }
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// 5️⃣ Multipart upload limit
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 104857600;
});

// 6️⃣ KMS Encryption Service
builder.Services.AddSingleton(KeyManagementServiceClient.Create());
builder.Services.AddScoped<IEncryptionService, KmsEncryptionService>();

// 7️⃣ JWT Service (signed/verified via KMS)

// 8️⃣ Logging
builder.Services.AddLogging(logging =>
{
    logging.ClearProviders();
    logging.AddConsole();
});

// 9️⃣ Build app
var app = builder.Build();

// 🔟 Middleware
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// 1️⃣1️⃣ Routes
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}"
);
app.MapGet("/", context =>
{
    context.Response.Redirect("/Home/Index");
    return Task.CompletedTask;
});

app.Run();
