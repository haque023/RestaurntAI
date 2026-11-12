using RestaurantAI.Infrastructure.Idempotency;
using RestaurantAI.Infrastructure.Messaging;
using RestaurantAI.Infrastructure.Persistence.PostgreSQL;
using RestaurantAI.ServiceDefaults;
using RestaurantAI.ServiceDefaults.Http;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddOpenApi();
builder.AddApiControllers();

builder.Services.AddScoped<OrderWorkflow>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IOrderStatsService, OrderStatsService>();
builder.Services.AddScoped<IServiceRequestService, ServiceRequestService>();
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
app.MapControllers();

await app.RunAsync();

public partial class Program;
