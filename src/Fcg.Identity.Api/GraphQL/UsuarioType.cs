using Fcg.Identity.Domain.Entities;
using HotChocolate.Types;

namespace Fcg.Identity.Api.GraphQL;

public class UsuarioType : ObjectType<Usuario>
{
    protected override void Configure(IObjectTypeDescriptor<Usuario> descriptor)
    {
        descriptor.Description("Usuário cadastrado na plataforma FCG.");

        // BindFieldsExplicitly garante que apenas os fields declarados abaixo entrem no schema.
        // Sem isso, .Ignore() entra em conflito com o .Field("email") customizado em HotChocolate v16.
        descriptor.BindFieldsExplicitly();

        descriptor.Field(u => u.Id).Type<NonNullType<IdType>>();
        descriptor.Field(u => u.Nome).Type<NonNullType<StringType>>();
        descriptor.Field(u => u.Tipo).Type<NonNullType<EnumType<Domain.Enums.TipoUsuario>>>();
        descriptor.Field(u => u.DataCriacao).Type<NonNullType<DateTimeType>>();
        descriptor.Field(u => u.Ativo).Type<NonNullType<BooleanType>>();

        // VO Email é achatado em um escalar string. SenhaHash NUNCA é exposta (não é declarada).
        descriptor
            .Field("email")
            .Type<NonNullType<StringType>>()
            .Resolve(ctx => ctx.Parent<Usuario>().Email.Endereco);
    }
}
