using RestaurantAI.Infrastructure.Messaging;
using RestaurantAI.Infrastructure.Persistence.PostgreSQL;
using RestaurantAI.Restaurant.Api.Endpoints;
using RestaurantAI.Restaurant.Api.Persistence;
using RestaurantAI.Restaurant.Application;
using RestaurantAI.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddOpenApi();

builder.Services.AddRestaurantApplication();
builder.Services.AddPostgresDbContext<RestaurantDbContext>(
    builder.Configuration,
    RestaurantDbContext.SchemaName,
    configure: (provider, options) => options.UseOutbox(provider));
builder.Services.AddRabbitMq(builder.Configuration);
builder.Services.AddOutbox<RestaurantDbContext>(builder.Configuration, module: "restaurant");

var app = builder.Build();

app.UseServiceDefaults();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Local/dev convenience; production runs migrations from the deployment pipeline.
if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await app.Services.MigrateDatabaseAsync<RestaurantDbContext>();
}

app.MapDefaultEndpoints();
app.MapRestaurantEndpoints();
app.MapDiscoveryEndpoints();

await app.RunAsync();

public partial class Program;
