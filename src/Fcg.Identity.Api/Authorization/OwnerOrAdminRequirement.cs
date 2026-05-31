using Microsoft.AspNetCore.Authorization;

namespace Fcg.Identity.Api.Authorization;

public class OwnerOrAdminRequirement(string routeParameterName = "id") : IAuthorizationRequirement
{
    public string RouteParameterName { get; } = routeParameterName;
}
