using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FCG.API.Authorization;
using FCG.Domain.Enums;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Moq;

namespace FCG.Tests.Unit.Api.Authorization;

public class OwnerOrAdminHandlerTests
{
    private readonly OwnerOrAdminRequirement _requirement = new();

    [Fact]
    public async Task DevePermitirQuandoUsuarioEAdministrador()
    {
        ClaimsPrincipal principal = CriarPrincipal(
            sub: Guid.NewGuid().ToString(),
            role: TipoUsuario.Administrador.ToString());
        OwnerOrAdminHandler handler = CriarHandler(routeId: Guid.NewGuid().ToString());

        AuthorizationHandlerContext context = new([_requirement], principal, resource: null);

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task DevePermitirQuandoSubDoTokenIgualAoIdDaRota()
    {
        Guid id = Guid.NewGuid();
        ClaimsPrincipal principal = CriarPrincipal(sub: id.ToString(), role: TipoUsuario.Usuario.ToString());
        OwnerOrAdminHandler handler = CriarHandler(routeId: id.ToString());

        AuthorizationHandlerContext context = new([_requirement], principal, resource: null);

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task NaoDevePermitirQuandoSubDiferenteEUsuarioNaoEAdmin()
    {
        ClaimsPrincipal principal = CriarPrincipal(
            sub: Guid.NewGuid().ToString(),
            role: TipoUsuario.Usuario.ToString());
        OwnerOrAdminHandler handler = CriarHandler(routeId: Guid.NewGuid().ToString());

        AuthorizationHandlerContext context = new([_requirement], principal, resource: null);

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task NaoDevePermitirQuandoSubAusente()
    {
        ClaimsPrincipal principal = new(new ClaimsIdentity([new Claim(ClaimTypes.Role, TipoUsuario.Usuario.ToString())], "Bearer"));
        OwnerOrAdminHandler handler = CriarHandler(routeId: Guid.NewGuid().ToString());

        AuthorizationHandlerContext context = new([_requirement], principal, resource: null);

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task NaoDevePermitirQuandoIdDaRotaAusente()
    {
        ClaimsPrincipal principal = CriarPrincipal(
            sub: Guid.NewGuid().ToString(),
            role: TipoUsuario.Usuario.ToString());
        OwnerOrAdminHandler handler = CriarHandler(routeId: null);

        AuthorizationHandlerContext context = new([_requirement], principal, resource: null);

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task NaoDevePermitirQuandoSubInvalido()
    {
        ClaimsPrincipal principal = CriarPrincipal(sub: "nao-eh-guid", role: TipoUsuario.Usuario.ToString());
        OwnerOrAdminHandler handler = CriarHandler(routeId: Guid.NewGuid().ToString());

        AuthorizationHandlerContext context = new([_requirement], principal, resource: null);

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    private static ClaimsPrincipal CriarPrincipal(string sub, string role)
    {
        ClaimsIdentity identity = new(
            [
                new Claim(JwtRegisteredClaimNames.Sub, sub),
                new Claim(ClaimTypes.Role, role)
            ],
            authenticationType: "Bearer",
            nameType: JwtRegisteredClaimNames.Sub,
            roleType: ClaimTypes.Role);

        return new ClaimsPrincipal(identity);
    }

    private static OwnerOrAdminHandler CriarHandler(string? routeId)
    {
        DefaultHttpContext httpContext = new();
        if (routeId is not null)
        {
            httpContext.Request.RouteValues = new RouteValueDictionary { ["id"] = routeId };
        }

        Mock<IHttpContextAccessor> accessorMock = new();
        accessorMock.Setup(a => a.HttpContext).Returns(httpContext);

        return new OwnerOrAdminHandler(accessorMock.Object);
    }
}
