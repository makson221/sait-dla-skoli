using System.Security.Claims;
using ArkhivRobit.Models;

namespace ArkhivRobit.Services;

/// <summary>Ролі користувачів. Облікових записів немає: роль визначається кодом, який людина ввела.</summary>
public static class Roles
{
    public const string Admin = "Admin";
    public const string Teacher = "Teacher";
    public const string Viewer = "Viewer";

    public const string TeacherIdClaim = "teacher_id";
    public const string StampClaim = "code_stamp";

    public static bool IsAdmin(this ClaimsPrincipal user) => user.IsInRole(Admin);

    public static bool CanUpload(this ClaimsPrincipal user) => user.IsInRole(Admin) || user.IsInRole(Teacher);

    public static int? TeacherId(this ClaimsPrincipal user) =>
        int.TryParse(user.FindFirstValue(TeacherIdClaim), out var id) ? id : null;

    /// <summary>Редагувати й видаляти роботу може адміністратор або викладач, який її завантажив.</summary>
    public static bool CanManage(this ClaimsPrincipal user, Work work) =>
        user.IsAdmin() || (user.IsInRole(Teacher) && work.UploadedByTeacherId is not null && work.UploadedByTeacherId == user.TeacherId());
}

/// <summary>Політики доступу до сторінок.</summary>
public static class Policies
{
    /// <summary>Перегляд каталогу: усім, якщо архів публічний, інакше лише тим, хто ввів будь-який код.</summary>
    public const string View = "View";

    /// <summary>Завантаження робіт: викладачі та адміністратор.</summary>
    public const string Upload = "Upload";

    /// <summary>Адміністрування: лише адміністратор.</summary>
    public const string Admin = "Admin";
}
