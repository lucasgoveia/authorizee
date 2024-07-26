﻿using System.Diagnostics;
using System.Text.Json;
using Valtuutus.Core;
using Valtuutus.Core.Data;
using Valtuutus.Core.Observability;
using Valtuutus.Data.Postgres.Utils;
using Dapper;
using Microsoft.Extensions.Logging;

namespace Valtuutus.Data.Postgres;

public sealed class PostgresDataReaderProvider : RateLimiterExecuter, IDataReaderProvider
{
    private readonly DbConnectionFactory _connectionFactory;
    private readonly ILogger<IDataReaderProvider> _logger;

    public PostgresDataReaderProvider(DbConnectionFactory connectionFactory, ILogger<IDataReaderProvider> logger,
        ValtuutusDataOptions options) : base(options)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<List<RelationTuple>> GetRelations(RelationTupleFilter tupleFilter, CancellationToken cancellationToken)
    {
        using var activity = DefaultActivitySource.Instance.StartActivity();

        return await ExecuteWithRateLimit(async (ct) =>
        {
            using var connection = _connectionFactory();

            var queryTemplate = new SqlBuilder()
                .FilterRelations(tupleFilter)
                .AddTemplate(@"SELECT 
                    entity_type,
                    entity_id,
                    relation,
                    subject_type,
                    subject_id, 
                    subject_relation 
                FROM relation_tuples /**where**/");

#if DEBUG
            _logger.LogDebug("Querying relations tuples with filter: {filter}", JsonSerializer.Serialize(tupleFilter));
            var start = Stopwatch.GetTimestamp();
#endif
            var res = (await connection.QueryAsync<RelationTuple>(new CommandDefinition(queryTemplate.RawSql,
                    queryTemplate.Parameters, cancellationToken: ct)))
                .ToList();
#if DEBUG
            _logger.LogDebug("Queried relations in {QueryDuration}ms, returned {QueryItemCount} items",
                Stopwatch.GetElapsedTime(start).TotalMilliseconds, res.Count);
#endif
            return res;
        }, cancellationToken);
    }
    
    public async Task<List<RelationTuple>> GetRelationsWithEntityIds(EntityRelationFilter entityRelationFilter, string subjectType, IEnumerable<string> entityIds, string? subjectRelation, CancellationToken cancellationToken)
    {
        using var activity = DefaultActivitySource.Instance.StartActivity();


        return await ExecuteWithRateLimit(async (ct) =>
        {
            using var connection = _connectionFactory();

            var queryTemplate = new SqlBuilder()
                .FilterRelations(entityRelationFilter, subjectType, entityIds, subjectRelation)
                .AddTemplate(@"SELECT 
                    entity_type,
                    entity_id,
                    relation,
                    subject_type,
                    subject_id, 
                    subject_relation 
                FROM relation_tuples /**where**/");

#if DEBUG
            _logger.LogDebug("Querying relations tuples with filter {sql}, with params: {params}", queryTemplate.RawSql,
                JsonSerializer.Serialize(new { entityRelationFilter, subjectType, entitiesIds = entityIds, subjectRelation }));
            var start = Stopwatch.GetTimestamp();
#endif
            var res = (await connection.QueryAsync<RelationTuple>(new CommandDefinition(queryTemplate.RawSql,
                    queryTemplate.Parameters, cancellationToken: ct)))
                .ToList();


#if DEBUG
            _logger.LogDebug("Queried relations in {QueryDuration}ms, returned {QueryItemCount} items",
                Stopwatch.GetElapsedTime(start).TotalMilliseconds, res.Count);
#endif
            return res;
        }, cancellationToken);
    }
    
    public async Task<List<RelationTuple>> GetRelationsWithSubjectsIds(EntityRelationFilter entityFilter, IList<string> subjectsIds, string subjectType, CancellationToken cancellationToken)
    {
        using var activity = DefaultActivitySource.Instance.StartActivity();

        return await ExecuteWithRateLimit(async (ct) =>
        {
            using var connection = _connectionFactory();

            var queryTemplate = new SqlBuilder()
                .FilterRelations(entityFilter, subjectsIds, subjectType)
                .AddTemplate(@"SELECT 
                    entity_type,
                    entity_id,
                    relation,
                    subject_type,
                    subject_id, 
                    subject_relation 
                FROM relation_tuples /**where**/");

#if DEBUG
            _logger.LogDebug("Querying relations tuples with filter {sql}, with params: {params}", queryTemplate.RawSql,
                JsonSerializer.Serialize(new { entityFilter, subjectsIds }));
            var start = Stopwatch.GetTimestamp();
#endif

            var res = (await connection.QueryAsync<RelationTuple>(new CommandDefinition(queryTemplate.RawSql,
                    queryTemplate.Parameters, cancellationToken: ct)))
                .ToList();

#if DEBUG
            _logger.LogDebug("Queried relations in {QueryDuration}ms, returned {QueryItemCount} items",
                Stopwatch.GetElapsedTime(start).TotalMilliseconds, res.Count);
#endif

            return res;
        }, cancellationToken);
    }
    
    public async Task<AttributeTuple?> GetAttribute(EntityAttributeFilter filter, CancellationToken cancellationToken)
    {
        using var activity = DefaultActivitySource.Instance.StartActivity();

        return await ExecuteWithRateLimit(async (ct) =>
        {
            using var connection = _connectionFactory();

            var queryTemplate = new SqlBuilder()
                .FilterAttributes(filter)
                .AddTemplate(@"SELECT
                    entity_type,
                    entity_id,
                    attribute,
                    value
                FROM attributes /**where**/
                LIMIT 1");

            _logger.LogDebug("Querying attributes tuples with filter: {filter}", filter);

            return await connection.QuerySingleOrDefaultAsync<AttributeTuple>(new CommandDefinition(
                queryTemplate.RawSql,
                queryTemplate.Parameters, cancellationToken: ct));
        }, cancellationToken);

    }

    public async Task<List<AttributeTuple>> GetAttributes(EntityAttributeFilter filter, CancellationToken cancellationToken)
    {
        using var activity = DefaultActivitySource.Instance.StartActivity();

        return await ExecuteWithRateLimit(async (ct) =>
        {
            using var connection = _connectionFactory();

            var queryTemplate = new SqlBuilder()
                .FilterAttributes(filter)
                .AddTemplate(@"SELECT
                    entity_type,
                    entity_id,
                    attribute,
                    value
                FROM attributes /**where**/");

            _logger.LogDebug("Querying attributes tuples with filter: {filter}", filter);

            return (await connection.QueryAsync<AttributeTuple>(new CommandDefinition(queryTemplate.RawSql,
                    queryTemplate.Parameters, cancellationToken: ct)))
                .ToList();
        }, cancellationToken);

    }

    public async Task<List<AttributeTuple>> GetAttributes(AttributeFilter filter, IEnumerable<string> entitiesIds, CancellationToken cancellationToken)
    {
        using var activity = DefaultActivitySource.Instance.StartActivity();

        return await ExecuteWithRateLimit(async (ct) =>
        {
            using var connection = _connectionFactory();

            var queryTemplate = new SqlBuilder()
                .FilterAttributes(filter, entitiesIds)
                .AddTemplate(@"SELECT
                    entity_type,
                    entity_id,
                    attribute,
                    value
                FROM attributes /**where**/");

            _logger.LogDebug("Querying attributes tuples with filter: {filter}", filter);

            return (await connection.QueryAsync<AttributeTuple>(new CommandDefinition(queryTemplate.RawSql,
                    queryTemplate.Parameters, cancellationToken: ct)))
                .ToList();
        }, cancellationToken);
    }
}