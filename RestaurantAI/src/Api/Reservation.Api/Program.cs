using RestaurantAI.Infrastructure.Idempotency;
using RestaurantAI.Infrastructure.Messaging;
using RestaurantAI.Infrastructure.Persistence.PostgreSQL;
using RestaurantAI.Reservation.Api.Endpoints;
using RestaurantAI.Reservation.Api.Persistence;
using RestaurantAI.Reservation.Application;
using RestaurantAI.Reservation.Application.Reservations;
using RestaurantAI.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddOpenApi();

builder.Services.AddReservationApplication();
builder.Services.AddPostgresDbContext<ReservationDbContext>(
    builder.Configuration,
    ReservationDbContext.SchemaName,
    configure: (provider, options) => options.UseOutbox(provider));
builder.Services.AddScoped<IReservationRepository, ReservationRepository>();
builder.Services.AddIdempotency<ReservationDbContext>();
builder.Services.AddRabbitMq(builder.Configuration);
builder.Services.AddOutbox<ReservationDbContext>(builder.Configuration, module: "reservation");

var app = builder.Build();

app.UseServiceDefaults();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Local/dev convenience; production runs migrations from the deployment pipeline.
if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await app.Services.MigrateDatabaseAsync<ReservationDbContext>();
}

app.MapDefaultEndpoints();
app.MapReservationEndpoints();

await app.RunAsync();

public partial class Program;
