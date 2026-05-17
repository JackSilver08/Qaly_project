using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;
using System.Security.Claims;

namespace Qaly.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/projects/{projectId:guid}/wiki")]
public class WikiController : ControllerBase
{
    private readonly QalyDbContext _context;

    public WikiController(QalyDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetPages(Guid projectId)
    {
        var pages = await _context.WikiPages
            .Where(p => p.ProjectId == projectId)
            .Include(p => p.Author)
            .OrderByDescending(p => p.UpdatedAt)
            .Select(p => new WikiPageDto
            {
                Id = p.Id,
                Title = p.Title,
                Content = p.Content,
                AuthorName = p.Author.FullName,
                UpdatedAt = p.UpdatedAt
            })
            .ToListAsync();

        return Ok(pages);
    }

    [HttpPost]
    public async Task<IActionResult> CreatePage(Guid projectId, [FromBody] CreateWikiPageRequest request)
    {
        if (request == null)
        {
            return BadRequest(new { error = "Wiki request payload is required." });
        }

        if (!await CanEditWiki(projectId)) return Forbid();

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return BadRequest(new { error = "Wiki title is required." });
        }

        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdStr, out var userId)) return Unauthorized();

        var page = new WikiPage
        {
            ProjectId = projectId,
            Title = request.Title,
            Content = request.Content ?? string.Empty,
            AuthorId = userId,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        try
        {
            _context.WikiPages.Add(page);
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return BadRequest(new { error = "Unable to save wiki page. Please verify the project access and input values." });
        }
        catch (InvalidOperationException)
        {
            return BadRequest(new { error = "Unable to create wiki page because the current project state is invalid." });
        }

        return Ok(new WikiPageDto
        {
            Id = page.Id,
            Title = page.Title,
            Content = page.Content,
            AuthorName = (await _context.Users.FindAsync(userId))?.FullName ?? "Unknown",
            UpdatedAt = page.UpdatedAt
        });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdatePage(Guid projectId, Guid id, [FromBody] CreateWikiPageRequest request)
    {
        if (request == null)
        {
            return BadRequest(new { error = "Wiki request payload is required." });
        }

        if (!await CanEditWiki(projectId)) return Forbid();

        var page = await _context.WikiPages.FindAsync(id);
        if (page == null || page.ProjectId != projectId) return NotFound();

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return BadRequest(new { error = "Wiki title is required." });
        }

        page.Title = request.Title;
        page.Content = request.Content ?? string.Empty;
        page.UpdatedAt = DateTimeOffset.UtcNow;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return BadRequest(new { error = "Unable to save wiki page changes. Please verify the project access and input values." });
        }
        return Ok();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeletePage(Guid projectId, Guid id)
    {
        if (!await CanManageWiki(projectId)) return Forbid();

        var page = await _context.WikiPages.FindAsync(id);
        if (page == null || page.ProjectId != projectId) return NotFound();

        _context.WikiPages.Remove(page);
        await _context.SaveChangesAsync();
        return Ok();
    }

    private async Task<bool> CanManageWiki(Guid projectId)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdStr, out var userId)) return false;

        var user = await _context.Users.FindAsync(userId);
        if (user?.Role == "Admin") return true;

        var isOwner = await _context.Projects.AnyAsync(p => p.Id == projectId && p.OwnerId == userId);
        if (isOwner) return true;

        var member = await _context.ProjectMembers
            .FirstOrDefaultAsync(m => m.ProjectId == projectId && m.UserId == userId);
        
        return member != null && (member.Role == "Owner" || member.Role == "Manager" || member.Role == "Admin");
    }

    private async Task<bool> CanEditWiki(Guid projectId)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdStr, out var userId)) return false;

        var user = await _context.Users.FindAsync(userId);
        if (user?.Role == "Admin") return true;

        var isOwner = await _context.Projects.AnyAsync(p => p.Id == projectId && p.OwnerId == userId);
        if (isOwner) return true;

        var member = await _context.ProjectMembers
            .FirstOrDefaultAsync(m => m.ProjectId == projectId && m.UserId == userId);

        return member != null && member.Role != "Viewer";
    }
}

public class WikiPageDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; }
}

public class CreateWikiPageRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Content { get; set; }
}
