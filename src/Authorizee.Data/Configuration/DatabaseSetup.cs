﻿using System.Data;
using Authorizee.Core.Data;
using Dapper;
using Microsoft.Extensions.DependencyInjection;

namespace Authorizee.Data.Configuration;

public delegate IDbConnection DbConnectionFactory();

public static class DatabaseSetup
{
    public static void AddDatabaseSetup(this IServiceCollection  services, DbConnectionFactory factory)
    {
        Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;
        SqlMapper.AddTypeHandler(new JsonTypeHandler());
        services.AddScoped<DbConnectionFactory>(_ => factory);
        services.AddScoped<IRelationTupleReader, RelationTupleReader>();
        services.AddScoped<IAttributeReader, AttributeReader>();
    }
}