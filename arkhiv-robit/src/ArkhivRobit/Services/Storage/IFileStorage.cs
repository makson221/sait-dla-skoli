namespace ArkhivRobit.Services.Storage;

/// <summary>
/// Сховище файлів робіт. Є дві реалізації:
/// <see cref="LocalFileStorage"/> — папка на диску (для розробки),
/// <see cref="S3FileStorage"/> — хмарне сховище, сумісне з Amazon S3 (Cloudflare R2, Backblaze B2 тощо).
/// </summary>
public interface IFileStorage
{
    Task SaveAsync(string key, Stream content, string contentType, CancellationToken ct = default);

    /// <summary>
    /// Пряме тимчасове посилання на скачування з хмари. Null — сховище не вміє видавати посилання,
    /// тоді файл віддає сам сервер через <see cref="OpenReadAsync"/>.
    /// </summary>
    Task<string?> GetDownloadUrlAsync(string key, string fileName, CancellationToken ct = default);

    Task<Stream> OpenReadAsync(string key, CancellationToken ct = default);

    Task DeleteAsync(string key, CancellationToken ct = default);
}
