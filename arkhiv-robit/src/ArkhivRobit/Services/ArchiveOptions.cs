namespace ArkhivRobit.Services;

/// <summary>Налаштування архіву (секція «Archive» в appsettings.json або змінні середовища Archive__*).</summary>
public class ArchiveOptions
{
    public const string Section = "Archive";

    /// <summary>Код адміністратора. Обов'язково задайте власний у змінній середовища Archive__AdminCode.</summary>
    public string AdminCode { get; set; } = "";

    /// <summary>
    /// Код для перегляду архіву. Якщо порожній, роботи може переглядати й скачувати будь-хто.
    /// Якщо заданий, спершу потрібно ввести цей код.
    /// </summary>
    public string ViewCode { get; set; } = "";

    /// <summary>Максимальний розмір одного файлу в мегабайтах.</summary>
    public int MaxFileSizeMb { get; set; } = 200;

    /// <summary>Дозволені розширення файлів.</summary>
    public string[] AllowedExtensions { get; set; } =
        { ".pdf", ".doc", ".docx", ".odt", ".rtf", ".ppt", ".pptx", ".zip", ".rar", ".7z" };

    /// <summary>Скільки спроб входу дозволено з однієї IP-адреси за 5 хвилин.</summary>
    public int LoginAttemptsLimit { get; set; } = 10;

    public long MaxFileSizeBytes => MaxFileSizeMb * 1024L * 1024L;

    public bool IsPublic => string.IsNullOrWhiteSpace(ViewCode);
}
