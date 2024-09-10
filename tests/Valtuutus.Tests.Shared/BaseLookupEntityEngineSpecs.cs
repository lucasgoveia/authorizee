using System.Text.Json.Nodes;
using Valtuutus.Core;
using Valtuutus.Core.Schemas;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Valtuutus.Core.Configuration;
using Valtuutus.Core.Engines.LookupEntity;
using Valtuutus.Data;
using Valtuutus.Core.Data;
using Valtuutus.Core.Lang;

namespace Valtuutus.Tests.Shared;

public abstract class BaseLookupEntityEngineSpecs : IAsyncLifetime
{
    protected BaseLookupEntityEngineSpecs(IDatabaseFixture fixture)
    {
        Fixture = fixture;
    }
    protected abstract IValtuutusDataBuilder AddSpecificProvider(IServiceCollection services);
    
    protected IDatabaseFixture Fixture { get; }
    
    private ServiceProvider CreateServiceProvider(string? schema = null)
    {
        var services = new ServiceCollection()
            .AddValtuutusCore(schema ?? TestsConsts.DefaultSchema);
        AddSpecificProvider(services)
            .AddConcurrentQueryLimit(5);

        return services.BuildServiceProvider();
    }

    private async ValueTask<ILookupEntityEngine> CreateEngine(RelationTuple[] tuples, AttributeTuple[] attributes, string? schema = null)
    {
        var serviceProvider = CreateServiceProvider(schema);
        var scope = serviceProvider.CreateScope();
        var lookupEntityEngine = scope.ServiceProvider.GetRequiredService<ILookupEntityEngine>();
        if(tuples.Length == 0 && attributes.Length == 0) return lookupEntityEngine;
        var provider = scope.ServiceProvider.GetRequiredService<IDataWriterProvider>();
        await provider.Write(tuples, attributes, default);
        return lookupEntityEngine;
    }
    
    public async Task InitializeAsync()
    {
        await Fixture.ResetDatabaseAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }
    
    public static TheoryData<RelationTuple[], AttributeTuple[], LookupEntityRequest, HashSet<string>>
        TopLevelChecks = LookupEntityEngineSpecList.TopLevelChecks;

    [Theory]
    [MemberData(nameof(TopLevelChecks))]
    public async Task TopLevelCheckShouldReturnExpectedResult(RelationTuple[] tuples, AttributeTuple[] attributes,
        LookupEntityRequest request, HashSet<string> expected)
    {
        // Arrange
        var engine = await CreateEngine(tuples, attributes);

        // Act
        var result = await engine.LookupEntity(request, default);

        // assert
        result.Should().BeEquivalentTo(expected);
    }

    public static TheoryData<RelationTuple[], AttributeTuple[], LookupEntityRequest, HashSet<string>>
        IndirectRelationLookup = LookupEntityEngineSpecList.IndirectRelationLookup;

    [Theory]
    [MemberData(nameof(IndirectRelationLookup))]
    public async Task IndirectRelationLookupShouldReturnExpectedResult(RelationTuple[] tuples,
        AttributeTuple[] attributes, LookupEntityRequest request, HashSet<string> expected)
    {
        // Arrange
        var engine = await CreateEngine(tuples, attributes);

        // Act
        var result = await engine.LookupEntity(request, default);

        // assert
        result.Should().BeEquivalentTo(expected);
    }

    public static TheoryData<RelationTuple[], AttributeTuple[], LookupEntityRequest, HashSet<string>>
        SimplePermissionLookup = LookupEntityEngineSpecList.SimplePermissionLookup;
    
    [Theory]
    [MemberData(nameof(SimplePermissionLookup))]
    public async Task SimplePermissionLookupShouldReturnExpectedResult(RelationTuple[] tuples,
        AttributeTuple[] attributes, LookupEntityRequest request, HashSet<string> expected)
    {
        // Arrange
        var engine = await CreateEngine(tuples, attributes);

        // Act
        var result = await engine.LookupEntity(request, default);

        // assert
        result.Should().BeEquivalentTo(expected);
    }
    
    public static TheoryData<RelationTuple[], AttributeTuple[], LookupEntityRequest, HashSet<string>>
        IntersectWithRelationAndAttributePermissionLookup = LookupEntityEngineSpecList.IntersectWithRelationAndAttributePermissionLookup;
    
    [Theory]
    [MemberData(nameof(IntersectWithRelationAndAttributePermissionLookup))]
    public async Task IntersectWithRelationAndAttributeLookupShouldReturnExpectedResult(RelationTuple[] tuples,
        AttributeTuple[] attributes, LookupEntityRequest request, HashSet<string> expected)
    {
        // Arrange
        var engine = await CreateEngine(tuples, attributes);

        // Act
        var result = await engine.LookupEntity(request, default);

        // assert
        result.Should().BeEquivalentTo(expected);
    }
    
