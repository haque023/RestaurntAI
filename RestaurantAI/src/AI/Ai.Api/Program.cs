using RestaurantAI.AI.Api.Endpoints;
using RestaurantAI.AI.Api.Integration;
using RestaurantAI.AI.Api.Usage;
using RestaurantAI.AI.Context;
using RestaurantAI.AI.Foundry;
using RestaurantAI.AI.Memory;
using RestaurantAI.AI.Rag;
using RestaurantAI.Infrastructure.Persistence.PostgreSQL;
using RestaurantAI.Infrastructure.Persistence.Redis;
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
builder.Services.AddRedisCache(builder.Configuration, instanceName: "ai");
builder.Services.AddMemoryCache();
builder.Services.Configure<AiUsageOptions>(builder.Configuration.GetSection(AiUsageOptions.SectionName));
builder.Services.AddScoped<AiUsageService>();
builder.Services.AddHttpClient<RestaurantAiContextClient>(client =>
        client.BaseAddress = new Uri(builder.Configuration["Services:Gateway"] ?? "http://localhost:5080"))
    .AddStandardResilienceHandler();

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
app.MapDiscoveryAssistantEndpoints();
app.MapAiUsageEndpoints();

await app.RunAsync();

public partial class Program;
