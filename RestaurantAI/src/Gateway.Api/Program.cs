using RestaurantAI.Gateway.Api.Configuration;
using RestaurantAI.Gateway.Api.Middleware;
using RestaurantAI.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

// YARP stays thin: routing, authentication, rate limiting, policies and correlation only.
// No business logic lives here.
builder.AddPlatformObservability();
builder.Services.AddGatewayAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddGatewayRateLimiting(builder.Configuration);
builder.Services.AddGatewayReverseProxy(builder.Configuration);

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<GatewayExceptionMiddleware>();
app.UseForwardedHeaders();

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapDefaultEndpoints();
app.MapReverseProxy();

await app.RunAsync();

public partial class Program;
