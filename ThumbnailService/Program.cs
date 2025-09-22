using Google.Cloud.Storage.V1;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using ThumbnailService.Models;
using ThumbnailService.Services;

var builder = WebApplication.CreateBuilder(args);

// -------------------------
// 1️⃣ MVC
// -------------------------
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// -------------------------
// 2️⃣ Kestrel listen on PORT (Cloud Run)
// -------------------------
var portEnv = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(portEnv) && int.TryParse(portEnv, out var port))
{
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.ListenAnyIP(port);
        options.Limits.MaxRequestBodySize = 104857600; // 100 MB
    });
}

// -------------------------
// 3️⃣ Google Cloud Services
// -------------------------
builder.Services.AddSingleton(provider => StorageClient.Create());
builder.Services.AddSingleton<IStorageService, GoogleStorageService>();
builder.Services.AddSingleton<IThumbnailService, ThumbnailServiceImpl>();

var pubsubTopic = builder.Configuration.GetValue<string>("Gcp:ThumbnailJobTopic") ?? string.Empty;
builder.Services.AddSingleton(new PubSubService(pubsubTopic));

// -------------------------
// 4️⃣ PostgreSQL + SecretFetcher + KMS
// -------------------------
var secretFetcher = new SecretFetcher();
string dbPassword = await secretFetcher.GetDecryptedPasswordAsync();

var connStr = $"Host=/cloudsql/hoangassignment:asia-southeast1:leo-cloudsql-postgre;" +
              $"Database=thumbnaildb;Username=appuser;Password={dbPassword}";

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connStr));

// -------------------------
// 5️⃣ Multipart upload limit
// -------------------------
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 104857600; // 100 MB
});

// -------------------------
// 6️⃣ KMS Encryption service
// -------------------------
builder.Services.AddSingleton<IEncryptionService>(sp =>
    new KmsEncryptionService(builder.Configuration["Jwt:KmsKeyName"]));

// -------------------------
// 7️⃣ Build app
// -------------------------
var app = builder.Build();

// -------------------------
// 8️⃣ Middleware
// -------------------------
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// JWT Auth Middleware
app.UseMiddleware<ThumbnailService.Middleware.JwtAuthMiddleware>();

// -------------------------
// 9️⃣ Routes
// -------------------------
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}"
);

app.MapGet("/", context =>
{
    context.Response.Redirect("/Home/Index");
    return Task.CompletedTask;
});

// -------------------------
// 1️⃣0️⃣ Debug: List Buckets (optional)
// -------------------------
var credPath = Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS");
if (string.IsNullOrWhiteSpace(credPath))
{
    Console.WriteLine("GOOGLE_APPLICATION_CREDENTIALS not set.");
}
else
{
    Console.WriteLine($"Using credentials from: {credPath}");
    var storageClient = StorageClient.Create();
    try
    {
        Console.WriteLine("Buckets in project 'hoangassignment':");
        foreach (var bucket in storageClient.ListBuckets("hoangassignment"))
        {
            Console.WriteLine($"- {bucket.Name}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error listing buckets: {ex.Message}");
    }
}

// -------------------------
// 1️⃣1️⃣ Run app
// -------------------------
app.Run();
