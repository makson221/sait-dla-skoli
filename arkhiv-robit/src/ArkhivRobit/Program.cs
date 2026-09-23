using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Threading.RateLimiting;
using ArkhivRobit.Data;
using ArkhivRobit.Services;
using ArkhivRobit.Services.Storage;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.WebEncoders;

var builder = WebApplication.CreateBuilder(args);

// Хостинги на кшталт Render чи Railway передають порт у змінній PORT.
if (Environment.GetEnvironmentVariable("PORT") is { Length: > 0 } port)
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("uk-UA");

// ---------- Налаштування ----------
var archiveSection = builder.Configuration.GetSection(ArchiveOptions.Section);
builder.Services.Configure<ArchiveOptions>(archiveSection);
builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection(StorageOptions.Section));
var archive = archiveSection.Get<ArchiveOptions>() ?? new ArchiveOptions();

// Дозволяємо великі файли (розмір задається в Archive__MaxFileSizeMb).
var maxRequestBytes = archive.MaxFileSizeBytes + 1024 * 1024;
builder.WebHost.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = maxRequestBytes);
builder.Services.Configure<FormOptions>(f => f.MultipartBodyLengthLimit = maxRequestBytes);

// ---------- База даних і сховище файлів ----------
builder.Services.AddArchiveDatabase(builder.Configuration, builder.Environment.ContentRootPath);
builder.Services.AddDataProtection().PersistKeysToDbContext<AppDbContext>().SetApplicationName("ArkhivRobit");

var storageProvider = builder.Configuration["Storage:Provider"] ?? "Local";
if (storageProvider.Equals("S3", StringComparison.OrdinalIgnoreCase))
    builder.Services.AddSingleton<IFileStorage, S3FileStorage>();
else
    builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();

// ---------- Вхід за кодом і права доступу ----------
builder.Services.AddScoped<AccessService>();
builder.Services.AddScoped<ArkhivRobit.Pages.Works.WorkSaver>();
builder.Services.AddSingleton<IAuthorizationHandler, ViewAccessHandler>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/Login";
        o.AccessDeniedPath = "/Login";
        o.LogoutPath = "/Logout";
        o.Cookie.Name = "arkhiv.auth";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.ExpireTimeSpan = TimeSpan.FromDays(30);
        o.SlidingExpiration = true;
        o.Events.OnValidatePrincipal = AccessService.ValidatePrincipalAsync;
    });

builder.Services.AddAuthorization(o =>
{
    o.AddPolicy(Policies.View, p => p.AddRequirements(new ViewAccessRequirement()));
    o.AddPolicy(Policies.Upload, p => p.RequireRole(Roles.Admin, Roles.Teacher));
    o.AddPolicy(Policies.Admin, p => p.RequireRole(Roles.Admin));
});

// Захист від підбору кодів: обмежена кількість спроб входу за 5 хвилин з однієї IP-адреси.
// Рахуються лише надсилання форми (POST), а не відкриття сторінки.
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("login", context => HttpMethods.IsPost(context.Request.Method)
        ? RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = archive.LoginAttemptsLimit, Window = TimeSpan.FromMinutes(5) })
        : RateLimitPartition.GetNoLimiter("get"));
});

// Кирилиця в HTML виводиться як є, а не у вигляді «&#x41D;…».
builder.Services.Configure<WebEncoderOptions>(o => o.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All));

builder.Services.AddRazorPages(o =>
{
    o.Conventions.AuthorizeFolder("/", Policies.View);
    o.Conventions.AuthorizeFolder("/Admin", Policies.Admin);
    o.Conventions.AuthorizePage("/Works/Upload", Policies.Upload);
    o.Conventions.AuthorizePage("/Works/Edit", Policies.Upload);
    o.Conventions.AllowAnonymousToPage("/Login");
    o.Conventions.AllowAnonymousToPage("/Error");
});

// Хмарні хостинги стоять за проксі: беремо реальну IP-адресу й протокол із заголовків X-Forwarded-*.
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

var app = builder.Build();

// ---------- Створення таблиць при першому запуску ----------
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();

    if (string.IsNullOrWhiteSpace(archive.AdminCode))
        app.Logger.LogWarning("Код адміністратора не задано (Archive__AdminCode). Вхід адміністратора вимкнено.");
    else if (!app.Environment.IsDevelopment() && AccessCodes.Normalize(archive.AdminCode).Length < 10)
        app.Logger.LogWarning("Код адміністратора закороткий. Використовуйте щонайменше 10 символів.");
}

app.UseForwardedHeaders();
if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

// Скачування файлу роботи: з хмари — переадресація на тимчасове посилання, з диска — напряму.
app.MapGet("/download/{id:int}", async (int id, AppDbContext db, IFileStorage storage, CancellationToken ct) =>
{
    var work = await db.Works.AsNoTracking().FirstOrDefaultAsync(w => w.Id == id, ct);
    if (work is null) return Results.NotFound();

    await db.Works.Where(w => w.Id == id)
        .ExecuteUpdateAsync(s => s.SetProperty(w => w.DownloadCount, w => w.DownloadCount + 1), ct);

    var url = await storage.GetDownloadUrlAsync(work.FileKey, work.FileName, ct);
    if (url is not null) return Results.Redirect(url);

    var stream = await storage.OpenReadAsync(work.FileKey, ct);
    return Results.File(stream, work.ContentType, work.FileName, enableRangeProcessing: true);
}).RequireAuthorization(Policies.View);

app.Run();

/// <summary>Потрібно для інтеграційних тестів (WebApplicationFactory).</summary>
public partial class Program { }
