using Google.Cloud.Storage.V1;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using ThumbnailService.Models;
using ThumbnailService.Services;

var builder = WebApplication.CreateBuilder(args);

// MVC
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// Kestrel listen on PORT (Cloud Run)
var portEnv = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(portEnv) && int.TryParse(portEnv, out var port))
{
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.ListenAnyIP(port);
        options.Limits.MaxRequestBodySize = 104857600; // 100 MB
    });
}

// GCS + Services
builder.Services.AddSingleton(provider => GoogleStorageService.CreateClientFromEnvironment());
builder.Services.AddSingleton<IStorageService, GoogleStorageService>();
builder.Services.AddSingleton<IThumbnailService, ThumbnailServiceImpl>();

// PostgreSQL
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("CloudSqlPostgres")));

// Multipart upload limit (100 MB)
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 104857600; 
});

var app = builder.Build();

// Middleware
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// Routes
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}"
);

app.MapGet("/", context =>
{
    context.Response.Redirect("/Home/Index");
    return Task.CompletedTask;
});

// Debug: List buckets
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

app.Run();
