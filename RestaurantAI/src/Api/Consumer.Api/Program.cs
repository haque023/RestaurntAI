using RestaurantAI.Infrastructure.Messaging;
using RestaurantAI.Infrastructure.Persistence.PostgreSQL;
using RestaurantAI.Consumer.Api.Endpoints;
using RestaurantAI.Consumer.Api.Persistence;
using RestaurantAI.Consumer.Application;
using RestaurantAI.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddOpenApi();

builder.Services.AddConsumerApplication();
builder.Services.AddPostgresDbContext<ConsumerDbContext>(
    builder.Configuration,
    ConsumerDbContext.SchemaName,
    configure: (provider, options) => options.UseOutbox(provider));
builder.Services.AddRabbitMq(builder.Configuration);
builder.Services.AddOutbox<ConsumerDbContext>(builder.Configuration, module: "consumer");

var app = builder.Build();

app.UseServiceDefaults();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Local/dev convenience; production runs migrations from the deployment pipeline.
if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await app.Services.MigrateDatabaseAsync<ConsumerDbContext>();
}

app.MapDefaultEndpoints();
app.MapConsumerEndpoints();
app.MapFavoriteEndpoints();

await app.RunAsync();

public partial class Program;
