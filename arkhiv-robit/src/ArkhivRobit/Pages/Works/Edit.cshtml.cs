using ArkhivRobit.Data;
using ArkhivRobit.Models;
using ArkhivRobit.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace ArkhivRobit.Pages.Works;

/// <summary>Редагування роботи: доступне адміністратору та викладачу, який її завантажив.</summary>
public class EditModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly WorkSaver _saver;

    public EditModel(AppDbContext db, WorkSaver saver, IOptions<ArchiveOptions> options)
    {
        _db = db;
        _saver = saver;
        Options = options.Value;
    }

    public ArchiveOptions Options { get; }

    [BindProperty]
    public WorkInput Input { get; set; } = new();

    public Work Work { get; private set; } = null!;

    public List<string> KnownGroups { get; private set; } = new();

    public WorkFormViewModel Form => new(Input, KnownGroups, Options, ModelState, Work.FileName);

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken ct)
    {
        if (await LoadAsync(id, ct) is { } denied) return denied;
        Input = WorkInput.From(Work);
        KnownGroups = await _saver.KnownGroupsAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id, CancellationToken ct)
    {
        if (await LoadAsync(id, ct) is { } denied) return denied;

        if (Input.File is not null && FileRules.Validate(Input.File.FileName, Input.File.Length, Options) is { } fileError)
            ModelState.AddModelError("Input.File", fileError);

        if (!ModelState.IsValid)
        {
            KnownGroups = await _saver.KnownGroupsAsync(ct);
            return Page();
        }

        WorkSaver.ApplyInput(Work, Input);
        await _saver.SaveAsync(Work, Input.File, ct);

        TempData["Message"] = "Зміни збережено.";
        return RedirectToPage("/Works/Details", new { id });
    }

    private async Task<IActionResult?> LoadAsync(int id, CancellationToken ct)
    {
        var work = await _db.Works.FindAsync(new object[] { id }, ct);
        if (work is null) return NotFound();
        if (!User.CanManage(work)) return Forbid();
        Work = work;
        return null;
    }
}
