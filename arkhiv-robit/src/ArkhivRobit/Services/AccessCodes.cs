using System.Security.Cryptography;
using System.Text;

namespace ArkhivRobit.Services;

/// <summary>
/// Робота з кодами доступу. Коди нечутливі до регістру, пробілів і дефісів:
/// «abcd-efgh», «ABCD EFGH» та «ABCDEFGH» — це той самий код.
/// </summary>
public static class AccessCodes
{
    // Без символів, які легко сплутати: 0/O, 1/I/L.
    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    /// <summary>Генерує випадковий код вигляду «7F3K-92QA».</summary>
    public static string Generate()
    {
        Span<char> chars = stackalloc char[8];
        for (var i = 0; i < chars.Length; i++)
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        return $"{chars[..4]}-{chars[4..]}";
    }

    public static string Normalize(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return "";
        var sb = new StringBuilder(code.Length);
        foreach (var c in code)
            if (!char.IsWhiteSpace(c) && c != '-' && c != '–' && c != '—')
                sb.Append(char.ToUpperInvariant(c));
        return sb.ToString();
    }

    /// <summary>SHA-256 хеш нормалізованого коду (64 шістнадцяткові символи).</summary>
    public static string Hash(string? code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Normalize(code))));

    /// <summary>
    /// Короткий «відбиток» коду, який зберігається в cookie. Якщо код змінять,
    /// відбиток перестане збігатися і всі старі сесії з цим кодом завершаться.
    /// </summary>
    public static string Stamp(string codeHash) => codeHash.Length >= 16 ? codeHash[..16] : codeHash;

    /// <summary>Порівняння за сталий час, щоб не можна було підбирати код за часом відповіді.</summary>
    public static bool Matches(string? input, string? expectedCode)
    {
        if (string.IsNullOrEmpty(Normalize(input)) || string.IsNullOrEmpty(Normalize(expectedCode))) return false;
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(Hash(input)),
            Encoding.ASCII.GetBytes(Hash(expectedCode)));
    }
}
