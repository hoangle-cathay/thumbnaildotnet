using Google.Cloud.Kms.V1;
using Google.Cloud.Storage.V1;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using ThumbnailService.Models;
using ThumbnailService.Services;

var builder = WebApplication.CreateBuilder(args);

// -------------------------
// 1️⃣ MVC & Razor Pages
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
// 3️⃣ Google Cloud Storage & Thumbnail Services
// -------------------------
builder.Services.AddSingleton(StorageClient.Create());
builder.Services.AddSingleton<IStorageService, GoogleStorageService>();
builder.Services.AddSingleton<IThumbnailService, ThumbnailServiceImpl>();

var pubsubTopic = builder.Configuration.GetValue<string>("Gcp:ThumbnailJobTopic") ?? string.Empty;
builder.Services.AddSingleton(new PubSubService(pubsubTopic));

// -------------------------
// 4️⃣ PostgreSQL + DbContext
// -------------------------
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("CloudSqlPostgres")));

// -------------------------
// 5️⃣ Multipart upload limit
// -------------------------
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 104857600; // 100 MB
});

// -------------------------
// 6️⃣ KMS Encryption service (AES)
// -------------------------
builder.Services.AddSingleton<IEncryptionService>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var keyBase64 = config["Security:AesKeyBase64"];
    var ivBase64 = config["Security:AesIvBase64"];
    if (string.IsNullOrEmpty(keyBase64) || string.IsNullOrEmpty(ivBase64))
        throw new Exception("Missing AES key/iv in config");
    var key = Convert.FromBase64String(keyBase64);
    var iv = Convert.FromBase64String(ivBase64);
    return new EncryptionService(key, iv);
});

// -------------------------
// 7️⃣ JWT Service with KMS
// -------------------------
builder.Services.AddSingleton(KeyManagementServiceClient.Create());
builder.Services.AddScoped<IJwtService, KmsJwtService>();

// -------------------------
// 8️⃣ Build app
// -------------------------
var app = builder.Build();

// -------------------------
// 9️⃣ Middleware pipeline
// -------------------------
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// JWT Auth Middleware (requires IJwtService)
app.UseMiddleware<ThumbnailService.Middleware.JwtAuthMiddleware>();

// -------------------------
// 1️⃣0️⃣ Routes
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
// 1️⃣1️⃣ Optional: Debug list buckets
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
// 1️⃣2️⃣ Run app
// -------------------------
app.Run();
