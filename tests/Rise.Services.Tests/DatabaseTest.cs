using Rise.Persistence;
namespace Rise.Services.Tests;

[Collection("Database")]
public abstract class DatabaseTest(
    DatabaseFixture database,
    ITestOutputHelper output) : IAsyncLifetime
{
    protected ApplicationDbContext CreateDbContext() => database.CreateDbContext(output);

    public async Task InitializeAsync()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.TestOutput(output)
            .CreateLogger();

        await database.ResetAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;
}