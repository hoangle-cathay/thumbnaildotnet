using ThumbnailService.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using ThumbnailService.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS for React frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("ReactApp", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "https://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Kestrel port config (Cloud Run)
var portEnv = Environment.GetEnvironmentVariable("PORT") ?? "5001";
if (!string.IsNullOrWhiteSpace(portEnv) && int.TryParse(portEnv, out var port))
{
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.ListenAnyIP(port);
        options.Limits.MaxRequestBodySize = 104857600; // 100 MB
    });
}

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
        options.LoginPath = "/api/Account/login";
        options.LogoutPath = "/api/Account/logout";
        options.AccessDeniedPath = "/api/Account/access-denied";
        options.ExpireTimeSpan = TimeSpan.FromHours(1);
        options.SlidingExpiration = true;
    });

builder.Services.AddSession();

// Multipart upload limit
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 104857600; // 100 MB
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// Enable Swagger in development
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// CORS must be before Authentication
app.UseCors("ReactApp");

app.UseAuthentication();
app.UseAuthorization();
app.UseSession();

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

app.MapControllers();

app.Run();