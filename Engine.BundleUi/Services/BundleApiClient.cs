using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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

    public async Task<IReadOnlyList<WorkflowDefinitionMetadata>> GetRegisteredWorkflowsAsync(CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync("/workflows", cancellationToken);
        return await HandleResponse<IReadOnlyList<WorkflowDefinitionMetadata>>(response, cancellationToken);
    }

    public async Task<WorkflowInstanceChecklist> StartWorkflowInstanceAsync(
        string workflowName,
        JsonObject inputs,
        int? version,
        CancellationToken cancellationToken)
    {
        var payload = new JsonObject
        {
            ["inputs"] = inputs,
            ["version"] = version
        };

        using var content = new StringContent(
            payload.ToJsonString(),
            Encoding.UTF8,
            "application/json");

        using var response = await _httpClient.PostAsync($"/workflows/{Uri.EscapeDataString(workflowName)}/instances", content, cancellationToken);
        return await HandleResponse<WorkflowInstanceChecklist>(response, cancellationToken);
    }

    public async Task<WorkflowInstanceChecklist> GetWorkflowInstanceAsync(Guid instanceId, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync($"/instances/{instanceId:D}", cancellationToken);
        return await HandleResponse<WorkflowInstanceChecklist>(response, cancellationToken);
    }

    public async Task CancelWorkflowInstanceAsync(Guid instanceId, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsync($"/instances/{instanceId:D}/cancel", null, cancellationToken);
        if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Accepted)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new InvalidOperationException($"API request failed ({(int)response.StatusCode}): {body}");
    }

    public async Task<IReadOnlyList<StepExecutionLog>> GetStepExecutionLogsAsync(
        Guid instanceId,
        string stepId,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(
            $"/instances/{instanceId:D}/steps/{Uri.EscapeDataString(stepId)}/logs",
            cancellationToken);
        return await HandleResponse<IReadOnlyList<StepExecutionLog>>(response, cancellationToken);
    }

    public async Task<IReadOnlyList<WorkflowInstanceSummary>> GetWorkflowInstancesAsync(CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync("/instances", cancellationToken);
        return await HandleResponse<IReadOnlyList<WorkflowInstanceSummary>>(response, cancellationToken);
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
