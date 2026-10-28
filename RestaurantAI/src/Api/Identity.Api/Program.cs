using RestaurantAI.Infrastructure.Messaging;
using RestaurantAI.Infrastructure.Persistence.PostgreSQL;
using RestaurantAI.Identity.Api.Endpoints;
using RestaurantAI.Identity.Api.Integration;
using RestaurantAI.Identity.Api.Persistence;
using RestaurantAI.Identity.Api.Security;
using RestaurantAI.Identity.Application;
using RestaurantAI.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddOpenApi();

builder.Services.AddIdentityApplication();
builder.Services.AddPostgresDbContext<IdentityDbContext>(
    builder.Configuration,
    IdentityDbContext.SchemaName,
    configure: (provider, options) => options.UseOutbox(provider));
builder.Services.AddRabbitMq(builder.Configuration);
builder.Services.AddOutbox<IdentityDbContext>(builder.Configuration, module: "identity");
builder.Services.AddSingleton<PasswordHasher>();
builder.Services.AddSingleton<PlatformTokenIssuer>();
builder.Services.AddHttpClient<PlatformServicesClient>(client =>
        client.BaseAddress = new Uri(builder.Configuration["Services:Gateway"] ?? "http://localhost:5080"))
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
    await app.Services.MigrateDatabaseAsync<IdentityDbContext>();
}

await BootstrapAdministrator.EnsureAsync(app.Services);

app.MapDefaultEndpoints();
app.MapIdentityEndpoints();
app.MapStaffEndpoints();
app.MapAdminIdentityEndpoints();

await app.RunAsync();

public partial class Program;
