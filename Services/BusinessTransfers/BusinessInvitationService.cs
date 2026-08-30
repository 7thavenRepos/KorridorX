using System.Net;
using KorridorX.Configuration;
using KorridorX.Data;
using KorridorX.Dtos.BusinessTransfers;
using KorridorX.Models.Customers;
using KorridorX.Models.Enums;
using KorridorX.Services.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KorridorX.Services.BusinessTransfers;

public class BusinessInvitationService : IBusinessInvitationService
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);
    private readonly AppDbContext _db;
    private readonly IBusinessAccessService _access;
    private readonly INotificationQueueService _notifications;
    private readonly AccountSecurityOptions _accountOptions;

    public BusinessInvitationService(
        AppDbContext db,
        IBusinessAccessService access,
        INotificationQueueService notifications,
        IOptions<SecurityOptions> securityOptions)
    {
        _db = db;
        _access = access;
        _notifications = notifications;
        _accountOptions = securityOptions.Value.Accounts;
    }

    public async Task<IReadOnlyList<BusinessInvitationDto>> GetInvitationsAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var access = await _access.EnsurePermissionAsync(userId, BusinessPermission.ManageBusinessUsers, ct);
        var invitations = await _db.BusinessInvitations
            .AsNoTracking()
            .Where(x => x.BusinessProfileId == access.BusinessProfileId && !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

        return invitations.Select(ToDto).ToList();
    }

    public async Task<BusinessInvitationDto> CreateAsync(
        Guid userId,
        CreateBusinessInvitationRequestDto request,
        CancellationToken ct = default)
    {
        var access = await _access.EnsurePermissionAsync(userId, BusinessPermission.ManageBusinessUsers, ct);
        ValidateRole(request.Role);
        var email = NormalizeDisplayEmail(request.Email);
        var normalizedEmail = email.ToUpperInvariant();

        var isOwner = await _db.BusinessProfiles.AnyAsync(
            x => x.Id == access.BusinessProfileId && x.OwnerUser.NormalizedEmail == normalizedEmail,
            ct);
        var isMember = await _db.BusinessUsers.AnyAsync(
            x => x.BusinessProfileId == access.BusinessProfileId &&
                 x.User.NormalizedEmail == normalizedEmail &&
                 x.IsActive && !x.IsDeleted,
            ct);
        if (isOwner || isMember)
            throw new InvalidOperationException("This email address already belongs to an active business member.");

        var now = DateTime.UtcNow;
        var pending = await _db.BusinessInvitations
            .Where(x => x.BusinessProfileId == access.BusinessProfileId &&
                        x.NormalizedEmail == normalizedEmail &&
                        x.AcceptedAt == null && x.RevokedAt == null && !x.IsDeleted)
            .ToListAsync(ct);
        foreach (var previous in pending)
        {
            previous.RevokedAt = now;
            previous.RevokedByUserId = userId;
            previous.LastUpdatedAt = now;
            previous.LastUpdatedByUserId = userId;
        }

        var token = BusinessInvitationToken.Create();
        var invitation = new BusinessInvitation
        {
            BusinessProfileId = access.BusinessProfileId,
            Email = email,
            NormalizedEmail = normalizedEmail,
            Role = request.Role,
            Permissions = NormalizePermissions(request.Role, request.Permissions),
            TokenHash = BusinessInvitationToken.Hash(token),
            ExpiresAt = now.Add(Lifetime),
            LastSentAt = now,
            SendCount = 1,
            CreatedByUserId = userId
        };
        _db.BusinessInvitations.Add(invitation);

        var businessName = await _db.BusinessProfiles
            .Where(x => x.Id == access.BusinessProfileId)
            .Select(x => x.BusinessName)
            .FirstAsync(ct);
        await QueueInvitationAsync(invitation, token, businessName, ct);
        await _db.SaveChangesAsync(ct);
        return ToDto(invitation);
    }

    public async Task<BusinessInvitationDto> ResendAsync(
        Guid userId,
        Guid invitationId,
        CancellationToken ct = default)
    {
        var access = await _access.EnsurePermissionAsync(userId, BusinessPermission.ManageBusinessUsers, ct);
        var invitation = await _db.BusinessInvitations
            .Include(x => x.BusinessProfile)
            .FirstOrDefaultAsync(x => x.Id == invitationId &&
                                      x.BusinessProfileId == access.BusinessProfileId &&
                                      !x.IsDeleted,
                ct)
            ?? throw new InvalidOperationException("Business invitation not found.");
        EnsurePending(invitation);

        var now = DateTime.UtcNow;
        var token = BusinessInvitationToken.Create();
        invitation.TokenHash = BusinessInvitationToken.Hash(token);
        invitation.ExpiresAt = now.Add(Lifetime);
        invitation.LastSentAt = now;
        invitation.SendCount += 1;
        invitation.LastUpdatedAt = now;
        invitation.LastUpdatedByUserId = userId;

        await QueueInvitationAsync(invitation, token, invitation.BusinessProfile.BusinessName, ct);
        await _db.SaveChangesAsync(ct);
        return ToDto(invitation);
    }

    public async Task RevokeAsync(Guid userId, Guid invitationId, CancellationToken ct = default)
    {
        var access = await _access.EnsurePermissionAsync(userId, BusinessPermission.ManageBusinessUsers, ct);
        var invitation = await _db.BusinessInvitations.FirstOrDefaultAsync(
            x => x.Id == invitationId && x.BusinessProfileId == access.BusinessProfileId && !x.IsDeleted,
            ct) ?? throw new InvalidOperationException("Business invitation not found.");
        EnsurePending(invitation);

        invitation.RevokedAt = DateTime.UtcNow;
        invitation.RevokedByUserId = userId;
        invitation.LastUpdatedAt = DateTime.UtcNow;
        invitation.LastUpdatedByUserId = userId;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<BusinessInvitationPreviewDto> GetPreviewAsync(
        string token,
        CancellationToken ct = default)
    {
        var invitation = await FindPendingAsync(token, ct);
        return new BusinessInvitationPreviewDto(
            invitation.BusinessProfile.BusinessName,
            invitation.Email,
            invitation.Role,
            invitation.ExpiresAt);
    }

    public async Task ValidateForRegistrationAsync(
        string token,
        string email,
        CancellationToken ct = default)
    {
        var invitation = await FindPendingAsync(token, ct);
        EnsureEmail(invitation, email);
    }

    public async Task AcceptAsync(
        string token,
        Guid userId,
        string email,
        bool saveChanges = true,
        CancellationToken ct = default)
    {
        var invitation = await FindPendingAsync(token, ct);
        EnsureEmail(invitation, email);

        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId, ct)
            ?? throw new InvalidOperationException("The invited account was not found.");
        if (user.UserType != UserType.Business)
            throw new InvalidOperationException("Business invitations can only be accepted by a business web account.");

        var member = await _db.BusinessUsers.FirstOrDefaultAsync(
            x => x.BusinessProfileId == invitation.BusinessProfileId && x.UserId == userId,
            ct);
        if (member is null)
        {
            member = new BusinessUser
            {
                BusinessProfileId = invitation.BusinessProfileId,
                UserId = userId,
                Role = invitation.Role,
                Permissions = invitation.Permissions,
                IsActive = true,
                CreatedByUserId = invitation.CreatedByUserId
            };
            _db.BusinessUsers.Add(member);
        }
        else
        {
            member.IsDeleted = false;
            member.DeletedAt = null;
            member.DeletedByUserId = null;
            member.Role = invitation.Role;
            member.Permissions = invitation.Permissions;
            member.IsActive = true;
            member.LastUpdatedAt = DateTime.UtcNow;
            member.LastUpdatedByUserId = userId;
        }

        invitation.AcceptedAt = DateTime.UtcNow;
        invitation.AcceptedByUserId = userId;
        invitation.LastUpdatedAt = DateTime.UtcNow;
        invitation.LastUpdatedByUserId = userId;
        if (saveChanges)
            await _db.SaveChangesAsync(ct);
    }

    private async Task<BusinessInvitation> FindPendingAsync(string token, CancellationToken ct)
    {
        var hash = BusinessInvitationToken.Hash(token);
        var invitation = await _db.BusinessInvitations
            .Include(x => x.BusinessProfile)
            .FirstOrDefaultAsync(x => x.TokenHash == hash && !x.IsDeleted, ct)
            ?? throw InvalidInvitation();
        EnsurePending(invitation);
        return invitation;
    }

    private async Task QueueInvitationAsync(
        BusinessInvitation invitation,
        string token,
        string businessName,
        CancellationToken ct)
    {
        var url = $"{_accountOptions.FrontendBaseUrl.TrimEnd('/')}/auth/business-invitation?token={Uri.EscapeDataString(token)}";
        var safeBusinessName = WebUtility.HtmlEncode(businessName);
        var body = $"<p>You have been invited to join <strong>{safeBusinessName}</strong> on KorridorX.</p>" +
                   $"<p>This secure invitation expires in 7 days. <a href=\"{WebUtility.HtmlEncode(url)}\">Review and accept the invitation</a>.</p>" +
                   "<p>If you were not expecting this invitation, you can ignore this email.</p>";
        await _notifications.QueueEmailAsync(
            invitation.Email,
            $"Join {businessName} on KorridorX",
            body,
            nameof(BusinessInvitation),
            invitation.Id,
            ct);
    }

    private static void ValidateRole(BusinessUserRole role)
    {
        if (!Enum.IsDefined(typeof(BusinessUserRole), role) || role == BusinessUserRole.Owner)
            throw new InvalidOperationException("A valid non-owner business role is required.");
    }

    private static string NormalizeDisplayEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            throw new InvalidOperationException("A valid invitation email address is required.");
        return email.Trim().ToLowerInvariant();
    }

    private static void EnsureEmail(BusinessInvitation invitation, string email)
    {
        if (!string.Equals(invitation.NormalizedEmail, NormalizeDisplayEmail(email).ToUpperInvariant(), StringComparison.Ordinal))
            throw new InvalidOperationException("This invitation belongs to a different email address.");
    }

    private static void EnsurePending(BusinessInvitation invitation)
    {
        if (invitation.AcceptedAt.HasValue || invitation.RevokedAt.HasValue || invitation.ExpiresAt <= DateTime.UtcNow)
            throw InvalidInvitation();
    }

    private static InvalidOperationException InvalidInvitation() =>
        new("The business invitation is invalid or has expired.");

    private static BusinessPermission NormalizePermissions(BusinessUserRole role, BusinessPermission permissions) =>
        permissions == BusinessPermission.None
            ? BusinessAccessService.DefaultPermissions(role)
            : permissions & BusinessPermission.All;

    private static BusinessInvitationDto ToDto(BusinessInvitation invitation) =>
        new(
            invitation.Id,
            invitation.Email,
            invitation.Role,
            invitation.Permissions,
            invitation.AcceptedAt.HasValue
                ? "Accepted"
                : invitation.RevokedAt.HasValue
                    ? "Revoked"
                    : invitation.ExpiresAt <= DateTime.UtcNow ? "Expired" : "Pending",
            invitation.ExpiresAt,
            invitation.LastSentAt,
            invitation.SendCount,
            invitation.CreatedAt);
}
