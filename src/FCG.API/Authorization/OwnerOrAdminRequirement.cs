using Microsoft.AspNetCore.Authorization;

namespace FCG.API.Authorization;

public class OwnerOrAdminRequirement(string routeParameterName = "id") : IAuthorizationRequirement
{
    public string RouteParameterName { get; } = routeParameterName;
}
