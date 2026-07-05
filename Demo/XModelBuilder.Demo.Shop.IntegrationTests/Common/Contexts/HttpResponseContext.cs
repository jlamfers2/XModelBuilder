namespace XModelBuilder.Demo.Shop.IntegrationTests.Common;

/// <summary>The most recent API response, so generic "Then the request is rejected" steps can read it.</summary>
public sealed class HttpResponseContext
{
    public ApiResponse? Last { get; set; }

    public ApiResponse Require() =>
        Last ?? throw new InvalidOperationException("No API call has been made yet in this scenario.");
}
