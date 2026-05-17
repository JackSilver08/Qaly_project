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
public partial class LoginModel : PageModel
{
    private readonly IAuthService _authService;
    private readonly ILogger<LoginModel> _logger;

    public LoginModel(IAuthService authService, ILogger<LoginModel> logger)
    {
        _authService = authService;
        _logger = logger;
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
        try
        {
            var result = await _authService.LoginAsync(new LoginDto(Email, Password), ct);
            if (!result.IsSuccess || result.Data == null)
            {
                ErrorMessage = result.Error ?? "Không thể đăng nhập.";
                return Page();
            }

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                AuthClaimsFactory.CreatePrincipal(result.Data));

            TempData["ToastSuccess"] = "Đăng nhập thành công";
            return LocalRedirect(SafeReturnUrl(ReturnUrl));
        }
        catch (Exception ex)
        {
            LogLoginFailed(_logger, ex, Email);
            ErrorMessage = "Không thể đăng nhập lúc này. Vui lòng thử lại.";
            return Page();
        }
    }

    private string SafeReturnUrl(string? returnUrl)
        => Url.IsLocalUrl(returnUrl) ? returnUrl! : "/";

    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "Login failed unexpectedly for {Email}.")]
    private static partial void LogLoginFailed(ILogger logger, Exception exception, string email);
}
