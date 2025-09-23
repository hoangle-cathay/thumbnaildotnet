using ThumbnailService.Services;
using Google.Cloud.Kms.V1;
using Google.Cloud.Storage.V1;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using ThumbnailService.Models;

var builder = WebApplication.CreateBuilder(args);

// 1️⃣ MVC & Razor Pages
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// 2️⃣ Kestrel port config (Cloud Run)
var portEnv = Environment.GetEnvironmentVariable("PORT") ?? "5001";
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

// 4️⃣ SecretFetcher & KMS
builder.Services.AddSingleton<SecretFetcher>();
builder.Services.AddSingleton(KeyManagementServiceClient.Create());
builder.Services.AddScoped<IEncryptionService, KmsEncryptionService>();

// 5️⃣ Logging
builder.Services.AddLogging(logging =>
{
    logging.ClearProviders();
    logging.AddConsole();
});

// 6️⃣ PostgreSQL (Cloud SQL) with SecretFetcher
builder.Services.AddDbContext<AppDbContext>((serviceProvider, options) =>
{
    var env = builder.Environment;
    string connectionStringTemplate = builder.Configuration.GetConnectionString("CloudSqlPostgres");
    string finalConnectionString;

    if (env.IsDevelopment())
    {
        finalConnectionString = connectionStringTemplate;
    }
    else
    {
        var fetcher = serviceProvider.GetRequiredService<SecretFetcher>();
        var dbPassword = fetcher.GetDecryptedPassword();
        finalConnectionString = connectionStringTemplate.Replace("{PLACEHOLDER}", dbPassword);
    }

    options.UseNpgsql(finalConnectionString);
});

// 7️⃣ Multipart upload limit
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 104857600; // 100 MB
});

// 8️⃣ Cookie Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(1);
        options.SlidingExpiration = true;
    });

// 9️⃣ Build app
var app = builder.Build();

// 🔟 Log app startup
var startupLogger = app.Services.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Program>>();
startupLogger.LogInformation("ThumbnailService app started. Environment: {Env}", app.Environment.EnvironmentName);

// 1️⃣1️⃣ Middleware
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// ✅ Authentication & Authorization must be here
app.UseAuthentication();
app.UseAuthorization();

// 1️⃣2️⃣ Routes
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
