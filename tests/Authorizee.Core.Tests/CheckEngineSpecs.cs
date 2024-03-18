using Authorizee.Core.Schemas;
using Authorizee.Tests.Shared;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Authorizee.Core.Tests;

public sealed class CheckEngineSpecs : BaseCheckEngineSpecs
{
    protected override ValueTask<CheckEngine> CreateEngine(RelationTuple[] tuples, AttributeTuple[] attributes, Schema? schema = null)
    {
        var readerProvider = new InMemoryReaderProvider(tuples, attributes);
        var logger = Substitute.For<ILogger<CheckEngine>>();
        return ValueTask.FromResult(new CheckEngine(readerProvider, schema ?? TestsConsts.Schemas, logger));
    }
}