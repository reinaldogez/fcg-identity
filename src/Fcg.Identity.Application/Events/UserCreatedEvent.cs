namespace Fcg.Identity.Application.Events;

// TEMPORÁRIO: substituir pelo record do pacote de contratos compartilhado quando ele estiver
// publicado. Record puro: SEM [EntityName] e SEM atributos MassTransit — o nome da exchange é
// cravado no bus, mantendo o contrato livre de dependência de transporte.
public record UserCreatedEvent
{
    public int EventVersion { get; init; } = 1;
    public DateTimeOffset OccurredAt { get; init; }
    public Guid UserId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
}
