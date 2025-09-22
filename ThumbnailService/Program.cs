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

// 4️⃣ PostgreSQL
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("CloudSqlPostgres")));

// 5️⃣ Multipart upload limit
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 104857600;
});


// 6️⃣ No AES key/IV: Use direct KMS encryption/decryption via SecretManagerKmsHelper
// Remove AesEncryptionService and related DI registration.

// 7️⃣ JWT Service
builder.Services.AddSingleton(KeyManagementServiceClient.Create());
builder.Services.AddScoped<IJwtService, KmsJwtService>();

// 8️⃣ Build app
var app = builder.Build();

// 9️⃣ Middleware
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseMiddleware<ThumbnailService.Middleware.JwtAuthMiddleware>();

// 10️⃣ Routes
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
