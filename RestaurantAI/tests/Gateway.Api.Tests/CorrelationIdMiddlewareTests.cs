using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using RestaurantAI.Gateway.Api.Middleware;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.Gateway.Api.Tests;

public sealed class CorrelationIdMiddlewareTests
{
    [Theory]
    [InlineData("abc-123_DEF", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("has space", false)]
    [InlineData("<script>", false)]
    public void IsValid_accepts_only_safe_ids(string? value, bool expected) =>
        Assert.Equal(expected, CorrelationIdMiddleware.IsValid(value));

    [Fact]
    public void IsValid_rejects_overlong_ids() =>
        Assert.False(CorrelationIdMiddleware.IsValid(new string('a', CorrelationIdMiddleware.MaxLength + 1)));

    [Fact]
    public async Task Keeps_a_valid_incoming_correlation_id()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[PlatformHeaders.CorrelationId] = "client-supplied-1";
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask, NullLogger<CorrelationIdMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal("client-supplied-1", context.TraceIdentifier);
    }

    [Fact]
    public async Task Replaces_an_invalid_incoming_correlation_id()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[PlatformHeaders.CorrelationId] = "bad value!";
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask, NullLogger<CorrelationIdMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.NotEqual("bad value!", context.TraceIdentifier);
        Assert.True(CorrelationIdMiddleware.IsValid(context.TraceIdentifier));
    }
}
