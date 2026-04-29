using Microsoft.AspNetCore.Authorization;

namespace FCG.API.Authorization;

public class OwnerOrAdminRequirement : IAuthorizationRequirement
{
    public string RouteParameterName { get; }

    public OwnerOrAdminRequirement(string routeParameterName = "id")
    {
        RouteParameterName = routeParameterName;
    }
}
