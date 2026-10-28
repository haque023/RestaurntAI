using RestaurantAI.Infrastructure.Messaging;
using RestaurantAI.Infrastructure.Persistence.PostgreSQL;
using RestaurantAI.Infrastructure.Persistence.Redis;
using RestaurantAI.Menu.Api.Endpoints;
using RestaurantAI.Menu.Api.Persistence;
using RestaurantAI.Menu.Application;
using RestaurantAI.Menu.Application.Menu;
using RestaurantAI.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddOpenApi();

builder.Services.AddMenuApplication();
builder.Services.AddPostgresDbContext<MenuDbContext>(
    builder.Configuration,
    MenuDbContext.SchemaName,
    configure: (provider, options) => options.UseOutbox(provider));
builder.Services.AddScoped<IMenuItemRepository, MenuItemRepository>();
builder.Services.AddRedisCache(builder.Configuration, instanceName: "menu");
builder.Services.AddRabbitMq(builder.Configuration);
builder.Services.AddOutbox<MenuDbContext>(builder.Configuration, module: "menu");

var app = builder.Build();

app.UseServiceDefaults();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Local/dev convenience; production runs migrations from the deployment pipeline.
if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await app.Services.MigrateDatabaseAsync<MenuDbContext>();
}

app.MapDefaultEndpoints();
app.MapMenuEndpoints();
app.MapDishDiscoveryEndpoints();

await app.RunAsync();

public partial class Program;
