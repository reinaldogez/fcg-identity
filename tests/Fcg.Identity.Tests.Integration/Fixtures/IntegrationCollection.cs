namespace Fcg.Identity.Tests.Integration.Fixtures;

// Uma única IdentityApiFactory (1 SQL Server + 1 RabbitMQ) compartilhada por todas as classes desta
// coleção. Cada classe subir os próprios containers fazia 7 SQL Server pesados rodarem em paralelo e
// estourarem a memória do Docker, deixando o servidor sem resposta (timeouts intermitentes). Os testes
// da coleção rodam em série, então o reset de banco entre eles garante o isolamento.
[CollectionDefinition("Integration")]
public class IntegrationCollection : ICollectionFixture<IdentityApiFactory>;
