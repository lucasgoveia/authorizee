﻿using Authorizee.Core;
using Authorizee.Core.Configuration;
using Authorizee.Core.Schemas;
using Authorizee.Data.Configuration;
using Authorizee.Tests.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Authorizee.Data.SqlServer.Tests;

[Collection("SqlServerSpec")]
public sealed class LookupSubjectEngineSpecs : BaseLookupSubjectEngineSpecs, IAsyncLifetime
{
    private readonly SqlServerFixture _fixture;

    public LookupSubjectEngineSpecs(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }
    
    private ServiceProvider CreateServiceProvider(Schema? schema = null)
    {
        var serviceCollection = new ServiceCollection()
            .AddSingleton(Substitute.For<ILogger<SqlServerAttributeReader>>())
            .AddSingleton(Substitute.For<ILogger<SqlServerRelationTupleReader>>())
            .AddSingleton(Substitute.For<ILogger<LookupSubjectEngine>>())
            .AddDatabaseSetup(_fixture.DbFactory, o => o.AddSqlServer())
            .AddSchemaConfiguration(TestsConsts.Action);
        if (schema != null)
        {
            var serviceDescriptor = serviceCollection.First(descriptor => descriptor.ServiceType == typeof(Schema));
            serviceCollection.Remove(serviceDescriptor);
            serviceCollection.AddSingleton(schema);
        }

        return serviceCollection.BuildServiceProvider();
    }
    
    
    protected override async ValueTask<LookupSubjectEngine> CreateEngine(RelationTuple[] tuples, AttributeTuple[] attributes, Schema? schema = null)
    {
        var serviceProvider = CreateServiceProvider(schema);
        await Task.WhenAll(_fixture.DbFactory.InsertRelations(tuples), _fixture.DbFactory.InsertAttributes(attributes));
        var checkEngine = serviceProvider.CreateScope().ServiceProvider.GetRequiredService<LookupSubjectEngine>();
        return checkEngine;
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