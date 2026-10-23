using Microsoft.Extensions.Logging;
using Npgsql;
using Respawn;
using Rise.Persistence;
using Testcontainers.PostgreSql;
using ILogger = Serilog.ILogger;

namespace Rise.Services.Tests;

public sealed class DatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:17")
        .WithPortBinding(5433, 5432)
        .WithDatabase("rise_tests")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private NpgsqlConnection _connection = null!;
    private Respawner _respawner = null!;

    public string ConnectionString => container.GetConnectionString();

    public ApplicationDbContext CreateDbContext(ITestOutputHelper output)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(ConnectionString)
            .LogTo(
                Log.Information,
                [DbLoggerCategory.Database.Command.Name],
                LogLevel.Information)
            .EnableSensitiveDataLogging()
            .EnableDetailedErrors()
            .Options;

        return new ApplicationDbContext(options);
    }
    private ApplicationDbContext CreateDbContext(ILogger logger)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(ConnectionString)
            .LogTo(
                logger.Information,
                [DbLoggerCategory.Database.Command.Name],
                LogLevel.Information)
            .EnableSensitiveDataLogging()
            .EnableDetailedErrors()
            .Options;

        return new ApplicationDbContext(options);
    }

    public async Task InitializeAsync()
    {
        await container.StartAsync();
        await using (var context = CreateDbContext(Log.Logger))
        {
            await context.Database.EnsureCreatedAsync();
        }

        _connection = new NpgsqlConnection(ConnectionString);
        await _connection.OpenAsync();

        _respawner = await Respawner.CreateAsync(_connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"]
        });
    }

    public Task ResetAsync() => _respawner.ResetAsync(_connection);

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
        await container.DisposeAsync();
        await Log.CloseAndFlushAsync();
    }
}