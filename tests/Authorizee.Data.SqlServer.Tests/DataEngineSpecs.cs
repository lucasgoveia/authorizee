﻿using Authorizee.Core;
using Authorizee.Core.Configuration;
using Authorizee.Core.Data;
using Authorizee.Data.Configuration;
using Authorizee.Data.Postgres.Tests;
using Authorizee.Tests.Shared;
using Dapper;
using FluentAssertions;
using IdGen;
using IdGen.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sqids;

namespace Authorizee.Data.SqlServer.Tests;


[Collection("SqlServerSpec")]
public sealed class DataEngineSpecs : IAsyncLifetime
{
    private readonly SqlServerFixture _fixture;

    public DataEngineSpecs(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }
    
    private ServiceProvider CreateServiceProvider()
    {
        var serviceCollection = new ServiceCollection()
            .AddSingleton(Substitute.For<ILogger<IDataReaderProvider>>())
            .AddDatabaseSetup(_fixture.DbFactory, o => o.AddSqlServer())
            .AddSchemaConfiguration(TestsConsts.Action);

        serviceCollection.Remove(serviceCollection.First(descriptor => descriptor.ServiceType == typeof(IIdGenerator<long>)));
        serviceCollection.AddIdGen(0, () => new IdGeneratorOptions
        {
            TimeSource = new MockAutoIncrementingIntervalTimeSource(1)
        });

        return serviceCollection.BuildServiceProvider();
    }
    
    [Fact]
    public async Task WritingData_ShouldAssociateRelationWithTransactionId()
    {
        // arrange
        var provider = CreateServiceProvider();
        
        // act
        var dataEngine = provider.GetRequiredService<DataEngine>();
        var snapToken = await dataEngine.Write([new RelationTuple("project", "1", "member", "user", "1")], [], default);
        var decoder = provider.GetRequiredService<SqidsEncoder<long>>();
        var transactionId = decoder.Decode(snapToken.Value).Single();

        // assert
        using var db = _fixture.DbFactory();
        var relationCount = await db.ExecuteScalarAsync<bool>("SELECT (SELECT COUNT(*) FROM relation_tuples WHERE created_tx_id = @id)", 
            new { id = transactionId });
        var exists = await db.ExecuteScalarAsync<bool>("""
                                                          SELECT
                                                          CASE
                                                              WHEN EXISTS(SELECT 1 FROM transactions WHERE id = @id)
                                                                   THEN 1
                                                              ELSE 0
                                                          END
                                                       """, 
            new { id = transactionId });
        
        relationCount.Should().BeTrue();
        
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task Writing_empty_data_should_throw()
    {
        // arrange
        var provider = CreateServiceProvider();
        
        // act
        var dataEngine = provider.GetRequiredService<DataEngine>();
        Func<Task> act = async () => await dataEngine.Write(Array.Empty<RelationTuple>(), Array.Empty<AttributeTuple>(), default);

        // assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }
    
    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _fixture.ResetDatabaseAsync();
    }
}