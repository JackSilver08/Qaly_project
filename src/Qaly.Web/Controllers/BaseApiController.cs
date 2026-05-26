using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace Qaly.Web.Controllers;

[ApiController]
public abstract class BaseApiController : ControllerBase
{
    protected Guid CurrentUserId
    {
        get
        {
            var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, out var userId)
                ? userId
                : throw new UnauthorizedAccessException("Authenticated user id is missing or invalid.");
        }
    }
}
