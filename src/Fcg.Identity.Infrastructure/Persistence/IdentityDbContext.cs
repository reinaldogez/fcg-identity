using Fcg.Identity.Domain.Entities;
using Fcg.Identity.Domain.Interfaces;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Fcg.Identity.Infrastructure.Persistence;

public class IdentityDbContext(DbContextOptions<IdentityDbContext> options)
    : DbContext(options),
        IUnitOfWork
{
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public async Task SalvarAlteracoesAsync(CancellationToken cancellationToken = default) =>
        await SaveChangesAsync(cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);

        modelBuilder.AddTransactionalOutboxEntities(); // OutboxMessage + OutboxState
        modelBuilder.AddInboxStateEntity(); // InboxState — fica ociosa: este serviço só publica

        base.OnModelCreating(modelBuilder);
    }
}
