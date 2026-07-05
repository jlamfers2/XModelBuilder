using System.Net;
using System.Text.Json;
using XModelBuilder.Demo.Shop.Contracts;

namespace XModelBuilder.Demo.Shop.IntegrationTests.Contexts;

/// <summary>A minimal, deserializable view of an API response, shared between drivers and steps.</summary>
public sealed class ApiResponse(HttpStatusCode statusCode, string body)
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web) { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };

    public HttpStatusCode StatusCode { get; } = statusCode;
    public string Body { get; } = body;
    public bool IsSuccess => (int)StatusCode is >= 200 and < 300;

    public T Read<T>() =>
        JsonSerializer.Deserialize<T>(Body, JsonOptions)
        ?? throw new InvalidOperationException($"Could not deserialize response body into {typeof(T).Name}: {Body}");
}

/// <summary>Who is acting in the scenario right now. Set by the <c>AuthenticationDriver</c>.</summary>
public sealed class CurrentUserContext
{
    public string? Email { get; set; }
    public string? Role { get; set; }
    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(Email);
}

/// <summary>The most recent API response, so generic "Then the request is rejected" steps can read it.</summary>
public sealed class HttpResponseContext
{
    public ApiResponse? Last { get; set; }

    public ApiResponse Require() =>
        Last ?? throw new InvalidOperationException("No API call has been made yet in this scenario.");
}

/// <summary>The order(s) touched by the scenario.</summary>
public sealed class OrderContext
{
    public OrderResponse? Current { get; set; }

    public int CurrentId =>
        Current?.Id ?? throw new InvalidOperationException("No order has been placed yet in this scenario.");
}

/// <summary>The last product created by the scenario.</summary>
public sealed class CatalogContext
{
    public ProductResponse? LastCreated { get; set; }
}

/// <summary>
/// The AGGREGATE context: bundles the specific contexts so a step (or driver) that needs several of
/// them can take just this one. Steps that touch a single concern inject only the specific context.
/// </summary>
public sealed class ScenarioState(
    CurrentUserContext user,
    HttpResponseContext response,
    OrderContext order,
    CatalogContext catalog)
{
    public CurrentUserContext User => user;
    public HttpResponseContext Response => response;
    public OrderContext Order => order;
    public CatalogContext Catalog => catalog;
}
