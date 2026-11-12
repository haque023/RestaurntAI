using RestaurantAI.Infrastructure.Messaging;
using RestaurantAI.Infrastructure.Persistence.PostgreSQL;
using RestaurantAI.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddOpenApi();
builder.AddApiControllers();

builder.Services.AddScoped<ILoyaltyService, LoyaltyService>();
builder.Services.AddPostgresDbContext<LoyaltyDbContext>(
    builder.Configuration,
    LoyaltyDbContext.SchemaName,
    configure: (provider, options) => options.UseOutbox(provider));
builder.Services.AddRabbitMq(builder.Configuration);
builder.Services.AddOutbox<LoyaltyDbContext>(builder.Configuration, module: "loyalty");
builder.Services.AddScoped<ILoyaltyAccountRepository, LoyaltyAccountRepository>();

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
app.MapControllers();

await app.RunAsync();

public partial class Program;
