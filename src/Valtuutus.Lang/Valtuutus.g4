﻿grammar Valtuutus;

schema : (entityDefinition | functionDefinition)* EOF ;

entityDefinition
    : ENTITY ID LBRACE entityBody RBRACE
    ;

entityBody
    : (relationDefinition | attributeDefinition | permissionDefinition | COMMENT)*
    ;

relationDefinition
    : RELATION ID relationMember+ SEMI 
    ;

relationMember
    : ('@' ID (POUND ID)?)
    ;

attributeDefinition
    : ATTRIBUTE ID type SEMI
    ;

permissionDefinition
    : PERMISSION ID ASSIGN permissionExpression SEMI
    ;

functionDefinition
    : FN ID LPAREN parameterList RPAREN ARROW functionBody SEMI
    ;

parameterList
    : (ID type (COMMA ID type)*)?
    ;

type
    : INT | BOOL | STRING | DECIMAL
    ;

permissionExpression
    : permissionExpression AND permissionExpression       #andPermissionExpression
    | permissionExpression OR permissionExpression        #orPermissionExpression
    | functionCall                                        #functionCallPermissionExpression
    | ID (DOT ID)?                                        #identifierPermissionExpression
    | LPAREN permissionExpression RPAREN                  #parenthesisPermissionExpression
    ;

functionBody
    : functionExpression
    ;

functionExpression
    : functionExpression EQUAL functionExpression            #equalityExpression
    | functionExpression NOT_EQUAL functionExpression        #inequalityExpression
    | functionExpression GREATER functionExpression          #greaterExpression
    | functionExpression LESS functionExpression             #lessExpression
    | functionExpression GREATER_OR_EQUAL functionExpression #greaterOrEqualExpression
    | functionExpression LESS_OR_EQUAL functionExpression    #lessOrEqualExpression
    | ID                                                     #identifierExpression
    | literal                                                #literalExpression
    | functionExpression OR functionExpression               #orExpression
    | functionExpression AND functionExpression              #andExpression
    | NOT LPAREN functionExpression RPAREN                   #notExpression
    | LPAREN functionExpression RPAREN                       #parenthesisExpression
    ;

functionCall
    : ID LPAREN argumentList RPAREN
    ;

contextAccess
    : CONTEXT DOT ID
    ;
    
argumentList
    : (argument (COMMA argument)*)?
    ;
    
argument
    : ID | contextAccess | literal
    ;
    
literal
    : STRING_LITERAL | INT_LITERAL | DECIMAL_LITERAL | BOOLEAN_LITERAL
    ;

// keywords
ENTITY     : 'entity';
RELATION   : 'relation';
ATTRIBUTE  : 'attribute';
PERMISSION : 'permission';
CONTEXT    : 'context';
FN         : 'fn';

// types
INT        : 'int';
BOOL       : 'bool';
STRING     : 'string';
DECIMAL    : 'decimal';

// operators
ASSIGN     : ':=';
AND        : 'and';
OR         : 'or';
NOT        : 'not';
NOT_EQUAL  : '!=';
EQUAL      : '==';
GREATER    : '>';
GREATER_OR_EQUAL : '>=';
LESS_OR_EQUAL    : '<=';
LESS       : '<';

// literals
STRING_LITERAL : '"' (~["\\] | '\\' .)* '"' ;
INT_LITERAL    : [0-9]+ ;
DECIMAL_LITERAL : [0-9]+ '.' [0-9]+ ;
BOOLEAN_LITERAL: 'true' | 'false';

// structure
ARROW      : '=>';
POUND      : '#';
LBRACE     : '{';
RBRACE     : '}';
LBRACKET   : '[';
RBRACKET   : ']';
LPAREN     : '(';
RPAREN     : ')';
COMMA      : ',';
SEMI       : ';';
DOT        : '.';
ID         : [a-zA-Z_][a-zA-Z_0-9]*;
WS         : [ \t\r\n]+ -> skip;
COMMENT    : '//' ~[\r\n]* -> skip;