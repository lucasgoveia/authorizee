﻿using Valtuutus.Core;
using Valtuutus.Core.Configuration;
using Valtuutus.Core.Data;
using Valtuutus.Core.Schemas;
using Valtuutus.Tests.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Valtuutus.Data.Tests.Shared;

public abstract class DataLookupEntityEngineSpecs : BaseLookupEntityEngineSpecs, IAsyncLifetime
{
    
    protected abstract void AddSpecificProvider(IValtuutusDataBuilder builder);
    
    protected IDatabaseFixture _fixture = null!;
    
    private ServiceProvider CreateServiceProvider(Schema? schema = null)
    {
        var builder = new ServiceCollection()
            .AddSingleton(Substitute.For<ILogger<LookupEntityEngine>>())
            .AddValtuutusCore(TestsConsts.Action)
            .AddValtuutusData()
            .AddConcurrentQueryLimit(5);
        
        AddSpecificProvider(builder);
        
        var serviceCollection = builder.Services;
        if (schema != null)
        {
            var serviceDescriptor = serviceCollection.First(descriptor => descriptor.ServiceType == typeof(Schema));
            serviceCollection.Remove(serviceDescriptor);
            serviceCollection.AddSingleton(schema);
        }

        return serviceCollection.BuildServiceProvider();
    }
    
    protected sealed override async ValueTask<LookupEntityEngine> CreateEngine(RelationTuple[] tuples, AttributeTuple[] attributes, Schema? schema = null)
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