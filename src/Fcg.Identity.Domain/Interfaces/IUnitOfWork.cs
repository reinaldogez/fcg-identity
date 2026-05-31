namespace Fcg.Identity.Domain.Interfaces;

public interface IUnitOfWork
{
    Task SalvarAlteracoesAsync(CancellationToken cancellationToken = default);
}
