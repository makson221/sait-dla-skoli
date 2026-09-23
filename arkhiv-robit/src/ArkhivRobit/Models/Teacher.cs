namespace ArkhivRobit.Models;

/// <summary>
/// Викладач, який може завантажувати роботи. Акаунта з паролем немає:
/// викладач входить за особистим кодом доступу, який видає адміністратор.
/// </summary>
public class Teacher
{
    public int Id { get; set; }

    /// <summary>ПІБ викладача.</summary>
    public string Name { get; set; } = "";

    /// <summary>SHA-256 хеш коду доступу. Сам код у базі не зберігається.</summary>
    public string CodeHash { get; set; } = "";

    /// <summary>Вимкнений викладач не може увійти, а його активні сесії завершуються.</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public List<Work> Works { get; set; } = new();
}
