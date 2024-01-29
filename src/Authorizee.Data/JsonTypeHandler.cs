﻿using System.Data;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dapper;
using JsonSerializer = Jint.Native.Json.JsonSerializer;

namespace Authorizee.Data;

public class JsonTypeHandler : SqlMapper.TypeHandler<JsonValue>
{
    public override void SetValue(IDbDataParameter parameter, JsonValue? value)
    {
        parameter.Value = value?.ToJsonString();
    }

    public override JsonValue? Parse(object value)
    {
        if (value is string json)
        {
            return JsonNode.Parse(json)?.AsValue();
        }

        return default;
    }
}

