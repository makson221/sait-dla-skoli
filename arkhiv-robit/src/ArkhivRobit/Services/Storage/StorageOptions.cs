namespace ArkhivRobit.Services.Storage;

/// <summary>Налаштування сховища (секція «Storage» або змінні середовища Storage__*).</summary>
public class StorageOptions
{
    public const string Section = "Storage";

    /// <summary>«Local» — папка на диску, «S3» — хмарне сховище.</summary>
    public string Provider { get; set; } = "Local";

    /// <summary>Папка для файлів при Provider = Local.</summary>
    public string LocalPath { get; set; } = "App_Data/files";

    /// <summary>Адреса S3 API, наприклад https://&lt;account&gt;.r2.cloudflarestorage.com або https://s3.eu-central-003.backblazeb2.com.</summary>
    public string ServiceUrl { get; set; } = "";

    public string Region { get; set; } = "auto";

    public string Bucket { get; set; } = "";

    public string AccessKey { get; set; } = "";

    public string SecretKey { get; set; } = "";

    /// <summary>Скільки хвилин діє посилання на скачування.</summary>
    public int DownloadLinkMinutes { get; set; } = 10;
}
