using ArkhivRobit.Services;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ArkhivRobit.Pages.Works;

/// <summary>Дані для часткового представлення _WorkFields.</summary>
public record WorkFormViewModel(
    WorkInput Input,
    List<string> KnownGroups,
    ArchiveOptions Options,
    ModelStateDictionary ModelState,
    string? CurrentFileName = null)
{
    public string? Error(string key) =>
        ModelState.TryGetValue(key, out var entry) && entry.Errors.Count > 0 ? entry.Errors[0].ErrorMessage : null;
}
