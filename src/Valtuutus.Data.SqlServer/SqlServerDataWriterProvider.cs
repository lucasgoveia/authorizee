﻿using Valtuutus.Core;
using Valtuutus.Core.Data;
using Valtuutus.Data.SqlServer.Utils;
using Dapper;
using FastMember;
using Microsoft.Data.SqlClient;
using Valtuutus.Data.Db;

namespace Valtuutus.Data.SqlServer;

internal sealed class SqlServerDataWriterProvider : IDataWriterProvider
{
    private readonly DbConnectionFactory _factory;
    private readonly ValtuutusDataOptions _options;
    private readonly IServiceProvider _provider;

    public SqlServerDataWriterProvider(DbConnectionFactory factory, ValtuutusDataOptions options,
        IServiceProvider provider)
    {
        _factory = factory;
        _options = options;
        _provider = provider;
    }

    public async Task<SnapToken> Write(IEnumerable<RelationTuple> relations, IEnumerable<AttributeTuple> attributes,
        CancellationToken ct)
    {
        var transactionId = Ulid.NewUlid();

#if NETSTANDARD2_0
        using var db = (SqlConnection) _factory();
#else
        await using var db = (SqlConnection)_factory();
#endif
        await db.OpenAsync(ct);

#if NETSTANDARD2_0
            var transaction = db.BeginTransaction();
#else
        var transaction = (SqlTransaction)await db.BeginTransactionAsync(ct);
#endif

        await InsertTransaction(db, transactionId, transaction, ct);

        var relationsBulkCopy = new SqlBulkCopy(db, SqlBulkCopyOptions.Default, transaction);
        relationsBulkCopy.DestinationTableName = "relation_tuples";

#if !NETSTANDARD2_0
        await
#endif
            using var relationsReader = ObjectReader.Create(relations.Select(x => new
            {
                x.EntityType,
                x.EntityId,
                x.SubjectType,
                x.SubjectId,
                x.Relation,
                x.SubjectRelation,
                TransactionId = transactionId.ToString()
            }));
        relationsBulkCopy.ColumnMappings.Add(new SqlBulkCopyColumnMapping("EntityType", "entity_type"));
        relationsBulkCopy.ColumnMappings.Add(new SqlBulkCopyColumnMapping("EntityId", "entity_id"));
        relationsBulkCopy.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Relation", "relation"));
        relationsBulkCopy.ColumnMappings.Add(new SqlBulkCopyColumnMapping("SubjectType", "subject_type"));
        relationsBulkCopy.ColumnMappings.Add(new SqlBulkCopyColumnMapping("SubjectId", "subject_id"));
        relationsBulkCopy.ColumnMappings.Add(new SqlBulkCopyColumnMapping("SubjectRelation", "subject_relation"));
        relationsBulkCopy.ColumnMappings.Add(new SqlBulkCopyColumnMapping("TransactionId", "created_tx_id"));

        await relationsBulkCopy.WriteToServerAsync(relationsReader, ct);

        await db.ExecuteAsync(new CommandDefinition(
            "CREATE TABLE #temp_attributes (entity_type NVARCHAR(256), entity_id NVARCHAR(64), attribute NVARCHAR(64), value NVARCHAR(256), created_tx_id CHAR(26))",
            transaction: transaction, cancellationToken: ct));

        using var attributesBulkCopy = new SqlBulkCopy(db, SqlBulkCopyOptions.Default, transaction);
        attributesBulkCopy.DestinationTableName = "#temp_attributes";

#if !NETSTANDARD2_0
        await
#endif

            using var attributesReader = ObjectReader.Create(attributes.Select(t => new
            {
                t.EntityType,
                t.EntityId,
                t.Attribute,
                Value = t.Value.ToJsonString(),
                TransactionId = transactionId.ToString()
            }));
        attributesBulkCopy.ColumnMappings.Add(new SqlBulkCopyColumnMapping("EntityType", "entity_type"));
        attributesBulkCopy.ColumnMappings.Add(new SqlBulkCopyColumnMapping("EntityId", "entity_id"));
        attributesBulkCopy.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Attribute", "attribute"));
        attributesBulkCopy.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Value", "value"));
        attributesBulkCopy.ColumnMappings.Add(new SqlBulkCopyColumnMapping("TransactionId", "created_tx_id"));


        await attributesBulkCopy.WriteToServerAsync(attributesReader, ct);

        await db.ExecuteAsync(new CommandDefinition(
            """
            MERGE INTO dbo.attributes AS target
            USING #temp_attributes AS source
            ON (target.entity_type = source.entity_type 
                AND target.entity_id = source.entity_id 
                AND target.attribute = source.attribute)
            WHEN MATCHED AND target.deleted_tx_id IS NULL THEN
                UPDATE SET target.deleted_tx_id = source.created_tx_id;

            INSERT INTO dbo.attributes (entity_type, entity_id, attribute, value, created_tx_id)
            SELECT source.entity_type, source.entity_id, source.attribute, source.value, source.created_tx_id
            FROM #temp_attributes AS source;
            """, transaction: transaction, cancellationToken: ct));

#if !NETCOREAPP3_0_OR_GREATER
        transaction.Commit();
#else
        await transaction.CommitAsync(ct);
#endif

        var snapToken = new SnapToken(transactionId.ToString());
        await (_options.OnDataWritten?.Invoke(_provider, snapToken) ?? Task.CompletedTask);
        return snapToken;
    }


