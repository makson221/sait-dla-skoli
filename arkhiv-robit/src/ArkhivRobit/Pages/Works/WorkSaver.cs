using ArkhivRobit.Data;
using ArkhivRobit.Models;
using ArkhivRobit.Services;
using ArkhivRobit.Services.Storage;
using Microsoft.EntityFrameworkCore;

namespace ArkhivRobit.Pages.Works;

/// <summary>Спільна логіка збереження роботи для сторінок «Додати» і «Редагувати».</summary>
public class WorkSaver
{
    private readonly AppDbContext _db;
    private readonly IFileStorage _storage;
    private readonly ILogger<WorkSaver> _logger;

    public WorkSaver(AppDbContext db, IFileStorage storage, ILogger<WorkSaver> logger)
    {
        _db = db;
        _storage = storage;
        _logger = logger;
    }

    /// <summary>Переносить поля з форми в модель, нормалізуючи текст.</summary>
    public static void ApplyInput(Work work, WorkInput input)
    {
        work.Type = input.Type;
        work.Title = TextNormalizer.Clean(input.Title);
        work.StudentName = TextNormalizer.Clean(input.StudentName);
        work.GroupName = TextNormalizer.Group(input.GroupName);
        work.Year = input.Year;
        work.Supervisor = TextNormalizer.CleanOrNull(input.Supervisor);
        work.Description = input.Description?.Trim() is { Length: > 0 } d ? d : null;
        work.SearchText = TextNormalizer.SearchText(work.Title, work.StudentName, work.Supervisor, work.GroupName);
        work.UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Зберігає роботу. Якщо передано новий файл — спершу завантажує його в сховище,
    /// потім оновлює базу, а старий файл видаляє. Якщо запис у базу не вдався, новий файл прибирається.
    /// </summary>
    public async Task SaveAsync(Work work, IFormFile? file, CancellationToken ct)
    {
        string? oldKey = null;
        string? newKey = null;

        if (file is not null)
        {
            var fileName = FileRules.SafeFileName(file.FileName);
            newKey = FileRules.CreateKey(work.Year, fileName);
            var contentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType;

            await using (var stream = file.OpenReadStream())
                await _storage.SaveAsync(newKey, stream, contentType, ct);

            oldKey = string.IsNullOrEmpty(work.FileKey) ? null : work.FileKey;
            work.FileKey = newKey;
            work.FileName = fileName;
            work.FileSize = file.Length;
            work.ContentType = contentType;
        }

        try
        {
            if (work.Id == 0) _db.Works.Add(work);
            await _db.SaveChangesAsync(ct);
        }
        catch
        {
            if (newKey is not null) await TryDeleteAsync(newKey);
            throw;
        }

        if (oldKey is not null) await TryDeleteAsync(oldKey);
    }

    public async Task DeleteAsync(Work work, CancellationToken ct)
    {
        _db.Works.Remove(work);
        await _db.SaveChangesAsync(ct);
        await TryDeleteAsync(work.FileKey);
    }

    // Файл, який не вдалося видалити, не повинен ламати дію користувача — лише записуємо в журнал.
    private async Task TryDeleteAsync(string key)
    {
        try
        {
            await _storage.DeleteAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не вдалося видалити файл {Key} зі сховища", key);
        }
    }

    public Task<List<string>> KnownGroupsAsync(CancellationToken ct) =>
        _db.Works.Select(w => w.GroupName).Distinct().OrderBy(g => g).ToListAsync(ct);
}
