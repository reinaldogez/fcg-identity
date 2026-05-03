using FCG.API.GraphQL.Authorization;
using FCG.API.GraphQL.Errors;
using HotChocolate.Execution.Configuration;
using Microsoft.AspNetCore.Authorization;

namespace FCG.API.GraphQL;

public static class GraphQLConfiguration
{
    public static IRequestExecutorBuilder AddFcgGraphQL(this IServiceCollection services)
    {
        // Handler GraphQL coexiste com o REST OwnerOrAdminHandler na mesma policy:
        // o ASP.NET Core executa todos os handlers e considera autorizado se algum tiver Succeed.
        services.AddSingleton<IAuthorizationHandler, OwnerOrAdminGraphQLHandler>();

        return services
            .AddGraphQLServer()
            .AddAuthorization()
            .AddFiltering()
            .AddSorting()
            .AddProjections()
            .AddQueryType(d => d.Name("Query"))
            .AddTypeExtension<UsuarioQueries>()
            .AddType<UsuarioType>()
            .AddErrorFilter<DomainErrorFilter>();
    }
}
