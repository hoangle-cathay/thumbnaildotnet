using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using ThumbnailService.Models;
using ThumbnailService.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Database: Cloud SQL (PostgreSQL) via connection string with SSL options from config
var connectionString = builder.Configuration.GetConnectionString("CloudSqlPostgres") ?? "";
builder.Services.AddDbContext<AppDbContext>(options =>
{
	options.UseNpgsql(connectionString);
});

// Cookie authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
	.AddCookie(options =>
	{
		options.LoginPath = "/Account/Login";
		options.LogoutPath = "/Account/Logout";
		options.AccessDeniedPath = "/Account/AccessDenied";
	});

builder.Services.AddSession();

// GCP services (Storage, etc.)
builder.Services.AddGoogleCloudServices(builder.Configuration);
builder.Services.AddSingleton<IThumbnailService, ThumbnailServiceImpl>();

// Encryption service: prefer Secret Manager, then env, then dev fallback
byte[] ResolveSecretBytes(string? smName, string envVar)
{
    // Try Secret Manager name from config if provided
    if (!string.IsNullOrWhiteSpace(smName))
    {
        try
        {
            var client = Google.Cloud.SecretManager.V1.SecretManagerServiceClient.Create();
            var versionName = Google.Cloud.SecretManager.V1.SecretVersionName.Parse(smName);
            var payload = client.AccessSecretVersion(versionName).Payload.Data.ToArray();
            // Expect base64-encoded contents
            var asString = System.Text.Encoding.UTF8.GetString(payload);
            return Convert.FromBase64String(asString.Trim());
        }
        catch
        {
            // fall back to env
        }
    }
    var envVal = Environment.GetEnvironmentVariable(envVar);
    if (!string.IsNullOrWhiteSpace(envVal))
    {
        return Convert.FromBase64String(envVal);
    }
    // Dev fallback only
    var bytes = envVar.Contains("IV") ? new byte[16] : new byte[32];
    Random.Shared.NextBytes(bytes);
    return bytes;
}

var aesKeySm = builder.Configuration["Security:AesKeySecretName"];
var aesIvSm = builder.Configuration["Security:AesIvSecretName"];
var keyBytes = ResolveSecretBytes(aesKeySm, "AES_KEY_BASE64");
var ivBytes = ResolveSecretBytes(aesIvSm, "AES_IV_BASE64");
builder.Services.AddSingleton<IEncryptionService>(new EncryptionService(keyBytes, ivBytes));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
	app.UseExceptionHandler("/Home/Error");
	app.UseHsts();
}

// Auto-apply EF migrations at startup
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.Migrate();
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Failed to apply migrations");
    }
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();
app.UseSession();

app.MapControllerRoute(
	name: "default",
	pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
