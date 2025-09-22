using Arrume.Web.Data;
using Arrume.Web.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.HttpLogging;

var builder = WebApplication.CreateBuilder(args);
var env = builder.Environment;

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// (Opcional) logging de requests/responses simplificado
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

// Security headers (CSP atualizada)
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["X-Frame-Options"] = "DENY";
    ctx.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    ctx.Response.Headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";
    ctx.Response.Headers["Content-Security-Policy"] =
    "default-src 'self'; " +
    "img-src 'self' data: https:; " +
    "script-src 'self' https://code.jquery.com https://cdnjs.cloudflare.com; " +
    "style-src 'self' 'unsafe-inline'; " +
    "connect-src 'self' https://viacep.com.br https://viacep.com; " +
    "frame-ancestors 'none';";
    await next();
});

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

app.UseHttpLogging(); // <- habilita o http logging

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
    app.Logger.LogInformation("ENV={Env} | ZAPI={Mode} | Provider={Provider}",
        env.EnvironmentName, zapi.UseFake ? "FAKE" : "REAL", prov.GetType().Name);

    if (env.IsDevelopment())
    {
        var db = scope.ServiceProvider.GetRequiredService<DevSqliteDbContext>();
        db.Database.EnsureCreated();
    }
}

var url = Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? "http://localhost:5024";
app.Logger.LogInformation("➡️ ARRUME rodando em {Url} (ENV={Env})", url, env.EnvironmentName);
app.Run(url);