    public async Task<SnapToken> Delete(DeleteFilter filter, CancellationToken ct)
    {
        var transactId = Ulid.NewUlid();

#if NETSTANDARD2_0
        using var db = (SqlConnection) _factory();
#else
        await using var db = (SqlConnection)_factory();
#endif
        await db.OpenAsync(ct);

#if NETSTANDARD2_0
        var transaction = db.BeginTransaction();
#else
        var transaction = (SqlTransaction)await db.BeginTransactionAsync(ct);
#endif
        await InsertTransaction(db, transactId, transaction, ct);

        var snapTokenParam = new
        {
            SnapToken = new DbString { Length = 26, Value = transactId.ToString(), IsFixedLength = true }
        };

        if (filter.Relations.Length > 0)
        {
            var relationsBuilder = new SqlBuilder();
            relationsBuilder = relationsBuilder.FilterDeleteRelations(filter.Relations);
            var queryTemplate =
                relationsBuilder.AddTemplate(@"UPDATE relation_tuples set deleted_tx_id = @SnapToken /**where**/",
                    snapTokenParam);

            await db.ExecuteAsync(new CommandDefinition(queryTemplate.RawSql, queryTemplate.Parameters,
                cancellationToken: ct, transaction: transaction));
        }

        if (filter.Attributes.Length > 0)
        {
            var attributesBuilder = new SqlBuilder();
            attributesBuilder = attributesBuilder.FilterDeleteAttributes(filter.Attributes);
            var queryTemplate =
                attributesBuilder.AddTemplate(@"UPDATE attributes set deleted_tx_id = @SnapToken /**where**/",
                    snapTokenParam);

            await db.ExecuteAsync(new CommandDefinition(queryTemplate.RawSql, queryTemplate.Parameters,
                cancellationToken: ct, transaction: transaction));
        }

#if NETSTANDARD2_0
        transaction.Commit();
#else
        await transaction.CommitAsync(ct);
#endif

        var snapToken = new SnapToken(transactId.ToString());
        await (_options.OnDataWritten?.Invoke(_provider, snapToken) ?? Task.CompletedTask);
        return snapToken;
    }

    private static async Task InsertTransaction(SqlConnection db, Ulid transactId, SqlTransaction transaction,
        CancellationToken ct)
    {
        await db.ExecuteAsync(new CommandDefinition(
            "INSERT INTO transactions (id, created_at) VALUES (@id, @created_at)",
            new { id = transactId, created_at = DateTimeOffset.UtcNow }, transaction: transaction,
            cancellationToken: ct));
    }
}