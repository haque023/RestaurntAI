using RestaurantAI.Infrastructure.Messaging;
using RestaurantAI.Infrastructure.Persistence.PostgreSQL;
using RestaurantAI.Loyalty.Api.Endpoints;
using RestaurantAI.Loyalty.Api.Persistence;
using RestaurantAI.Loyalty.Application;
using RestaurantAI.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddOpenApi();

builder.Services.AddLoyaltyApplication();
builder.Services.AddPostgresDbContext<LoyaltyDbContext>(
    builder.Configuration,
    LoyaltyDbContext.SchemaName,
    configure: (provider, options) => options.UseOutbox(provider));
builder.Services.AddRabbitMq(builder.Configuration);
builder.Services.AddOutbox<LoyaltyDbContext>(builder.Configuration, module: "loyalty");
builder.Services.AddScoped<RestaurantAI.Loyalty.Application.Loyalty.ILoyaltyAccountRepository, LoyaltyAccountRepository>();

var app = builder.Build();

app.UseServiceDefaults();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Local/dev convenience; production runs migrations from the deployment pipeline.
if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await app.Services.MigrateDatabaseAsync<LoyaltyDbContext>();
}

app.MapDefaultEndpoints();
app.MapLoyaltyEndpoints();

await app.RunAsync();

public partial class Program;
