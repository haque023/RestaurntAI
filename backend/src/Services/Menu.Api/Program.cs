using RestaurantAI.Infrastructure.Messaging;
using RestaurantAI.Infrastructure.Persistence.PostgreSQL;
using RestaurantAI.Infrastructure.Persistence.Redis;
using RestaurantAI.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddOpenApi();
builder.AddApiControllers();

builder.Services.AddScoped<MenuWorkflow>();
builder.Services.AddScoped<IMenuService, MenuService>();
builder.Services.AddScoped<IDishDiscoveryService, DishDiscoveryService>();
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
app.MapControllers();

await app.RunAsync();

public partial class Program;
