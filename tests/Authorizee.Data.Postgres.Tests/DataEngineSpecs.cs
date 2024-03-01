﻿using Authorizee.Core;
using Authorizee.Core.Data;
using Authorizee.Data.Tests.Shared;
using Dapper;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Sqids;

namespace Authorizee.Data.Postgres.Tests;


[Collection("PostgreSqlSpec")]
public class DataEngineSpecs : DataSpecificDataEngineSpecs
{
    protected override void AddSpecificProvider(IServiceCollection services)
    {
        services.AddPostgres();
    }
    public DataEngineSpecs(PostgresFixture fixture)
    {
        _fixture = fixture;
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
        var relationCount = await db.ExecuteScalarAsync<bool>("SELECT (SELECT COUNT(*) FROM public.relation_tuples WHERE created_tx_id = @id) = 1", 
            new { id = transactionId });
        
        relationCount.Should().BeTrue();
        
        var exists = await db.ExecuteScalarAsync<bool>("SELECT EXISTS(SELECT 1 FROM public.transactions WHERE id = @id)", 
            new { id = transactionId });
        
        exists.Should().BeTrue();
    }
    
    [Fact]
    public async Task DeletingData_ShouldReturnTransactionId()
    {
        // arrange
        var provider = CreateServiceProvider();
        var dataEngine = provider.GetRequiredService<DataEngine>();
        
        // act
        var decoder = provider.GetRequiredService<SqidsEncoder<long>>();
        var newSnapToken = await dataEngine.Delete(new DeleteFilter
        {
            Relations = new[] { new DeleteRelationsFilter
            {
                EntityType = "project",
                EntityId = "1",
                Relation = "member",
                SubjectType = "user",
                SubjectId = "1"
            
            } }
        }, default);
        
        
        // assert
        using var db = _fixture.DbFactory();
        
        var newTransactionId = decoder.Decode(newSnapToken.Value).Single();
        // new transaction should exist
        var newTransaction = await db.ExecuteScalarAsync<bool>("SELECT EXISTS(SELECT 1 FROM public.transactions WHERE id = @id)", 
            new { id = newTransactionId });
        
        newTransaction.Should().BeTrue();
    }

}