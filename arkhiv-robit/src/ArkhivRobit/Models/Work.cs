namespace ArkhivRobit.Models;

/// <summary>Курсова або дипломна робота, що зберігається в архіві.</summary>
public class Work
{
    public int Id { get; set; }

    public WorkType Type { get; set; }

    /// <summary>Тема роботи.</summary>
    public string Title { get; set; } = "";

    /// <summary>ПІБ студента.</summary>
    public string StudentName { get; set; } = "";

    /// <summary>Назва групи у нормалізованому вигляді (наприклад, «КН-41»).</summary>
    public string GroupName { get; set; } = "";

    /// <summary>Рік захисту.</summary>
    public int Year { get; set; }

    /// <summary>Науковий керівник (необов'язково).</summary>
    public string? Supervisor { get; set; }

    /// <summary>Короткий опис або анотація (необов'язково).</summary>
    public string? Description { get; set; }

    /// <summary>Ключ файлу у сховищі (наприклад, «works/2026/3f2a….pdf»).</summary>
    public string FileKey { get; set; } = "";

    /// <summary>Оригінальна назва файлу, під якою його буде скачано.</summary>
    public string FileName { get; set; } = "";

    public long FileSize { get; set; }

    public string ContentType { get; set; } = "application/octet-stream";

    /// <summary>Текст для пошуку в нижньому регістрі: тема, студент, керівник, група.</summary>
    public string SearchText { get; set; } = "";

    public int DownloadCount { get; set; }

    /// <summary>Викладач, який завантажив роботу (null — адміністратор або викладача видалено).</summary>
    public int? UploadedByTeacherId { get; set; }

    public Teacher? UploadedByTeacher { get; set; }

    /// <summary>Ім'я того, хто завантажив, збережене на момент завантаження.</summary>
    public string UploadedByName { get; set; } = "";

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
