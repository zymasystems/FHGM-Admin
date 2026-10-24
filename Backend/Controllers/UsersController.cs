using FHGM.Backend.Authentication;
using FHGM.Backend.DTOs;
using FHGM.Backend.Data;
using FHGM.Backend.Entities;
using FHGM.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FHGM.Backend.Controllers;

[ApiController, Route("api/users"), Authorize(Policy = Permissions.ManageUsers)]
public sealed class UsersController(AppDbContext db, UserManager<AppUser> users, AuditService audit) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(int page = 1)
    {
        if (page < 1 || page > 10000) return BadRequest();
        var items = await users.Users.AsNoTracking().OrderBy(x => x.Email).Skip((page - 1) * 25).Take(25).ToListAsync();
        var result = new List<object>();
        foreach (var user in items) result.Add(new { user.Id, user.Email, user.DisplayName, user.IsActive, roles = await users.GetRolesAsync(user) });
        return Ok(new { items = result, total = await users.Users.CountAsync(), page });
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateUserRequest request)
    {
        if (!Roles.All.Contains(request.Role)) return BadRequest(new { message = "Choose a supported role." });
        await using var transaction = await db.Database.BeginTransactionAsync();
        var user = new AppUser { UserName = request.Email, Email = request.Email, DisplayName = request.DisplayName.Trim(), EmailConfirmed = true };
        var result = await users.CreateAsync(user, request.Password);
        if (!result.Succeeded) return BadRequest(new { message = string.Join(" ", result.Errors.Select(x => x.Description)) });
        result = await users.AddToRoleAsync(user, request.Role);
        if (!result.Succeeded) return BadRequest(new { message = "Could not assign that role." });
        audit.Record("UserCreated", "User", user.Id);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return StatusCode(201, new { user.Id, user.Email, user.DisplayName, role = request.Role });
    }

    [HttpPut("{id}/active")]
    public async Task<IActionResult> SetActive(string id, ActiveRequest request)
    {
        if (id == users.GetUserId(User)) return BadRequest(new { message = "You cannot disable your own account." });
        var user = await users.FindByIdAsync(id);
        if (user is null) return NotFound();
        // Keep super administrator lifecycle out of this initial UI to avoid accidental lockout.
        if (await users.IsInRoleAsync(user, Roles.SuperAdmin)) return BadRequest(new { message = "Super administrator accounts cannot be disabled here." });
        await using var transaction = await db.Database.BeginTransactionAsync();
        user.IsActive = request.IsActive;
        var result = await users.UpdateAsync(user);
        if (!result.Succeeded) return Conflict();
        result = await users.UpdateSecurityStampAsync(user);
        if (!result.Succeeded) return Conflict();
        audit.Record(request.IsActive ? "UserEnabled" : "UserDisabled", "User", id);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return NoContent();
    }
}
