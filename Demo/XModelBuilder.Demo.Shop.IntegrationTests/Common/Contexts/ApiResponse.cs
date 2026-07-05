using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace XModelBuilder.Demo.Shop.IntegrationTests.Common;

/// <summary>A minimal, deserializable view of an API response, shared between drivers and steps.</summary>
public sealed class ApiResponse(HttpStatusCode statusCode, string body)
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public HttpStatusCode StatusCode { get; } = statusCode;
    public string Body { get; } = body;
    public bool IsSuccess => (int)StatusCode is >= 200 and < 300;

    public T Read<T>() =>
        JsonSerializer.Deserialize<T>(Body, JsonOptions)
        ?? throw new InvalidOperationException($"Could not deserialize response body into {typeof(T).Name}: {Body}");
}
