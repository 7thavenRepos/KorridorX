using System.Security.Claims;
using KorridorX.Configuration;
using KorridorX.Data.Seed;
using KorridorX.Dtos.AdminUsers;
using KorridorX.Infrastructure;
using KorridorX.Models.Enums;
using KorridorX.Services.AdminUsers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace KorridorX.Controllers;

[Authorize(Roles = "Admin,SuperAdmin")]
[ApiController]
[Route("api/admin/users")]
[EnableRateLimiting(SecurityRateLimitPolicies.Sensitive)]
public sealed class AdminUsersController : ControllerBase
{
    private readonly IAdminUserManagementService _service;

    public AdminUsersController(IAdminUserManagementService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetUsers(
        [FromQuery] string? search = null,
        [FromQuery] UserStatus? status = null,
        [FromQuery] UserType? userType = null,
        [FromQuery] string? role = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _service.GetUsersAsync(
            search,
            status,
            userType,
            role,
            page,
            pageSize,
            ct);

        return Ok(ApiResponses.OkPaged(
            result.Items,
            result.Meta,
            "Users retrieved successfully."));
    }

    [HttpGet("{userId:guid}")]
    public async Task<IActionResult> GetUser(
        Guid userId,
        CancellationToken ct = default)
    {
        var result = await _service.GetUserAsync(userId, ct);
        return Ok(ApiResponses.Ok(result, "User retrieved successfully."));
    }

    [HttpGet("roles")]
    public IActionResult GetAssignableRoles()
    {
        var result = _service.GetAssignableRoles();
        return Ok(ApiResponses.Ok(result, "Assignable administrative roles retrieved successfully."));
    }

    [Authorize(Roles = IdentityRoleNames.SuperAdmin)]
    [HttpPost]
    public async Task<IActionResult> CreateInternalUser(
        [FromBody] CreateAdminUserRequestDto request,
        CancellationToken ct)
    {
        var result = await _service.CreateInternalUserAsync(
            request,
            GetUserId(),
            ct);

        return Ok(ApiResponses.Ok(result, "Internal user created successfully."));
    }

    [Authorize(Roles = IdentityRoleNames.SuperAdmin)]
    [HttpPut("{userId:guid}/status")]
    public async Task<IActionResult> UpdateStatus(
        Guid userId,
        [FromBody] UpdateAdminUserStatusRequestDto request,
        CancellationToken ct)
    {
        var result = await _service.UpdateStatusAsync(
            userId,
            request,
            GetUserId(),
            ct);

        return Ok(ApiResponses.Ok(result, "User status updated successfully."));
    }

    [Authorize(Roles = IdentityRoleNames.SuperAdmin)]
    [HttpPut("{userId:guid}/roles")]
    public async Task<IActionResult> UpdateRoles(
        Guid userId,
        [FromBody] UpdateAdminUserRolesRequestDto request,
        CancellationToken ct)
    {
        var result = await _service.UpdateRolesAsync(
            userId,
            request,
            GetUserId(),
            ct);

        return Ok(ApiResponses.Ok(result, "User roles updated successfully."));
    }

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(value, out var userId)
            ? userId
            : throw new UnauthorizedAccessException("Invalid authenticated user.");
    }
}