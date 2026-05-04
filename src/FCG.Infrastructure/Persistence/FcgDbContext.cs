using FCG.Domain.Entities;
using FCG.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FCG.Infrastructure.Persistence;

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
