using System.Security.Claims;
using ArkhivRobit.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ArkhivRobit.Services;

/// <summary>Вхід за кодом доступу та перевірка, чи код досі дійсний.</summary>
public class AccessService
{
    private readonly AppDbContext _db;
    private readonly ArchiveOptions _options;

    public AccessService(AppDbContext db, IOptions<ArchiveOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    /// <summary>Знаходить, кому належить код, і створює відповідного користувача. Null — код невірний.</summary>
    public async Task<ClaimsPrincipal?> SignInWithCodeAsync(string? code)
    {
        if (AccessCodes.Matches(code, _options.AdminCode))
            return CreatePrincipal("Адміністратор", Roles.Admin, AccessCodes.Hash(_options.AdminCode));

        var hash = AccessCodes.Hash(code);
        var teacher = await _db.Teachers.AsNoTracking().FirstOrDefaultAsync(t => t.CodeHash == hash && t.IsActive);
        if (teacher is not null)
            return CreatePrincipal(teacher.Name, Roles.Teacher, teacher.CodeHash, teacher.Id);

        if (!_options.IsPublic && AccessCodes.Matches(code, _options.ViewCode))
            return CreatePrincipal("Гість", Roles.Viewer, AccessCodes.Hash(_options.ViewCode));

        return null;
    }

    /// <summary>
    /// Викликається для кожного запиту з cookie. Якщо адміністратор змінив код або вимкнув
    /// викладача, сесія стає недійсною і людина має увійти знову.
    /// </summary>
    public async Task<bool> IsStillValidAsync(ClaimsPrincipal user)
    {
        var stamp = user.FindFirstValue(Roles.StampClaim);
        if (stamp is null) return false;

        if (user.IsInRole(Roles.Admin))
            return !string.IsNullOrEmpty(AccessCodes.Normalize(_options.AdminCode)) &&
                   stamp == AccessCodes.Stamp(AccessCodes.Hash(_options.AdminCode));

        if (user.IsInRole(Roles.Teacher))
        {
            var id = user.TeacherId();
            var teacher = await _db.Teachers.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
            return teacher is { IsActive: true } && stamp == AccessCodes.Stamp(teacher.CodeHash);
        }

        if (user.IsInRole(Roles.Viewer))
            return _options.IsPublic || stamp == AccessCodes.Stamp(AccessCodes.Hash(_options.ViewCode));

        return false;
    }

    private static ClaimsPrincipal CreatePrincipal(string name, string role, string codeHash, int? teacherId = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, name),
            new(ClaimTypes.Role, role),
            new(Roles.StampClaim, AccessCodes.Stamp(codeHash)),
        };
        if (teacherId is not null) claims.Add(new Claim(Roles.TeacherIdClaim, teacherId.Value.ToString()));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    }

    /// <summary>Подія cookie-автентифікації, що відхиляє застарілі сесії.</summary>
    public static async Task ValidatePrincipalAsync(CookieValidatePrincipalContext context)
    {
        var access = context.HttpContext.RequestServices.GetRequiredService<AccessService>();
        if (context.Principal is null || !await access.IsStillValidAsync(context.Principal))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    }
}
