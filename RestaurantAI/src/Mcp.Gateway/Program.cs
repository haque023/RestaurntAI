using RestaurantAI.Infrastructure.Persistence.Redis;
using RestaurantAI.Mcp.Gateway.Audit;
using RestaurantAI.Mcp.Gateway.Authorization;
using RestaurantAI.Mcp.Gateway.Clients;
using RestaurantAI.Mcp.Gateway.Idempotency;
using RestaurantAI.Mcp.Gateway.Tools.Menu;
using RestaurantAI.Mcp.Gateway.Tools.Orders;
using RestaurantAI.Mcp.Gateway.Tools.Reservations;
using RestaurantAI.ServiceDefaults;
using RestaurantAI.ServiceDefaults.Http;

var builder = WebApplication.CreateBuilder(args);

// The MCP Gateway is the only path from AI agents to business capabilities.
// Every tool call is authenticated, authorized (User + Tenant + Branch + Role + Permission),
// made idempotent where it mutates state, and audited. Tools never touch the database.
builder.AddServiceDefaults();

builder.Services.AddRedisCache(builder.Configuration, instanceName: "mcp");
builder.Services.AddScoped<TenantContextResolver>();
builder.Services.AddSingleton<ToolPermissionService>();
builder.Services.AddScoped<McpAuthorizationService>();
builder.Services.AddSingleton<IdempotencyKeyGenerator>();
builder.Services.AddScoped<IdempotencyService>();
builder.Services.AddScoped<McpAuditService>();

builder.Services.AddTransient<ForwardCallerContextHandler>();
builder.Services.AddHttpClient<BusinessApiClient>(client =>
        client.BaseAddress = new Uri(builder.Configuration["Services:Gateway"] ?? "http://localhost:5080"))
    .AddHttpMessageHandler<ForwardCallerContextHandler>()
    .AddStandardResilienceHandler();

builder.Services.AddMcpServer()
    .WithHttpTransport(options => options.Stateless = true)
    .WithTools<GetMenuTool>()
    .WithTools<SearchMenuTool>()
    .WithTools<GetAvailabilityTool>()
    .WithTools<CreateOrderTool>()
    .WithTools<GetOrderTool>()
    .WithTools<CancelOrderTool>()
    .WithTools<CreateReservationTool>()
    .WithTools<GetReservationTool>()
    .WithTools<CancelReservationTool>();

var app = builder.Build();

app.UseServiceDefaults();
app.MapDefaultEndpoints();
app.MapMcp("/mcp").RequireAuthorization();

await app.RunAsync();

public partial class Program;
