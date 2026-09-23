using System.ComponentModel.DataAnnotations;
using ArkhivRobit.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace ArkhivRobit.Pages;

[EnableRateLimiting("login")]
public class LoginModel : PageModel
{
    private readonly AccessService _access;
    private readonly ArchiveOptions _options;

    public LoginModel(AccessService access, IOptions<ArchiveOptions> options)
    {
        _access = access;
        _options = options.Value;
    }

    [BindProperty]
    [Required(ErrorMessage = "Введіть код доступу.")]
    [Display(Name = "Код доступу")]
    public string Code { get; set; } = "";

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public bool ArchiveIsPublic => _options.IsPublic;

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var principal = await _access.SignInWithCodeAsync(Code);
        if (principal is null)
        {
            ModelState.AddModelError(nameof(Code), "Невірний код доступу.");
            return Page();
        }

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
            new AuthenticationProperties { IsPersistent = true });

        TempData["Message"] = $"Вітаємо, {principal.Identity!.Name}!";
        return Url.IsLocalUrl(ReturnUrl) ? LocalRedirect(ReturnUrl) : RedirectToPage("/Index");
    }
}
