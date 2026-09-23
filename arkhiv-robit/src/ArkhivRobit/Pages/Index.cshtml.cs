using ArkhivRobit.Data;
using ArkhivRobit.Models;
using ArkhivRobit.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ArkhivRobit.Pages;

/// <summary>Головна сторінка: пошук робіт за групою, роком, типом і текстом.</summary>
public class IndexModel : PageModel
{
    public const int PageSize = 20;

    private readonly AppDbContext _db;

    public IndexModel(AppDbContext db) => _db = db;

    [BindProperty(SupportsGet = true, Name = "group")]
    public string? Group { get; set; }

    [BindProperty(SupportsGet = true, Name = "year")]
    public int? Year { get; set; }

    [BindProperty(SupportsGet = true, Name = "type")]
    public WorkType? Type { get; set; }

    [BindProperty(SupportsGet = true, Name = "q")]
    public string? Query { get; set; }

    [BindProperty(SupportsGet = true, Name = "page")]
    public int PageNumber { get; set; } = 1;

    public List<Work> Works { get; private set; } = new();
    public int TotalCount { get; private set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);

    public List<int> Years { get; private set; } = new();
    public List<string> Groups { get; private set; } = new();

    /// <summary>Швидкий вибір: рік → групи з кількістю робіт.</summary>
    public List<YearGroups> Overview { get; private set; } = new();

    public int ArchiveTotal { get; private set; }

    public bool HasFilter => !Filter.IsEmpty;

    private WorkFilter Filter => new() { Group = Group, Year = Year, Type = Type, Query = Query };

    public async Task OnGetAsync()
    {
        PageNumber = Math.Max(1, PageNumber);

        Years = await _db.Works.Select(w => w.Year).Distinct().OrderByDescending(y => y).ToListAsync();

        var groupsQuery = _db.Works.AsQueryable();
        if (Year is int year) groupsQuery = groupsQuery.Where(w => w.Year == year);
        Groups = await groupsQuery.Select(w => w.GroupName).Distinct().OrderBy(g => g).ToListAsync();

        ArchiveTotal = await _db.Works.CountAsync();

        if (!HasFilter)
        {
            var counts = await _db.Works
                .GroupBy(w => new { w.Year, w.GroupName })
                .Select(g => new { g.Key.Year, g.Key.GroupName, Count = g.Count() })
                .ToListAsync();
            Overview = counts
                .GroupBy(c => c.Year)
                .OrderByDescending(g => g.Key)
                .Select(g => new YearGroups(g.Key, g.OrderBy(c => c.GroupName).Select(c => (c.GroupName, c.Count)).ToList()))
                .ToList();
        }

        var works = Filter.Apply(_db.Works.AsNoTracking());
        TotalCount = await works.CountAsync();
        Works = await works
            .OrderByDescending(w => w.Year).ThenBy(w => w.GroupName).ThenBy(w => w.StudentName)
            .Skip((PageNumber - 1) * PageSize).Take(PageSize)
            .ToListAsync();
    }

    /// <summary>Посилання на іншу сторінку результатів із тими самими фільтрами.</summary>
    public Dictionary<string, string?> RouteFor(int page) => new()
    {
        ["group"] = Group,
        ["year"] = Year?.ToString(),
        ["type"] = Type?.ToString(),
        ["q"] = Query,
        ["page"] = page.ToString(),
    };

    public record YearGroups(int Year, List<(string Group, int Count)> Groups);
}
