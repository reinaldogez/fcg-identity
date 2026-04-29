using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FCG.Domain.Enums;
using Microsoft.AspNetCore.Authorization;

namespace FCG.API.Authorization;

public class OwnerOrAdminHandler(IHttpContextAccessor httpContextAccessor)
    : AuthorizationHandler<OwnerOrAdminRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OwnerOrAdminRequirement requirement)
    {
        if (context.User.IsInRole(TipoUsuario.Administrador.ToString()))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        string? subClaim = context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        string? routeId = httpContextAccessor.HttpContext?
            .GetRouteValue(requirement.RouteParameterName)?.ToString();

        if (subClaim is not null
            && routeId is not null
            && Guid.TryParse(subClaim, out Guid usuarioIdToken)
            && Guid.TryParse(routeId, out Guid usuarioIdRota)
            && usuarioIdToken == usuarioIdRota)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
