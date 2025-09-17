
using Google.Cloud.Storage.V1;
using Microsoft.EntityFrameworkCore;
using ThumbnailService.Models;
using ThumbnailService.Services;


var builder = WebApplication.CreateBuilder(args);

// ✅ 1. Register MVC services
builder.Services.AddControllersWithViews(); // This is required for controllers and views
builder.Services.AddRazorPages(); // This is required for Razor Pages

// Configure Kestrel to listen on the port from the PORT environment variable if set
var portEnv = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(portEnv) && int.TryParse(portEnv, out var port))
{
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.ListenAnyIP(port);
    });
}


// Add services to the container.
builder.Services.AddControllersWithViews();

// Register StorageClient
builder.Services.AddSingleton(StorageClient => GoogleStorageService.CreateClientFromEnvironment());

// Register IStorageService with your implementation
builder.Services.AddSingleton<IStorageService, GoogleStorageService>();

// Register IThumbnailService
builder.Services.AddSingleton<IThumbnailService, ThumbnailServiceImpl>();

// Register DbContext with PostgreSQL
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("CloudSqlPostgres")));


var app = builder.Build();

// --------------------
// 2️⃣ Configure middleware
// --------------------
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();  // wwwroot
app.UseRouting();

// --------------------
// 3️⃣ Map routes
// --------------------
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}"
);

// Optional: redirect root "/" to Home/Index
app.MapGet("/", context =>
{
    context.Response.Redirect("/Home/Index");
    return Task.CompletedTask;
});
// ------------------------------------
// ====================================


// 1. Load GOOGLE_APPLICATION_CREDENTIALS from environment variable
var credPath = Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS");
if (string.IsNullOrWhiteSpace(credPath))
{
    Console.WriteLine("GOOGLE_APPLICATION_CREDENTIALS environment variable is not set.");
}
else
{
    Console.WriteLine($"Using credentials from: {credPath}");
    // 2. Connect to Google Cloud Storage in project "hoangassignment"
    var storageClient = StorageClient.Create();
    try
    {
        // 3. List all buckets and write output to console
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
