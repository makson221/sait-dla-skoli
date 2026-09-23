using Microsoft.Extensions.Options;

namespace ArkhivRobit.Services.Storage;

/// <summary>Зберігає файли в папці на диску сервера. Зручно для розробки та тестування.</summary>
public class LocalFileStorage : IFileStorage
{
    private readonly string _root;

    public LocalFileStorage(IOptions<StorageOptions> options, IWebHostEnvironment env)
    {
        _root = Path.GetFullPath(Path.Combine(env.ContentRootPath, options.Value.LocalPath));
        Directory.CreateDirectory(_root);
    }

    public async Task SaveAsync(string key, Stream content, string contentType, CancellationToken ct = default)
    {
        var path = ResolvePath(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var file = File.Create(path);
        await content.CopyToAsync(file, ct);
    }

    public Task<string?> GetDownloadUrlAsync(string key, string fileName, CancellationToken ct = default) =>
        Task.FromResult<string?>(null);

    public Task<Stream> OpenReadAsync(string key, CancellationToken ct = default) =>
        Task.FromResult<Stream>(File.OpenRead(ResolvePath(key)));

    public Task DeleteAsync(string key, CancellationToken ct = default)
    {
        var path = ResolvePath(key);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    // Не дозволяє ключу вийти за межі папки сховища (наприклад, через «../»).
    private string ResolvePath(string key)
    {
        var path = Path.GetFullPath(Path.Combine(_root, key));
        if (!path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidOperationException("Недопустимий ключ файлу.");
        return path;
    }
}
