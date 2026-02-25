using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Engine.BundleUi.Models;
using Microsoft.AspNetCore.Components.Forms;

namespace Engine.BundleUi.Services;

public sealed class BundleApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;

    public BundleApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<BundlePreviewResponse> PreviewBundleAsync(IBrowserFile file, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream(50 * 1024 * 1024, cancellationToken);

        using var content = new MultipartFormDataContent();
        using var streamContent = new StreamContent(stream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
        content.Add(streamContent, "bundle", file.Name);

        using var response = await _httpClient.PostAsync("/bundles/preview", content, cancellationToken);
        return await HandleResponse<BundlePreviewResponse>(response, cancellationToken);
    }

    public async Task<BundleRegisterResponse> RegisterPreviewAsync(string previewId, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsync($"/bundles/previews/{previewId}/register", null, cancellationToken);
        return await HandleResponse<BundleRegisterResponse>(response, cancellationToken);
    }

    private static async Task<T> HandleResponse<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            var payload = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
            if (payload is null)
            {
                throw new InvalidOperationException("API returned empty response body.");
            }

            return payload;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            try
            {
                var error = JsonSerializer.Deserialize<ApiError>(body, JsonOptions);
                if (!string.IsNullOrWhiteSpace(error?.Message))
                {
                    throw new InvalidOperationException(error.Message);
                }
            }
            catch (JsonException)
            {
            }
        }

        throw new InvalidOperationException($"API request failed ({(int)response.StatusCode}): {body}");
    }

    private sealed record ApiError(string Message);
}