    public static TheoryData<RelationTuple[], AttributeTuple[], LookupEntityRequest, HashSet<string>>
        IntersectAttributeExpressionWithOtherNodesLookup = LookupEntityEngineSpecList.IntersectAttributeExpressionWithOtherNodes;
    
    [Theory]
    [MemberData(nameof(IntersectAttributeExpressionWithOtherNodesLookup))]
    public async Task IntersectAttributeExpressionWithOtherNodesLookupShouldReturnExpectedEntities(RelationTuple[] tuples,
        AttributeTuple[] attributes, LookupEntityRequest request, HashSet<string> expected)
    {
        // Arrange
        var engine = await CreateEngine(tuples, attributes);

        // Act
        var result = await engine.LookupEntity(request, default);

        // assert
        result.Should().BeEquivalentTo(expected);
    }
    
    [Fact]
    public async Task TestStringBasedAttributeExpression()
    {
        // arrange
        var schema = @"
            entity user {}
            entity workspace {
                attribute status bool;
                permission edit:= isActiveStatus(status);
            }
            fn isActiveStatus(status string) => status == ""active"";
        ";

        // act
        var engine = await CreateEngine([], [
            new AttributeTuple(TestsConsts.Workspaces.Identifier, TestsConsts.Workspaces.PublicWorkspace, "status",
                JsonValue.Create("active")!),
            new AttributeTuple(TestsConsts.Workspaces.Identifier, TestsConsts.Workspaces.PrivateWorkspace, "status",
                JsonValue.Create("active")!),
            new AttributeTuple(TestsConsts.Workspaces.Identifier, "3", "status",
                JsonValue.Create("archived")!),
        ], schema);

        // Act
        var result = await engine.LookupEntity(new LookupEntityRequest(TestsConsts.Workspaces.Identifier,
            "edit", "user", "1"), default);

        // assert
        result.Should().BeEquivalentTo(TestsConsts.Workspaces.PublicWorkspace, TestsConsts.Workspaces.PrivateWorkspace);
    }

    [Fact]
    public async Task TestDecimalBasedAttributeExpression()
    {
        // arrange
        var entity = new SchemaBuilder()
            .WithEntity(TestsConsts.Users.Identifier)
            .WithEntity("account")
            .WithRelation("owner", c => c.WithEntityType(TestsConsts.Users.Identifier))
            .WithAttribute("balance", typeof(decimal))
            .WithPermission("withdraw", PermissionNode.Intersect(
                PermissionNode.Leaf("owner"),
                PermissionNode.Expression("check_balance", [new PermissionNodeExpArgumentAttribute() { ArgOrder = 0, AttributeName = "balance" }])
            ))
            .SchemaBuilder
            .WithFunction(new Function("check_balance",
                [new FunctionParameter { ParamName = "balance", ParamOrder = 0, ParamType = LangType.Decimal }],
                (args) => (decimal?)args["balance"] >= 500m));

        var schema = entity.Build();

        // act
        var engine = await CreateEngine(
            [
                new RelationTuple("account", "1", "owner", TestsConsts.Users.Identifier, "1"),
                new RelationTuple("account", "2", "owner", TestsConsts.Users.Identifier, "1")
            ],
            [
                new AttributeTuple("account", "1", "balance",
                    JsonValue.Create(872.54m)),
                new AttributeTuple("account", "2", "balance",
                    JsonValue.Create(12.11m)),
            ], schema);

        // Act
        var result = await engine.LookupEntity(new LookupEntityRequest("account",
            "withdraw", "user", "1"), default);

        // assert
        result.Should().BeEquivalentTo("1");
    }

    public static TheoryData<RelationTuple[], AttributeTuple[], LookupEntityRequest, HashSet<string>>
        UnionRelationDepthLimit = LookupEntityEngineSpecList.UnionRelationDepthLimit;

    [Theory]
    [MemberData(nameof(UnionRelationDepthLimit))]
    public async Task LookupEntityWithDepthLimit(RelationTuple[] tuples,
        AttributeTuple[] attributes, LookupEntityRequest request, HashSet<string> expected)
    {
        // Arrange
        
        var schema = @"
            entity user {}
            entity group {
                relation member @user;
            }
            entity workspace {
                relation group_members @group;
                permission view := group_members.member;
            }
            fn isActiveStatus(status string) => status == ""active"";
        ";
        var engine = await CreateEngine(tuples, attributes, schema);

        // Act
        var result = await engine.LookupEntity(request, default);

        // Assert
        result.Should().BeEquivalentTo(expected);
    }
}