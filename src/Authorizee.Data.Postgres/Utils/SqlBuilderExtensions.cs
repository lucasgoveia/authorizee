﻿using Authorizee.Core.Data;
using Dapper;

namespace Authorizee.Data.Postgres.Utils;

public static class SqlBuilderExtensions
{
    public static SqlBuilder FilterRelations(this SqlBuilder builder, RelationTupleFilter tupleFilter)
    {
        builder = builder.Where("entity_type = @EntityType", tupleFilter);
        builder = builder.Where("entity_id = @EntityId", tupleFilter);
        builder = builder.Where("relation = @Relation", tupleFilter);

        if (!string.IsNullOrEmpty(tupleFilter.SubjectId))
            builder = builder.Where("subject_id = @SubjectId", tupleFilter);
        
        if (!string.IsNullOrEmpty(tupleFilter.SubjectRelation))
            builder = builder.Where("subject_relation = @SubjectRelation", tupleFilter);
        
        if (!string.IsNullOrEmpty(tupleFilter.SubjectType))
            builder = builder.Where("subject_type = @SubjectType", tupleFilter);
        
        return builder;
    }
    
    public static SqlBuilder FilterRelations(this SqlBuilder builder, EntityRelationFilter entityRelationFilter,
        string subjectType, IEnumerable<string> entitiesIds, string? subjectRelation)
    {
        var entitiesIdsArr = entitiesIds as string[] ?? entitiesIds.ToArray();
        
        if (!string.IsNullOrEmpty(subjectType))
            builder = builder.Where("subject_type = @SubjectType", new {SubjectType = subjectType});
        
        if (!string.IsNullOrEmpty(entityRelationFilter.EntityType))
            builder = builder.Where("entity_type = @EntityType", new {entityRelationFilter.EntityType});
        
        if (!string.IsNullOrEmpty(entityRelationFilter.Relation))
            builder = builder.Where("relation = @Relation", new {entityRelationFilter.Relation});
        
        if (entitiesIdsArr.Length != 0)
            builder = builder.Where("entity_id = ANY(@EntitiesIds)", new {EntitiesIds = entitiesIdsArr});
        
        if (!string.IsNullOrEmpty(subjectRelation))
            builder = builder.Where("subject_relation = @subjectRelation", new {subjectRelation});
        
        return builder;
    }
    
    public static SqlBuilder FilterRelations(this SqlBuilder builder, EntityRelationFilter entityFilter,  IList<string> subjectsIds, string subjectType)
    {
        if (!string.IsNullOrEmpty(entityFilter.EntityType))
            builder = builder.Where("entity_type = @EntityType", new {entityFilter.EntityType});
        
        if (!string.IsNullOrEmpty(entityFilter.Relation))
            builder = builder.Where("relation = @Relation", new {entityFilter.Relation});
        
        builder.Where("subject_type = @SubjectType", new {SubjectType = subjectType});

        if (subjectsIds.Count != 0)
            builder = builder.Where("subject_id = ANY(@SubjectsIds)", new {SubjectsIds = subjectsIds});
        
        return builder;
    }

    
    public static SqlBuilder FilterAttributes(this SqlBuilder builder, EntityAttributeFilter filter)
    {
        builder = builder.Where("entity_type = @EntityType", filter);
        builder = builder.Where("attribute = @Attribute", filter);
        
        if (!string.IsNullOrWhiteSpace(filter.EntityId))
            builder = builder.Where("entity_id = @EntityId", filter);
        
        return builder;
    }
    
    public static SqlBuilder FilterAttributes(this SqlBuilder builder, AttributeFilter filter, IEnumerable<string> entitiesIds)
    {
        var entitiesIdsArr = entitiesIds as string[] ?? entitiesIds.ToArray();
        
        builder = builder.Where("entity_type = @EntityType", filter);
        builder = builder.Where("attribute = @Attribute", filter);
        
        if (entitiesIdsArr.Length != 0)
            builder = builder.Where("entity_id = ANY(@entitiesIds)", new {entitiesIds = entitiesIdsArr});
        
        return builder;
    }
}