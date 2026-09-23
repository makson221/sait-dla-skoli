using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ArkhivRobit.Tests;

/// <summary>Запускає застосунок у пам'яті з тимчасовою базою SQLite та тимчасовою папкою для файлів.</summary>
public sealed class ArchiveAppFactory : WebApplicationFactory<Program>
{
    public const string AdminCode = "test-admin-code";

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "arkhiv-tests-" + Guid.NewGuid().ToString("N"));
    /// <summary>Код перегляду. Порожній — архів публічний.</summary>
    public string ViewCode { get; init; } = "";

    /// <summary>Ліміт спроб входу. У тестах великий, бо всі запити йдуть з однієї «IP-адреси».</summary>
    public int LoginAttemptsLimit { get; init; } = 1000;

    public ArchiveAppFactory() => Directory.CreateDirectory(_dir);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", $"Data Source={Path.Combine(_dir, "test.db")};Pooling=False");
        builder.UseSetting("Storage:Provider", "Local");
        builder.UseSetting("Storage:LocalPath", Path.Combine(_dir, "files"));
        builder.UseSetting("Archive:AdminCode", AdminCode);
        builder.UseSetting("Archive:ViewCode", ViewCode);
        builder.UseSetting("Archive:MaxFileSizeMb", "5");
        builder.UseSetting("Archive:LoginAttemptsLimit", LoginAttemptsLimit.ToString());
    }

    public HttpClient CreateBrowser() =>
        CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }
}

/// <summary>Допоміжні методи для роботи з формами (з урахуванням захисту від CSRF).</summary>
public static partial class BrowserExtensions
{
    public static async Task<string> GetTokenAsync(this HttpClient client, string url)
    {
        var html = await client.GetStringAsync(url);
        return AntiforgeryToken().Match(html).Groups[1].Value;
    }

    public static async Task<HttpResponseMessage> PostFormAsync(this HttpClient client, string pageUrl, string postUrl, Dictionary<string, string> fields)
    {
        fields["__RequestVerificationToken"] = await client.GetTokenAsync(pageUrl);
        return await client.PostAsync(postUrl, new FormUrlEncodedContent(fields));
    }

    public static async Task LoginAsync(this HttpClient client, string code)
    {
        var response = await client.PostFormAsync("/Login", "/Login", new() { ["Code"] = code });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    public static async Task<HttpResponseMessage> UploadAsync(this HttpClient client, string title, string group, int year, byte[] file, string fileName)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(await client.GetTokenAsync("/Works/Upload")), "__RequestVerificationToken" },
            { new StringContent("Diploma"), "Input.Type" },
            { new StringContent(title), "Input.Title" },
            { new StringContent("Тестовий Студент"), "Input.StudentName" },
            { new StringContent(group), "Input.GroupName" },
            { new StringContent(year.ToString()), "Input.Year" },
            { new ByteArrayContent(file), "Input.File", fileName },
        };
        return await client.PostAsync("/Works/Upload", form);
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex AntiforgeryToken();
}
