using RestaurantAI.Infrastructure.Messaging.RabbitMQ;
using RestaurantAI.Infrastructure.Persistence.PostgreSQL;
using RestaurantAI.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddOpenApi();
builder.AddApiControllers();

builder.Services.AddPostgresDbContext<PlatformDbContext>(builder.Configuration, PlatformDbContext.SchemaName);
builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection(RabbitMqOptions.SectionName));
builder.Services.AddHostedService<AuditEventConsumer>();

builder.Services.AddScoped<IAdminService, AdminService>();

var app = builder.Build();

app.UseServiceDefaults();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Local/dev convenience; production runs migrations from the deployment pipeline.
if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await app.Services.MigrateDatabaseAsync<PlatformDbContext>();
}

app.MapDefaultEndpoints();
app.MapControllers();

await app.RunAsync();

public partial class Program;
