using ArkhivRobit.Models;

namespace ArkhivRobit.Services;

/// <summary>Параметри пошуку в каталозі.</summary>
public class WorkFilter
{
    public string? Group { get; set; }
    public int? Year { get; set; }
    public WorkType? Type { get; set; }
    public string? Query { get; set; }

    public bool IsEmpty => string.IsNullOrWhiteSpace(Group) && Year is null && Type is null && string.IsNullOrWhiteSpace(Query);

    /// <summary>Застосовує фільтри до запиту до бази даних.</summary>
    public IQueryable<Work> Apply(IQueryable<Work> works)
    {
        var group = TextNormalizer.Group(Group);
        if (group.Length > 0) works = works.Where(w => w.GroupName == group);

        if (Year is int year) works = works.Where(w => w.Year == year);

        if (Type is WorkType type) works = works.Where(w => w.Type == type);

        // Кожне слово запиту має зустрічатися в темі, ПІБ студента, керівнику або групі.
        foreach (var word in TextNormalizer.SearchText(Query).Split(' ', StringSplitOptions.RemoveEmptyEntries))
            works = works.Where(w => w.SearchText.Contains(word));

        return works;
    }
}
