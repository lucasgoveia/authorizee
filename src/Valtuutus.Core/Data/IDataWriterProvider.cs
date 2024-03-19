﻿namespace Valtuutus.Core.Data;

public interface IDataWriterProvider
{
    public Task<SnapToken> Write(IEnumerable<RelationTuple> relations, IEnumerable<AttributeTuple> attributes, CancellationToken ct);

    public Task<SnapToken> Delete(DeleteFilter filter, CancellationToken ct);
}