using System.Security.Claims;
using ArkhivRobit.Models;
using ArkhivRobit.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace ArkhivRobit.Pages.Works;

/// <summary>Додавання нової роботи (для викладачів і адміністратора).</summary>
public class UploadModel : PageModel
{
    private readonly WorkSaver _saver;

    public UploadModel(WorkSaver saver, IOptions<ArchiveOptions> options)
    {
        _saver = saver;
        Options = options.Value;
    }

    public ArchiveOptions Options { get; }

    [BindProperty]
    public WorkInput Input { get; set; } = new();

    public List<string> KnownGroups { get; private set; } = new();

    public WorkFormViewModel Form => new(Input, KnownGroups, Options, ModelState);

    public async Task OnGetAsync(CancellationToken ct)
    {
        KnownGroups = await _saver.KnownGroupsAsync(ct);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (Input.File is null)
            ModelState.AddModelError("Input.File", "Оберіть файл роботи.");
        else if (FileRules.Validate(Input.File.FileName, Input.File.Length, Options) is { } fileError)
            ModelState.AddModelError("Input.File", fileError);

        if (!ModelState.IsValid)
        {
            KnownGroups = await _saver.KnownGroupsAsync(ct);
            return Page();
        }

        var work = new Work
        {
            CreatedAt = DateTime.UtcNow,
            UploadedByTeacherId = User.TeacherId(),
            UploadedByName = User.FindFirstValue(ClaimTypes.Name) ?? "",
        };
        WorkSaver.ApplyInput(work, Input);
        await _saver.SaveAsync(work, Input.File, ct);

        TempData["Message"] = $"Роботу «{work.Title}» додано до архіву.";
        return RedirectToPage("/Works/Details", new { id = work.Id });
    }
}
