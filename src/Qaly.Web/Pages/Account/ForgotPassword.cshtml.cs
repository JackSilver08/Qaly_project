using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Qaly.Web.Pages.Account;

// A recovery help page, not an email/token reset endpoint. Do not report an email
// as sent or alter a password until a verified recovery backend is available.
[AllowAnonymous]
public class ForgotPasswordModel : PageModel
{
    [BindProperty]
    [Required(ErrorMessage = "Vui lòng nhập email của tài khoản cần hỗ trợ.")]
    [EmailAddress(ErrorMessage = "Vui lòng nhập email hợp lệ.")]
    [StringLength(254, ErrorMessage = "Email không được dài quá 254 ký tự.")]
    public string Email { get; set; } = string.Empty;

    public bool RequestPrepared { get; private set; }
    public string RecoveryRequest => $"Xin chào, tôi cần hỗ trợ khôi phục quyền truy cập Qaly cho tài khoản {Email.Trim()}. Vui lòng hướng dẫn tôi xác minh danh tính và đặt lại mật khẩu. Cảm ơn.";

    public void OnGet() { }

    public IActionResult OnPost()
    {
        if (!ModelState.IsValid) return Page();
        RequestPrepared = true;
        return Page();
    }
}
