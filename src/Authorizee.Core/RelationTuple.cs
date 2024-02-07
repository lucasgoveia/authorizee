﻿namespace Authorizee.Core;

public record RelationTuple
{
    public string EntityType { get; private init; } = null!;
    public string EntityId { get; private init; } = null!;
    public string Relation { get; private init; } = null!;
    public string SubjectType { get; private init; } = null!;
    public string SubjectId { get; private init; } = null!;
    public string SubjectRelation { get; private init; } = null!;
    
    protected RelationTuple() {}
    
    public RelationTuple(string entityType, string entityId, string relation, string subjectType, string subjectId, string? subjectRelation = null)
    {
        EntityType = entityType;
        EntityId = entityId;
        Relation = relation;
        SubjectType = subjectType;
        SubjectId = subjectId;
        SubjectRelation = subjectRelation ?? "";
    }


    public bool IsDirectSubject()
    {
        return SubjectRelation == "";
    }
}