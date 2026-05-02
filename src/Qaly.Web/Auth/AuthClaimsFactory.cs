using System.Security.Claims;
using Qaly.Application.DTOs.User;

namespace Qaly.Web.Auth;

public static class AuthClaimsFactory
{
    public static ClaimsPrincipal CreatePrincipal(UserDto user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role)
        };

        var identity = new ClaimsIdentity(claims, "QalyCookie");
        return new ClaimsPrincipal(identity);
    }
}
