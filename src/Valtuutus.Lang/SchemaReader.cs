﻿using Antlr4.Runtime;
using Valtuutus.Core.Schemas;

namespace Valtuutus.Lang;

public static class SchemaReader
{
    private static readonly Dictionary<string, Type> _types = new()
    {
        { "string", typeof(string) },
        { "int", typeof(int)},
        { "bool", typeof(bool)},
        { "decimal", typeof(decimal) }
    };

    public static Schema Parse(string schema)
    {
        var schemaBuilder = new SchemaBuilder();

        var str = new AntlrInputStream(schema);
        var lexer = new ValtuutusLexer(str);
        var errorListener = new ParserSchemaErrorListener();
        lexer.RemoveErrorListeners();
        lexer.AddErrorListener(errorListener);
        var tokens = new CommonTokenStream(lexer);
        var parser = new ValtuutusParser(tokens);
        parser.RemoveErrorListeners();
        parser.AddErrorListener(errorListener);
        errorListener.ThrowIfErrors();
        var tree = parser.schema();
        
        // Parse functions
        foreach (var funcs in tree.functionDefinition())
        {
            //funcs.
        }
        
        // Parse entities
        foreach (var entityCtx in tree.entityDefinition())
        {
            var entityBuilder = schemaBuilder.WithEntity(entityCtx.ID().GetText());

            var relationCtx = entityCtx.entityBody().relationDefinition()!;

            foreach (var relation in relationCtx)
            {
                var idlen = relation.ID().Length;
                if (idlen < 2)
                {
                    throw new SchemaParseException(new List<string>
                    {
                        $"Invalid relation definition in entity {entityCtx.ID().GetText()}: relation name and target entity are required."
                    });
                }
                entityBuilder.WithRelation(relation.ID(0).GetText(), relationBuilder =>
                {
                    var subjectRelation = relation.POUND() != null;
                    if (!subjectRelation)
                    {
                        relationBuilder.WithEntityType(relation.ID(idlen -1).GetText());
                    }
                    else
                    {
                        relationBuilder.WithEntityType(relation.ID(idlen -2).GetText(), relation.ID(idlen -1).GetText());
                    }
                });
            }
            
            var attributeCtx = entityCtx.entityBody().attributeDefinition()!;

            foreach (var attribute in attributeCtx)
            {
                var attrType = _types[attribute.type().GetText()];
                entityBuilder.WithAttribute(attribute.ID().GetText(), attrType);
            }
            
            var permissionCtx = entityCtx.entityBody().permissionDefinition()!;

            foreach (var permission in permissionCtx)
            {
                // Convert permission expression to PermissionNode
                var permissionTree = BuildPermissionNode(permission.expression());
                entityBuilder.WithPermission(permission.ID().GetText(), permissionTree);
            }
            
        }
        return schemaBuilder.Build();

    }
    
    private static PermissionNode BuildPermissionNode(ValtuutusParser.ExpressionContext context)
    {
        switch (context)
        {
            case ValtuutusParser.AndExpressionContext andCtx:
                var leftAnd = BuildPermissionNode(andCtx.expression(0));
                var rightAnd = BuildPermissionNode(andCtx.expression(1));
                return PermissionNode.Intersect(leftAnd, rightAnd);

            case ValtuutusParser.OrExpressionContext orCtx:
                var leftOr = BuildPermissionNode(orCtx.expression(0));
                var rightOr = BuildPermissionNode(orCtx.expression(1));
                return PermissionNode.Union(leftOr, rightOr);

            case ValtuutusParser.IdentifierExpressionContext idCtx:
                return PermissionNode.Leaf(idCtx.ID().GetText());

            
            // TODO: Do some magic to handle the creation of expressions
            // case ValtuutusParser.FunctionCallExpressionContext funcCtx:
            //     var funcName = funcCtx.ID().GetText();
            //     if (funcName == "is_weekday")
            //     {
            //         return PermissionNode.AttributeStringExpression("day_of_week", day => day != "saturday" && day != "sunday");
            //     }
            //     else if (funcName == "check_credit")
            //     {
            //         return PermissionNode.AttributeIntExpression("credit", credit => credit > 5000);
            //     }
            //     throw new Exception($"Unknown function: {funcName}");

            case ValtuutusParser.ParenthesisExpressionContext parenCtx:
                return BuildPermissionNode(parenCtx.expression());

            default:
                throw new Exception("Unsupported expression type");
        }
    }
}