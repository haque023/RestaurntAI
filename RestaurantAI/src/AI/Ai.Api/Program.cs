using RestaurantAI.AI.Api.Endpoints;
using RestaurantAI.AI.Context;
using RestaurantAI.AI.Foundry;
using RestaurantAI.AI.Memory;
using RestaurantAI.AI.Rag;
using RestaurantAI.Infrastructure.Persistence.PostgreSQL;
using RestaurantAI.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

// AI platform host: Context Orchestrator + Memory + Claude, with tools through the MCP Gateway.
// The AI never touches business tables; it acts only through MCP tools with the user's token.
builder.AddServiceDefaults();
builder.Services.AddOpenApi();

builder.Services.AddAiMemory(builder.Configuration);
builder.Services.AddSingleton<IKnowledgeRetriever, NoKnowledgeRetriever>();
builder.Services.AddContextOrchestrator();
builder.Services.AddClaude(builder.Configuration);

var app = builder.Build();

app.UseServiceDefaults();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await app.Services.MigrateDatabaseAsync<MemoryDbContext>();
}

app.MapDefaultEndpoints();
app.MapWaiterEndpoints();

await app.RunAsync();

public partial class Program;
