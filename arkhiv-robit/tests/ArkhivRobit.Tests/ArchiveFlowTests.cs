using System.Net;
using System.Text.RegularExpressions;

namespace ArkhivRobit.Tests;

/// <summary>Інтеграційні тести: застосунок запускається повністю, запити йдуть як із браузера.</summary>
public class ArchiveFlowTests : IClassFixture<ArchiveAppFactory>
{
    private readonly ArchiveAppFactory _factory;

    public ArchiveFlowTests(ArchiveAppFactory factory) => _factory = factory;

    private async Task<string> CreateTeacherCodeAsync(string name)
    {
        var admin = _factory.CreateBrowser();
        await admin.LoginAsync(ArchiveAppFactory.AdminCode);
        var response = await admin.PostFormAsync("/Admin", "/Admin?handler=Add", new() { ["NewTeacherName"] = name });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var html = await admin.GetStringAsync("/Admin");
        return Regex.Match(html, "class=\"new-code\">([A-Z0-9-]+)<").Groups[1].Value;
    }

    [Fact]
    public async Task Catalog_IsPublic_ButUploadRequiresCode()
    {
        var guest = _factory.CreateBrowser();

        Assert.Equal(HttpStatusCode.OK, (await guest.GetAsync("/")).StatusCode);

        var upload = await guest.GetAsync("/Works/Upload");
        Assert.Equal(HttpStatusCode.Redirect, upload.StatusCode);
        Assert.StartsWith("/Login", upload.Headers.Location!.PathAndQuery);
    }

    [Fact]
    public async Task WrongCode_IsRejected()
    {
        var guest = _factory.CreateBrowser();
        var response = await guest.PostFormAsync("/Login", "/Login", new() { ["Code"] = "WRONG-CODE" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Невірний код доступу", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Teacher_CanUpload_AndEveryoneCanFindAndDownload()
    {
        var code = await CreateTeacherCodeAsync("Петренко Олена");
        Assert.Matches("^[A-Z0-9]{4}-[A-Z0-9]{4}$", code);

        var teacher = _factory.CreateBrowser();
        await teacher.LoginAsync(code.ToLowerInvariant());

        var content = "%PDF-1.4 тестовий файл"u8.ToArray();
        var upload = await teacher.UploadAsync("Система обліку успішності", "кн 42", 2024, content, "Робота.pdf");
        Assert.Equal(HttpStatusCode.Redirect, upload.StatusCode);
        var id = int.Parse(upload.Headers.Location!.OriginalString.Split('/').Last());

        var guest = _factory.CreateBrowser();
        var catalog = await guest.GetStringAsync("/?group=КН-42&year=2024");
        Assert.Contains("Система обліку успішності", catalog);

        var other = await guest.GetStringAsync("/?group=КН-42&year=2023");
        Assert.DoesNotContain("Система обліку успішності", other);

        var download = await guest.GetAsync($"/download/{id}");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal(content, await download.Content.ReadAsByteArrayAsync());
        Assert.Equal("Робота.pdf", download.Content.Headers.ContentDisposition?.FileNameStar);
    }

    [Fact]
    public async Task Teacher_CannotEditSomeoneElsesWork_OrOpenAdmin()
    {
        var author = _factory.CreateBrowser();
        await author.LoginAsync(await CreateTeacherCodeAsync("Автор"));
        var upload = await author.UploadAsync("Чужа робота", "ІПЗ-11", 2025, "%PDF"u8.ToArray(), "a.pdf");
        var id = upload.Headers.Location!.OriginalString.Split('/').Last();

        var stranger = _factory.CreateBrowser();
        await stranger.LoginAsync(await CreateTeacherCodeAsync("Інший викладач"));

        Assert.Equal(HttpStatusCode.Redirect, (await stranger.GetAsync($"/Works/Edit/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await stranger.GetAsync("/Admin")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await author.GetAsync($"/Works/Edit/{id}")).StatusCode);
    }

    [Fact]
    public async Task Upload_RejectsForbiddenFileType()
    {
        var teacher = _factory.CreateBrowser();
        await teacher.LoginAsync(await CreateTeacherCodeAsync("Викладач"));

        var response = await teacher.UploadAsync("Шкідливий файл", "КН-41", 2025, new byte[] { 1, 2, 3 }, "virus.exe");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Непідтримуваний формат", await response.Content.ReadAsStringAsync());
    }
}

/// <summary>Якщо задано код перегляду, каталог закритий для сторонніх.</summary>
public class PrivateArchiveTests : IDisposable
{
    private readonly ArchiveAppFactory _factory = new() { ViewCode = "perehliad-2026" };

    [Fact]
    public async Task Catalog_RequiresViewCode()
    {
        var guest = _factory.CreateBrowser();
        Assert.Equal(HttpStatusCode.Redirect, (await guest.GetAsync("/")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await guest.GetAsync("/download/1")).StatusCode);

        await guest.LoginAsync("PEREHLIAD 2026");
        Assert.Equal(HttpStatusCode.OK, (await guest.GetAsync("/")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await guest.GetAsync("/Works/Upload")).StatusCode);
    }

    public void Dispose() => _factory.Dispose();
}

/// <summary>Після кількох невдалих спроб входу сервер тимчасово перестає приймати нові.</summary>
public class LoginRateLimitTests : IDisposable
{
    private readonly ArchiveAppFactory _factory = new() { LoginAttemptsLimit = 3 };

    [Fact]
    public async Task TooManyAttempts_AreBlocked()
    {
        var guest = _factory.CreateBrowser();
        for (var i = 0; i < 3; i++)
        {
            var attempt = await guest.PostFormAsync("/Login", "/Login", new() { ["Code"] = $"WRONG-{i}" });
            Assert.Equal(HttpStatusCode.OK, attempt.StatusCode);
        }

        var blocked = await guest.PostFormAsync("/Login", "/Login", new() { ["Code"] = ArchiveAppFactory.AdminCode });
        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);

        // Сама сторінка входу при цьому відкривається.
        Assert.Equal(HttpStatusCode.OK, (await guest.GetAsync("/Login")).StatusCode);
    }

    public void Dispose() => _factory.Dispose();
}
