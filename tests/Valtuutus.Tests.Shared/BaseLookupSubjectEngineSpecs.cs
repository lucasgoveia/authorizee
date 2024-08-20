﻿using System.Text.Json.Nodes;
using Valtuutus.Core;
using Valtuutus.Core.Schemas;
using FluentAssertions;

namespace Valtuutus.Tests.Shared;

public abstract class BaseLookupSubjectEngineSpecs
{
    protected abstract ValueTask<LookupSubjectEngine> CreateEngine(RelationTuple[] tuples, AttributeTuple[] attributes,
        Schema? schema = null);

    public static TheoryData<RelationTuple[], AttributeTuple[], LookupSubjectRequest, HashSet<string>>
        TopLevelChecks => LookupSubjectEngineSpecList.TopLevelChecks;


    [Theory]
    [MemberData(nameof(TopLevelChecks))]
    public async Task TopLevelCheckShouldReturnExpectedResult(RelationTuple[] tuples, AttributeTuple[] attributes,
        LookupSubjectRequest request, HashSet<string> expected)
    {
        // Arrange
        var engine = await CreateEngine(tuples, attributes);

        // Act
        var result = await engine.Lookup(request, default);

        // assert
        result.Should().BeEquivalentTo(expected);
    }


    public static TheoryData<RelationTuple[], AttributeTuple[], LookupSubjectRequest, HashSet<string>>
        IndirectRelationLookup = LookupSubjectEngineSpecList.IndirectRelationLookup;

    [Theory]
    [MemberData(nameof(IndirectRelationLookup))]
    public async Task IndirectRelationLookupShouldReturnExpectedResult(RelationTuple[] tuples,
        AttributeTuple[] attributes, LookupSubjectRequest request, HashSet<string> expected)
    {
        // Arrange
        var engine = await CreateEngine(tuples, attributes);

        // Act
        var result = await engine.Lookup(request, default);

        // assert
        result.Should().BeEquivalentTo(expected);
    }

    public static TheoryData<RelationTuple[], AttributeTuple[], LookupSubjectRequest, HashSet<string>>
        SimplePermissionLookup = LookupSubjectEngineSpecList.SimplePermissionLookup;

    [Theory]
    [MemberData(nameof(SimplePermissionLookup))]
    public async Task SimplePermissionLookupShouldReturnExpectedResult(RelationTuple[] tuples,
        AttributeTuple[] attributes, LookupSubjectRequest request, HashSet<string> expected)
    {
        // Arrange
        var engine = await CreateEngine(tuples, attributes);

        // Act
        var result = await engine.Lookup(request, default);

        // assert
        result.Should().BeEquivalentTo(expected);
    }

    public static TheoryData<RelationTuple[], AttributeTuple[], LookupSubjectRequest, HashSet<string>>
        IntersectWithRelationAndAttributePermissionLookup =
            LookupSubjectEngineSpecList.IntersectWithRelationAndAttributePermissionLookup;

    [Theory]
    [MemberData(nameof(IntersectWithRelationAndAttributePermissionLookup))]
    public async Task IntersectWithRelationAndAttributeLookupShouldReturnExpectedResult(RelationTuple[] tuples,
        AttributeTuple[] attributes, LookupSubjectRequest request, HashSet<string> expected)
    {
        // Arrange
        var engine = await CreateEngine(tuples, attributes);

        // Act
        var result = await engine.Lookup(request, default);

        // assert
        result.Should().BeEquivalentTo(expected);
    }

    public static TheoryData<RelationTuple[], AttributeTuple[], LookupSubjectRequest, HashSet<string>>
        IntersectAttributeExpWithOtherNodesPermissionLookup =
            LookupSubjectEngineSpecList.IntersectAttributeExpWithOtherNodes;

