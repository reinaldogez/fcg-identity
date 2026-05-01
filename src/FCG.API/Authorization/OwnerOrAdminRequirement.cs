using Microsoft.AspNetCore.Authorization;

namespace FCG.API.Authorization;

public class OwnerOrAdminRequirement : IAuthorizationRequirement
{
    public OwnerOrAdminRequirement(string routeParameterName = "id")
    {
        RouteParameterName = routeParameterName;
    }

    public string RouteParameterName { get; }
}
