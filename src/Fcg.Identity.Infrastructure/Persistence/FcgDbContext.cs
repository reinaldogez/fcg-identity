using Fcg.Identity.Domain.Entities;
using Fcg.Identity.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Fcg.Identity.Infrastructure.Persistence;

public class FcgDbContext(DbContextOptions<FcgDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public async Task SalvarAlteracoesAsync(CancellationToken cancellationToken = default) =>
        await SaveChangesAsync(cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FcgDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
