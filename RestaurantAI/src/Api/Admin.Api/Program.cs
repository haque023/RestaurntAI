using RestaurantAI.Admin.Api.Endpoints;
using RestaurantAI.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseServiceDefaults();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapDefaultEndpoints();
app.MapAdminEndpoints();

await app.RunAsync();

public partial class Program;
