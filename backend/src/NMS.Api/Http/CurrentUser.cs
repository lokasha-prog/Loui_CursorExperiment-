using System.Security.Claims;
using NMS.Application.Abstractions;
using NMS.Application.Security;

namespace NMS.Api.Http;

/// <summary>Reads the signed-in person from the validated JWT. Permissions come from the role matrix in code.</summary>
public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private IReadOnlySet<string>? _permissions;

    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid UserId =>
        Guid.TryParse(Principal?.FindFirstValue("sub"), out var id) ? id : Guid.Empty;

    public string? Email => Principal?.FindFirstValue("email");

    public IReadOnlyCollection<string> Roles =>
        Principal?.FindAll("role").Select(c => c.Value).ToList() ?? [];

    public IReadOnlySet<string> Permissions => _permissions ??= RolePermissions.ForRoles(Roles);

    public string? IpAddress => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public string? UserAgent => accessor.HttpContext?.Request.Headers.UserAgent.ToString();

    public string? RequestId => accessor.HttpContext?.TraceIdentifier;

    public bool IsInRole(string role) => Roles.Contains(role);

    public bool HasPermission(string permission) => Permissions.Contains(permission);
}
