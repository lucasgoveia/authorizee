using System.Diagnostics;
using Authorizee.Core;
using Authorizee.Core.Configuration;
using Authorizee.Core.Data;
using Authorizee.Core.Observability;
using Authorizee.Core.Schemas;
using Authorizee.Data;
using Authorizee.Data.Configuration;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDatabaseSetup(() => new NpgsqlConnection(builder.Configuration.GetConnectionString("Db")!));

builder.Services.AddSchemaConfiguration(c =>
{
    c
        .WithEntity("user")
        .WithEntity("organization")
            .WithRelation("admin", rc => rc.WithEntityType("user"))
            .WithRelation("member", rc => rc.WithEntityType("user"))
        .WithEntity("team")
            .WithRelation("owner", rc => rc.WithEntityType("user"))
            .WithRelation("member", rc => rc.WithEntityType("user"))
            .WithRelation("org", rc => rc.WithEntityType("organization"))
            .WithPermission("edit", PermissionNode.Union("org.admin", "owner"))
            .WithPermission("delete", PermissionNode.Union("org.admin", "owner"))
            .WithPermission("invite", PermissionNode.Intersect("org.admin", PermissionNode.Union("owner", "member")))
            .WithPermission("remove_user", PermissionNode.Leaf("owner"))
        .WithEntity("project")
            .WithRelation("org", rc => rc.WithEntityType("organization"))
            .WithRelation("team", rc => rc.WithEntityType("team"))
            .WithRelation("member", rc => rc.WithEntityType("team", "member").WithEntityType("user"))
            .WithAttribute("public", typeof(bool))
            .WithPermission("view",  
            PermissionNode.Union(
                PermissionNode.Leaf("org.admin"), 
                PermissionNode.Leaf("member"), 
                PermissionNode.Intersect("public", "org.member"))
            )
            .WithPermission("edit", PermissionNode.Union("org.admin", "team.member"))
            .WithPermission("delete", PermissionNode.Leaf("team.member"));
});

Activity.DefaultIdFormat = ActivityIdFormat.W3C;


builder.Services
    .AddOpenTelemetry()
    .ConfigureResource((rb) => rb
        .AddService(serviceName: DefaultActivitySource.SourceName)
        .AddTelemetrySdk()
        .AddEnvironmentVariableDetector())
    .WithTracing(telemetry =>
    {
        telemetry
            .AddSource(DefaultActivitySource.SourceName)
            .AddSource(DefaultActivitySource.SourceNameInternal)
            .AddAspNetCoreInstrumentation(o =>
            {
                o.RecordException = true;
            })
            .AddOtlpExporter();
    })
    .WithMetrics(telemetry =>
    {
        telemetry
            .AddMeter("Microsoft.AspNetCore.Hosting")
            .AddMeter("Microsoft.AspNetCore.Server.Kestrel")
            .AddView("http-server-request-duration",
                new ExplicitBucketHistogramConfiguration
                {
                    Boundaries = new double[] { 0, 0.005, 0.01, 0.025, 0.05,
                        0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10 }
                })
            .AddAspNetCoreInstrumentation()
            .AddOtlpExporter();
    });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("/check",
        async ([AsParameters] CheckRequest req, [FromServices] PermissionEngine service, CancellationToken ct) => await service.Check(req, ct))
    .WithName("Check Relation")
    .WithOpenApi();

app.MapPost("/lookup-entity",
        async ([FromBody] LookupEntityRequest req, [FromServices] LookupEngine service, CancellationToken ct) => await service.LookupEntity(req, ct))
    .WithName("Lookup entity")
    .WithOpenApi();

app.Run();