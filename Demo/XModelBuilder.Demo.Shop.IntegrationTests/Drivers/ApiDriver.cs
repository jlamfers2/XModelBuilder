using System.Net.Http.Json;
using XModelBuilder.Demo.Shop.IntegrationTests.Contexts;
using XModelBuilder.Demo.Shop.IntegrationTests.Support.Infrastructure;

namespace XModelBuilder.Demo.Shop.IntegrationTests.Drivers;

/// <summary>
/// GENERIC driver base: HTTP + JSON plumbing plus authentication. It attaches the current user's
/// test-auth headers to every request and records the last response on the shared
/// <see cref="HttpResponseContext"/>. Specific drivers only express endpoints.
/// </summary>
public abstract class ApiDriver(HttpClient client, CurrentUserContext user, HttpResponseContext response)
{
    protected Task<ApiResponse> GetAsync(string url) => SendAsync(HttpMethod.Get, url);

    protected Task<ApiResponse> PostAsync(string url, object? body = null) => SendAsync(HttpMethod.Post, url, body);

    private async Task<ApiResponse> SendAsync(HttpMethod method, string url, object? body = null)
    {
        using var request = new HttpRequestMessage(method, url);

        if (user.IsAuthenticated)
        {
            request.Headers.Add(TestAuthHandler.EmailHeader, user.Email);
            if (!string.IsNullOrWhiteSpace(user.Role))
            {
                request.Headers.Add(TestAuthHandler.RoleHeader, user.Role);
            }
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, body.GetType());
        }

        using var httpResponse = await client.SendAsync(request);
        var raw = await httpResponse.Content.ReadAsStringAsync();

        var result = new ApiResponse(httpResponse.StatusCode, raw);
        response.Last = result;
        return result;
    }
}
