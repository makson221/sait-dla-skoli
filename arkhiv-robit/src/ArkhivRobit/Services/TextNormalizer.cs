using System.Text.RegularExpressions;

namespace ArkhivRobit.Services;

/// <summary>Приведення тексту до єдиного вигляду, щоб пошук знаходив «кн 41», «КН-41» і «кн–41» однаково.</summary>
public static partial class TextNormalizer
{
    /// <summary>Назва групи: великі літери, дефіс замість пробілів і тире. «кн 41» → «КН-41».</summary>
    public static string Group(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var text = value.Trim().ToUpperInvariant().Replace('–', '-').Replace('—', '-');
        text = SpacesAroundDash().Replace(text, "-");
        text = Spaces().Replace(text, "-");
        return MultipleDashes().Replace(text, "-");
    }

    /// <summary>Звичайний текст: обрізані пробіли по краях і одинарні пробіли всередині.</summary>
    public static string Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "" : Spaces().Replace(value.Trim(), " ");

    public static string? CleanOrNull(string? value)
    {
        var text = Clean(value);
        return text.Length == 0 ? null : text;
    }

    /// <summary>Рядок для пошуку: усі поля в нижньому регістрі через пробіл.</summary>
    public static string SearchText(params string?[] parts) =>
        string.Join(' ', parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => Clean(p).ToLowerInvariant()));

    [GeneratedRegex(@"\s*-\s*")]
    private static partial Regex SpacesAroundDash();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    [GeneratedRegex(@"-{2,}")]
    private static partial Regex MultipleDashes();
}
