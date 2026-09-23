using System.ComponentModel.DataAnnotations;
using ArkhivRobit.Data;
using ArkhivRobit.Models;
using ArkhivRobit.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ArkhivRobit.Pages.Admin;

/// <summary>Адміністрування: коди доступу викладачів і статистика архіву.</summary>
public class IndexModel : PageModel
{
    private readonly AppDbContext _db;

    public IndexModel(AppDbContext db) => _db = db;

    [BindProperty]
    [Required(ErrorMessage = "Вкажіть ПІБ викладача.")]
    [StringLength(200, MinimumLength = 2, ErrorMessage = "ПІБ має містити від 2 до 200 символів.")]
    public string NewTeacherName { get; set; } = "";

    public List<TeacherRow> Teachers { get; private set; } = new();

    public int WorksCount { get; private set; }
    public long TotalSize { get; private set; }
    public int TotalDownloads { get; private set; }
    public List<(int Year, int Coursework, int Diploma)> ByYear { get; private set; } = new();

    /// <summary>Щойно створений код показується лише один раз — у базі зберігається тільки його хеш.</summary>
    [TempData]
    public string? NewCode { get; set; }

    [TempData]
    public string? NewCodeOwner { get; set; }

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostAddAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }

        var code = AccessCodes.Generate();
        _db.Teachers.Add(new Teacher
        {
            Name = TextNormalizer.Clean(NewTeacherName),
            CodeHash = AccessCodes.Hash(code),
            CreatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        NewCode = code;
        NewCodeOwner = TextNormalizer.Clean(NewTeacherName);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostNewCodeAsync(int id)
    {
        var teacher = await _db.Teachers.FindAsync(id);
        if (teacher is null) return NotFound();

        var code = AccessCodes.Generate();
        teacher.CodeHash = AccessCodes.Hash(code);
        await _db.SaveChangesAsync();

        NewCode = code;
        NewCodeOwner = teacher.Name;
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostToggleAsync(int id)
    {
        var teacher = await _db.Teachers.FindAsync(id);
        if (teacher is null) return NotFound();

        teacher.IsActive = !teacher.IsActive;
        await _db.SaveChangesAsync();

        TempData["Message"] = teacher.IsActive ? $"Доступ для «{teacher.Name}» увімкнено." : $"Доступ для «{teacher.Name}» вимкнено.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var teacher = await _db.Teachers.FindAsync(id);
        if (teacher is null) return NotFound();

        // Роботи викладача залишаються в архіві, змінюється лише зв'язок з автором завантаження.
        await _db.Works.Where(w => w.UploadedByTeacherId == id)
            .ExecuteUpdateAsync(s => s.SetProperty(w => w.UploadedByTeacherId, (int?)null));
        _db.Teachers.Remove(teacher);
        await _db.SaveChangesAsync();

        TempData["Message"] = $"Викладача «{teacher.Name}» видалено. Його роботи залишились в архіві.";
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        Teachers = await _db.Teachers
            .OrderBy(t => t.Name)
            .Select(t => new TeacherRow(t.Id, t.Name, t.IsActive, t.CreatedAt, t.Works.Count))
            .ToListAsync();

        WorksCount = await _db.Works.CountAsync();
        // Суму рахуємо в пам'яті: SQLite не вміє агрегувати long через EF так само, як PostgreSQL.
        var sizes = await _db.Works.Select(w => new { w.FileSize, w.DownloadCount }).ToListAsync();
        TotalSize = sizes.Sum(s => s.FileSize);
        TotalDownloads = sizes.Sum(s => s.DownloadCount);

        var byYear = await _db.Works
            .GroupBy(w => new { w.Year, w.Type })
            .Select(g => new { g.Key.Year, g.Key.Type, Count = g.Count() })
            .ToListAsync();
        ByYear = byYear
            .GroupBy(x => x.Year)
            .OrderByDescending(g => g.Key)
            .Select(g => (g.Key,
                g.Where(x => x.Type == WorkType.Coursework).Sum(x => x.Count),
                g.Where(x => x.Type == WorkType.Diploma).Sum(x => x.Count)))
            .ToList();
    }

    public record TeacherRow(int Id, string Name, bool IsActive, DateTime CreatedAt, int WorksCount);
}
