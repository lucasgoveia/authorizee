﻿using Authorizee.Core;
using Authorizee.Core.Configuration;
using Authorizee.Core.Data;
using Authorizee.Core.Schemas;
using Authorizee.Data.Configuration;
using Authorizee.Tests.Shared;
using IdGen;
using IdGen.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Authorizee.Data.Postgres.Tests;

[Collection("PostgreSqlSpec")]
public sealed class LookupEntityEngineSpecs : BaseLookupEntityEngineSpecs, IAsyncLifetime
{
    private readonly PostgresFixture _fixture;

    public LookupEntityEngineSpecs(PostgresFixture fixture)
    {
        _fixture = fixture;
    }
    
    private ServiceProvider CreateServiceProvider(Schema? schema = null)
    {
        var serviceCollection = new ServiceCollection()
            .AddSingleton(Substitute.For<ILogger<IDataReaderProvider>>())
            .AddSingleton(Substitute.For<ILogger<LookupEntityEngine>>())
            .AddDatabaseSetup(_fixture.DbFactory, o => o.AddPostgres())
            .AddSchemaConfiguration(TestsConsts.Action);
        if (schema != null)
        {
            var serviceDescriptor = serviceCollection.First(descriptor => descriptor.ServiceType == typeof(Schema));
            serviceCollection.Remove(serviceDescriptor);
            serviceCollection.AddSingleton(schema);
        }        
        serviceCollection.Remove(serviceCollection.First(descriptor => descriptor.ServiceType == typeof(IIdGenerator<long>)));
        serviceCollection.AddIdGen(0, () => new IdGeneratorOptions
        {
            TimeSource = new MockAutoIncrementingIntervalTimeSource(1)
        });

        return serviceCollection.BuildServiceProvider();
    }
    
    
    protected override async ValueTask<LookupEntityEngine> CreateEngine(RelationTuple[] tuples, AttributeTuple[] attributes, Schema? schema = null)
    {
        var serviceProvider = CreateServiceProvider(schema);
        var scope = serviceProvider.CreateScope();
        var lookupEntityEngine = scope.ServiceProvider.GetRequiredService<LookupEntityEngine>();
        if(tuples.Length == 0 && attributes.Length == 0) return lookupEntityEngine;
        var dataEngine = scope.ServiceProvider.GetRequiredService<DataEngine>();
        await dataEngine.Write(tuples, attributes, default);
        return lookupEntityEngine;
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