    [Theory]
    [MemberData(nameof(IntersectAttributeExpWithOtherNodesPermissionLookup))]
    public async Task IntersectAttributeExpWithOtherNodesLookupShouldReturnExpectedResult(RelationTuple[] tuples,
        AttributeTuple[] attributes, LookupSubjectRequest request, HashSet<string> expected)
    {
        // Arrange
        var engine = await CreateEngine(tuples, attributes);

        // Act
        var result = await engine.Lookup(request, default);

        // assert
        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task TestStringBasedAttributeExpression()
    {
        // arrange
        var entity = new SchemaBuilder()
            .WithEntity(TestsConsts.Users.Identifier)
            .WithEntity(TestsConsts.Workspaces.Identifier)
            .WithRelation("member", c => c.WithEntityType(TestsConsts.Users.Identifier))
            .WithAttribute("status", typeof(string))
            .WithPermission("edit", PermissionNode.Intersect(
                PermissionNode.AttributeStringExpression("status", s => s == "active"),
                PermissionNode.Leaf("member")
            ));

        var schema = entity.SchemaBuilder.Build();

        // act
        var engine = await CreateEngine(
            [
                new RelationTuple(TestsConsts.Workspaces.Identifier, TestsConsts.Workspaces.PublicWorkspace, "member", TestsConsts.Users.Identifier,
                    TestsConsts.Users.Alice),
                new RelationTuple(TestsConsts.Workspaces.Identifier, TestsConsts.Workspaces.PublicWorkspace, "member", TestsConsts.Users.Identifier,
                    TestsConsts.Users.Bob),
            ],
            [
                new AttributeTuple(TestsConsts.Workspaces.Identifier, TestsConsts.Workspaces.PublicWorkspace, "status",
                    JsonValue.Create("active")!),
            ], schema);

        // Act
        var result = await engine.Lookup(new LookupSubjectRequest(TestsConsts.Workspaces.Identifier,
            "edit", "user", TestsConsts.Workspaces.PublicWorkspace), default);

        // assert
        result.Should()
            .BeEquivalentTo([TestsConsts.Users.Alice, TestsConsts.Users.Bob]);
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
                PermissionNode.AttributeDecimalExpression("balance", b => b >= 500m)
            ));

        var schema = entity.SchemaBuilder.Build();

        // act
        var engine = await CreateEngine(
            [
                new RelationTuple("account", "1", "owner", TestsConsts.Users.Identifier, TestsConsts.Users.Alice),
                new RelationTuple("account", "2", "owner", TestsConsts.Users.Identifier, TestsConsts.Users.Bob)
            ],
            [
                new AttributeTuple("account", "1", "balance",
                    JsonValue.Create(872.54m)),
                new AttributeTuple("account", "2", "balance",
                    JsonValue.Create(12.11m)),
            ], schema);

        // Act
        var result = await engine.Lookup(new LookupSubjectRequest("account",
            "withdraw", "user", "1"), default);

        // assert
        result.Should().BeEquivalentTo([TestsConsts.Users.Alice]);
    }

    public static TheoryData<RelationTuple[], AttributeTuple[], LookupSubjectRequest, HashSet<string>>
        UnionRelationDepthLimit = LookupSubjectEngineSpecList.UnionRelationDepthLimit;

    [Theory]
    [MemberData(nameof(UnionRelationDepthLimit))]
    public async Task LookupEntityWithDepthLimit(RelationTuple[] tuples,
        AttributeTuple[] attributes, LookupSubjectRequest request, HashSet<string> expected)
    {
        // Arrange
        var schema = new SchemaBuilder()
            .WithEntity(TestsConsts.Users.Identifier)
            .WithEntity(TestsConsts.Groups.Identifier)
                .WithRelation("member", rc =>
                    rc.WithEntityType(TestsConsts.Users.Identifier))
            .WithEntity(TestsConsts.Workspaces.Identifier)
                .WithRelation("group_members", rc =>
                    rc.WithEntityType(TestsConsts.Groups.Identifier))
                .WithPermission("view", PermissionNode.Leaf("group_members.member"))
            .SchemaBuilder.Build();
        var engine = await CreateEngine(tuples, attributes, schema);

        // Act
        var result = await engine.Lookup(request, default);

        // Assert
        result.Should().BeEquivalentTo(expected);
    }
}