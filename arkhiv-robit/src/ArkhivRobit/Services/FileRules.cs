namespace ArkhivRobit.Services;

/// <summary>Перевірка файлів, які завантажують викладачі.</summary>
public static class FileRules
{
    /// <summary>Повертає текст помилки або null, якщо файл підходить.</summary>
    public static string? Validate(string fileName, long size, ArchiveOptions options)
    {
        if (size <= 0) return "Файл порожній.";
        if (size > options.MaxFileSizeBytes) return $"Файл завеликий. Максимальний розмір — {options.MaxFileSizeMb} МБ.";

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (!options.AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            return $"Непідтримуваний формат файлу. Дозволено: {string.Join(", ", options.AllowedExtensions)}.";

        return null;
    }

    /// <summary>
    /// Ключ файлу у сховищі. Використовуються лише латинські символи й GUID, щоб уникнути
    /// проблем із кирилицею та однаковими назвами; оригінальна назва зберігається в базі.
    /// </summary>
    public static string CreateKey(int year, string fileName) =>
        $"works/{year}/{Guid.NewGuid():N}{Path.GetExtension(fileName).ToLowerInvariant()}";

    /// <summary>Назва файлу без шляху (деякі браузери передають повний шлях).</summary>
    public static string SafeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName.Replace('\\', '/'));
        return string.IsNullOrWhiteSpace(name) ? "file" : name.Trim();
    }

    public static string FormatSize(long bytes) => bytes switch
    {
        0 => "0 КБ",
        < 1024 * 1024 => $"{Math.Max(1, bytes / 1024)} КБ",
        < 1024L * 1024 * 1024 => $"{bytes / 1024d / 1024:0.0} МБ",
        _ => $"{bytes / 1024d / 1024 / 1024:0.00} ГБ",
    };
}
