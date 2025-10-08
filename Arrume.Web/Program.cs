using Arrume.Web.Data;
using Arrume.Web.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.HttpLogging;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
var env = builder.Environment;

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.AddHttpLogging(o =>
{
    o.LoggingFields = HttpLoggingFields.RequestMethod |
                      HttpLoggingFields.RequestPath |
                      HttpLoggingFields.ResponseStatusCode |
                      HttpLoggingFields.Duration;
});

builder.Services.AddControllersWithViews();

builder.Services.Configure<ZApiOptions>(builder.Configuration.GetSection("ZApi"));
var zapi = builder.Configuration.GetSection("ZApi").Get<ZApiOptions>() ?? new();

builder.Services.AddHttpClient<ILinkShortenerService, TinyUrlService>();

if (zapi.UseFake)
{
    builder.Services.AddSingleton<IZApiService, ZApiFakeService>();
}
else
{
    builder.Services.AddHttpClient<IZApiService, ZApiService>((provider, client) =>
    {
        var config = provider.GetRequiredService<IConfiguration>();
        var baseUrl = config["ZApi:UrlBase"] ?? "https://api.z-api.io";
        client.BaseAddress = new Uri(baseUrl);
        client.Timeout = TimeSpan.FromSeconds(config.GetValue<int>("ZApi:TimeoutSeconds", 30));
    });
}

if (env.IsDevelopment())
{
    builder.Services.AddDbContext<DevSqliteDbContext>(o =>
        o.UseSqlite(builder.Configuration.GetConnectionString("Sqlite")));
    builder.Services.AddScoped<ILeadStore, LeadStoreSqlite>();
    builder.Services.AddScoped<ICapoteiroProvider, CapoteiroProviderDevColleagues>();
}
else
{
    builder.Services.AddScoped<ILeadStore, LeadStoreSqlServer>();
    builder.Services.AddScoped<ICapoteiroProvider, CapoteiroProviderAzure>();
}

var app = builder.Build();

app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["X-Frame-Options"] = "DENY";
    ctx.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    ctx.Response.Headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";
    ctx.Response.Headers["Content-Security-Policy"] =
    "default-src 'self'; " +
    "img-src 'self' data: https:; " +
    "script-src 'self' 'unsafe-inline' https://code.jquery.com https://cdnjs.cloudflare.com https://www.googletagmanager.com https://connect.facebook.net; " +
    "style-src 'self' 'unsafe-inline'; " +
    "connect-src 'self' https://viacep.com.br https://viacep.com https://api.tinyurl.com https://is.gd https://www.google-analytics.com https://analytics.google.com https://www.facebook.com; " +
    "frame-src https://www.googletagmanager.com https://www.facebook.com; " +
    "frame-ancestors 'none';";
    await next();
});

bool devAuthEnabled = app.Configuration.GetValue<bool>("DevAuth:Enabled", false);
string devUser = app.Configuration["DevAuth:User"] ?? "";
string devPass = app.Configuration["DevAuth:Pass"] ?? "";

if (devAuthEnabled)
{
    app.Use(async (ctx, next) =>
    {
        bool ok = false;

        if (ctx.Request.Headers.TryGetValue("Authorization", out var auth) &&
            auth.ToString().StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var b64 = auth.ToString().Substring("Basic ".Length).Trim();
                var bytes = Convert.FromBase64String(b64);
                var decoded = Encoding.UTF8.GetString(bytes);
                var parts = decoded.Split(':', 2);
                if (parts.Length == 2)
                    ok = (parts[0] == devUser && parts[1] == devPass);
            }
            catch { /* ignore */ }
        }

        if (!ok)
        {
            ctx.Response.StatusCode = 401;
            ctx.Response.Headers["WWW-Authenticate"] = "Basic realm=\"ARRUME-DEV\"";
            ctx.Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
            await ctx.Response.WriteAsync("Unauthorized");
            return;
        }

        ctx.Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
        await next();
    });
}

var fwd = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    ForwardLimit = 2
};
fwd.KnownNetworks.Clear();
fwd.KnownProxies.Clear();
app.UseForwardedHeaders(fwd);

app.UseHttpLogging();

if (!env.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseRouting();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

using (var scope = app.Services.CreateScope())
{
    var prov = scope.ServiceProvider.GetRequiredService<ICapoteiroProvider>();
    var shortener = scope.ServiceProvider.GetRequiredService<ILinkShortenerService>();
    
    app.Logger.LogInformation("ENV={Env} | ZAPI={Mode} | Provider={Provider} | Shortener={Shortener}",
        env.EnvironmentName, zapi.UseFake ? "FAKE" : "REAL", prov.GetType().Name, shortener.GetType().Name);

    if (env.IsDevelopment())
    {
        var db = scope.ServiceProvider.GetRequiredService<DevSqliteDbContext>();
        db.Database.EnsureCreated();
    }
}

var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
{
    app.Urls.Add($"http://0.0.0.0:{port}");
}
app.Logger.LogInformation("➡️ ARRUME ouvindo na(s) URL(s): {Urls} (ENV={Env})",
    string.Join(", ", app.Urls), env.EnvironmentName);

app.Run();