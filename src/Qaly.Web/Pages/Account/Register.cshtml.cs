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
[IgnoreAntiforgeryToken]
public class RegisterModel : PageModel
{
    private readonly IAuthService _authService;

    public RegisterModel(IAuthService authService)
    {
        _authService = authService;
    }

    [BindProperty]
    public string FullName { get; set; } = string.Empty;

    [BindProperty]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    public string Password { get; set; } = string.Empty;

    [BindProperty]
    public string ConfirmPassword { get; set; } = string.Empty;

    public string? ErrorMessage { get; private set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        try
        {
            var result = await _authService.RegisterAsync(new RegisterDto(FullName, Email, Password, ConfirmPassword), ct);
            if (!result.IsSuccess || result.Data == null)
            {
                ErrorMessage = result.Error ?? "Không thể tạo tài khoản.";
                return Page();
            }

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                AuthClaimsFactory.CreatePrincipal(result.Data));

            TempData["ToastSuccess"] = "Tạo tài khoản thành công";
            return LocalRedirect("/");
        }
        catch
        {
            ErrorMessage = "Không thể tạo tài khoản lúc này. Vui lòng thử lại.";
            return Page();
        }
    }
}
