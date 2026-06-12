using Fcg.Identity.Application.UseCases;
using Fcg.Identity.Application.UseCases.Relatorios;
using Microsoft.Extensions.DependencyInjection;

namespace Fcg.Identity.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<CadastrarUsuarioUseCase>();
        services.AddScoped<ObterUsuarioPorIdUseCase>();
        services.AddScoped<ListarUsuariosUseCase>();
        services.AddScoped<AtualizarUsuarioUseCase>();
        services.AddScoped<AlterarSenhaUseCase>();
        services.AddScoped<DesativarUsuarioUseCase>();
        services.AddScoped<AtivarUsuarioUseCase>();
        services.AddScoped<AlterarTipoUsuarioUseCase>();
        services.AddScoped<LoginUseCase>();
        services.AddScoped<RefreshTokenUseCase>();
        services.AddScoped<LogoutUseCase>();
        services.AddScoped<ObterRelatorioUsuariosUseCase>();

        return services;
    }
}
