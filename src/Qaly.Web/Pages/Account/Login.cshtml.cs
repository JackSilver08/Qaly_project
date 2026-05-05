using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Qaly.Application.DTOs.User;
using Qaly.Application.Services;
using Qaly.Web.Auth;

namespace Qaly.Web.Pages.Account;

[AllowAnonymous]
public class LoginModel : PageModel
{
    private readonly IAuthService _authService;

    public LoginModel(IAuthService authService)
    {
        _authService = authService;
    }

    [BindProperty]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    public string Password { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string? ErrorMessage { get; private set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        var result = await _authService.LoginAsync(new LoginDto(Email, Password), ct);
        if (!result.IsSuccess || result.Data == null)
        {
            ErrorMessage = result.Error ?? "Unable to sign in.";
            return Page();
        }

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            AuthClaimsFactory.CreatePrincipal(result.Data));

        return LocalRedirect(SafeReturnUrl(ReturnUrl));
    }

    private string SafeReturnUrl(string? returnUrl)
        => Url.IsLocalUrl(returnUrl) ? returnUrl! : "/";
}
