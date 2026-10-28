using RestaurantAI.Infrastructure.Idempotency;
using RestaurantAI.Infrastructure.Messaging;
using RestaurantAI.Infrastructure.Persistence.PostgreSQL;
using RestaurantAI.Order.Api.Endpoints;
using RestaurantAI.Order.Api.Integration;
using RestaurantAI.Order.Api.Persistence;
using RestaurantAI.Order.Application;
using RestaurantAI.Order.Application.Abstractions;
using RestaurantAI.ServiceDefaults;
using RestaurantAI.ServiceDefaults.Http;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddOpenApi();

builder.Services.AddOrderApplication();
builder.Services.AddPostgresDbContext<OrderDbContext>(
    builder.Configuration,
    OrderDbContext.SchemaName,
    configure: (provider, options) => options.UseOutbox(provider));
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddIdempotency<OrderDbContext>();
builder.Services.AddRabbitMq(builder.Configuration);
builder.Services.AddOutbox<OrderDbContext>(builder.Configuration, module: "order");

builder.Services.AddTransient<ForwardCallerContextHandler>();
builder.Services.AddHttpClient<IMenuCatalog, MenuCatalogClient>(client =>
        client.BaseAddress = new Uri(builder.Configuration["Services:Menu"] ?? "http://localhost:5204"))
    .AddHttpMessageHandler<ForwardCallerContextHandler>()
    .AddStandardResilienceHandler();

var app = builder.Build();

app.UseServiceDefaults();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Local/dev convenience; production runs migrations from the deployment pipeline.
if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await app.Services.MigrateDatabaseAsync<OrderDbContext>();
}

app.MapDefaultEndpoints();
app.MapOrderEndpoints();

await app.RunAsync();

public partial class Program;
