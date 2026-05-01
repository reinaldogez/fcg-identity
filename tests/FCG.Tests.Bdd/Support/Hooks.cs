using FCG.Tests.Integration.Fixtures;
using Reqnroll;
using Reqnroll.BoDi;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace FCG.Tests.Bdd.Support;

[Binding]
public class Hooks(IObjectContainer objectContainer)
{
    private static FcgApiFactory _factory = null!;

    [BeforeTestRun]
    public static async Task BeforeTestRun()
    {
        _factory = new FcgApiFactory();
        await ((IAsyncLifetime)_factory).InitializeAsync();
    }

    [AfterTestRun]
    public static async Task AfterTestRun()
    {
        await _factory.DisposeAsync();
    }

    [BeforeScenario]
    public async Task BeforeScenario()
    {
        await _factory.ResetarBancoAsync();

        HttpClient client = _factory.CreateClient(
            new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
            }
        );

        objectContainer.RegisterInstanceAs(client);
        objectContainer.RegisterInstanceAs(new CenarioEstado());
    }
}
