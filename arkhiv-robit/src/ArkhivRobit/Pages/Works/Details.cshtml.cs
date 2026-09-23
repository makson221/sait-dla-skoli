using ArkhivRobit.Data;
using ArkhivRobit.Models;
using ArkhivRobit.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ArkhivRobit.Pages.Works;

/// <summary>Сторінка роботи з повним описом і кнопками «Скачати», «Редагувати», «Видалити».</summary>
public class DetailsModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly WorkSaver _saver;

    public DetailsModel(AppDbContext db, WorkSaver saver)
    {
        _db = db;
        _saver = saver;
    }

    public Work Work { get; private set; } = null!;

    public bool CanManage => User.CanManage(Work);

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken ct)
    {
        var work = await _db.Works.FindAsync(new object[] { id }, ct);
        if (work is null) return NotFound();
        Work = work;
        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id, CancellationToken ct)
    {
        var work = await _db.Works.FindAsync(new object[] { id }, ct);
        if (work is null) return NotFound();
        if (!User.CanManage(work)) return Forbid();

        await _saver.DeleteAsync(work, ct);
        TempData["Message"] = $"Роботу «{work.Title}» видалено.";
        return RedirectToPage("/Index");
    }
}
