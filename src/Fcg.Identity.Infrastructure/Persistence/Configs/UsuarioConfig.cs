using Fcg.Identity.Domain.Entities;
using Fcg.Identity.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fcg.Identity.Infrastructure.Persistence.Configs;

public class UsuarioConfig : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("Usuarios");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Nome).IsRequired().HasMaxLength(Usuario.NomeTamanhoMaximo);

        builder
            .Property(u => u.Email)
            .HasConversion(email => email.Endereco, endereco => Email.Reconstituir(endereco))
            .IsRequired()
            .HasMaxLength(256)
            .UseCollation("SQL_Latin1_General_CP1_CI_AS");

        builder.HasIndex(u => u.Email).IsUnique();

        builder
            .Property(u => u.SenhaHash)
            .HasConversion(hash => hash.Valor, valor => SenhaHash.Reconstituir(valor))
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(u => u.Tipo).IsRequired();

        builder.Property(u => u.DataCriacao).IsRequired();

        builder.Property(u => u.Ativo).IsRequired().HasDefaultValue(true);
    }
}
