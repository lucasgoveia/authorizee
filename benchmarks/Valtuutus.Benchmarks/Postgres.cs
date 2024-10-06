using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using System.Data;
using Testcontainers.PostgreSql;
using Valtuutus.Core.Engines.Check;
using Valtuutus.Data.Postgres;

namespace Valtuutus.Benchmarks;

[MemoryDiagnoser]
[JsonExporterAttribute.FullCompressed]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class Postgres
{
    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder()
        .WithUsername("Valtuutus")
        .WithPassword("Valtuutus123")
        .WithDatabase("Valtuutus")
        .WithName($"pg-benchmarks-{Guid.NewGuid()}")
        .Build();

    private ServiceProvider _serviceProvider = null!;
    private ICheckEngine _checkEngine = null!;

    [GlobalSetup]
    public void Setup()
    {
        _dbContainer.StartAsync().GetAwaiter().GetResult();
        IDbConnection DbFactory() => new NpgsqlConnection(_dbContainer.GetConnectionString());
        var assembly = typeof(ValtuutusPostgresOptions).Assembly;
        (_serviceProvider, _checkEngine) = CommonSetup.MigrateAndSeed(
                DbFactory, 
                (sc) => sc.AddPostgres(_ => DbFactory),
                assembly)
            .GetAwaiter()
            .GetResult();
    }

    [Benchmark]
    public async Task<bool> CheckRequest_Simple_Relation_Check()
    {
        return await _checkEngine.Check(new ()
        {
            Permission = "admin",
            EntityType = "organization",
            EntityId = "5171869f-b4e4-ca9a-b800-5e1dab069a26",
            SubjectType = "user",
            SubjectId = "3fca4119-3bda-4370-13cd-a3d317459c73"
            
        }, default);
    }
    
    [Benchmark]
    public async Task<bool> CheckRequest_Complex_Permission_Function_Eval()
    {
        return await _checkEngine.Check(new ()
        {
            Permission = "edit",
            EntityType = "project",
            EntityId = "e4010d7b-cea1-94c6-2232-e1f9ae557272",
            SubjectType = "user",
            SubjectId = "3fca4119-3bda-4370-13cd-a3d317459c73"
        }, default);
    }
    
    [GlobalCleanup]
    public async Task Cleanup()
    {
        await _dbContainer.DisposeAsync();
        await _serviceProvider.DisposeAsync();
    }
    
}