using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace FCG.API.OpenApi;

public class AuthorizeOperationTransformer : IOpenApiOperationTransformer
{
    private const string SchemeId = "Bearer";

    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken
    )
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;

        bool hasAuthorize = metadata.OfType<IAuthorizeData>().Any();
        bool hasAllowAnonymous = metadata.OfType<IAllowAnonymous>().Any();

        if (hasAuthorize && !hasAllowAnonymous)
        {
            operation.Security =
            [
                new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference(SchemeId, context.Document)] = [],
                },
            ];
        }

        return Task.CompletedTask;
    }
}
