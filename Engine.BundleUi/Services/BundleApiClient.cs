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

    public async Task<IReadOnlyList<WorkflowDraftSummary>> GetWorkflowDraftsAsync(CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync("/workflow-drafts", cancellationToken);
        return await HandleResponse<IReadOnlyList<WorkflowDraftSummary>>(response, cancellationToken);
    }

    public async Task<WorkflowDraft> GetWorkflowDraftAsync(Guid draftId, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync($"/workflow-drafts/{draftId:D}", cancellationToken);
        return await HandleResponse<WorkflowDraft>(response, cancellationToken);
    }

    public async Task<IReadOnlyList<DraftScript>> GetWorkflowDraftScriptsAsync(Guid draftId, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync($"/workflow-drafts/{draftId:D}/scripts", cancellationToken);
        return await HandleResponse<IReadOnlyList<DraftScript>>(response, cancellationToken);
    }

    public async Task<DraftScript> UploadWorkflowDraftScriptAsync(
        Guid draftId,
        IBrowserFile file,
        string? scriptPath,
        CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream(10 * 1024 * 1024, cancellationToken);
        using var content = new MultipartFormDataContent();
        using var streamContent = new StreamContent(stream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
        content.Add(streamContent, "script", file.Name);
        if (!string.IsNullOrWhiteSpace(scriptPath))
        {
            content.Add(new StringContent(scriptPath.Trim()), "scriptPath");
        }

        using var response = await _httpClient.PostAsync($"/workflow-drafts/{draftId:D}/scripts", content, cancellationToken);
        return await HandleResponse<DraftScript>(response, cancellationToken);
    }

    public async Task DeleteWorkflowDraftScriptAsync(Guid draftId, string scriptPath, CancellationToken cancellationToken)
    {
        var escapedPath = string.Join('/',
            scriptPath.Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Select(Uri.EscapeDataString));
        using var response = await _httpClient.DeleteAsync(
            $"/workflow-drafts/{draftId:D}/scripts/{escapedPath}",
            cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new InvalidOperationException($"API request failed ({(int)response.StatusCode}): {body}");
    }

    public async Task<WorkflowDraftSummary> CreateWorkflowDraftAsync(WorkflowDraftDefinition definition, CancellationToken cancellationToken)
    {
        var payload = new JsonObject
        {
            ["definition"] = JsonNode.Parse(JsonSerializer.Serialize(definition, JsonOptions))
        };

        using var content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync("/workflow-drafts", content, cancellationToken);
        return await HandleResponse<WorkflowDraftSummary>(response, cancellationToken);
    }

    public async Task<WorkflowDraftSummary> UpdateWorkflowDraftAsync(
        Guid draftId,
        WorkflowDraftDefinition definition,
        CancellationToken cancellationToken)
    {
        var payload = new JsonObject
        {
            ["definition"] = JsonNode.Parse(JsonSerializer.Serialize(definition, JsonOptions))
        };

        using var content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");
        using var response = await _httpClient.PutAsync($"/workflow-drafts/{draftId:D}", content, cancellationToken);
        return await HandleResponse<WorkflowDraftSummary>(response, cancellationToken);
    }

    public async Task DeleteWorkflowDraftAsync(Guid draftId, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.DeleteAsync($"/workflow-drafts/{draftId:D}", cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new InvalidOperationException($"API request failed ({(int)response.StatusCode}): {body}");
    }

    public async Task<WorkflowVersionPublishResult> PublishWorkflowDraftAsync(Guid draftId, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsync($"/workflow-drafts/{draftId:D}/publish", null, cancellationToken);
        return await HandleResponse<WorkflowVersionPublishResult>(response, cancellationToken);
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

    public async Task RetryStepAsync(Guid instanceId, string stepId, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsync(
            $"/instances/{instanceId:D}/steps/{Uri.EscapeDataString(stepId)}/retry",
            null,
            cancellationToken);

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

    public async Task<IReadOnlyList<ApprovalRequest>> GetApprovalsAsync(ApprovalQuery query, CancellationToken cancellationToken)
    {
        var queryParts = new List<string>();
        AddQuery(queryParts, "status", query.Status);
        AddQuery(queryParts, "instanceId", query.InstanceId?.ToString("D"));
        AddQuery(queryParts, "workflowName", query.WorkflowName);
        AddQuery(queryParts, "assignee", query.Assignee);
        AddQuery(queryParts, "stepId", query.StepId);
        AddQuery(queryParts, "createdAfter", query.CreatedAfter?.ToString("O"));
        AddQuery(queryParts, "createdBefore", query.CreatedBefore?.ToString("O"));
        var uri = "/approvals" + BuildQuerySuffix(queryParts);
        using var response = await _httpClient.GetAsync(uri, cancellationToken);
        return await HandleResponse<IReadOnlyList<ApprovalRequest>>(response, cancellationToken);
    }

    public async Task<ApprovalRequest> ApproveAsync(Guid approvalId, string actor, string? comment, CancellationToken cancellationToken)
    {
        var payload = new JsonObject
        {
            ["actor"] = actor,
            ["comment"] = string.IsNullOrWhiteSpace(comment) ? null : comment
        };
        using var content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync($"/approvals/{approvalId:D}/approve", content, cancellationToken);
        return await HandleResponse<ApprovalRequest>(response, cancellationToken);
    }

    public async Task<ApprovalRequest> RejectAsync(Guid approvalId, string actor, string? comment, CancellationToken cancellationToken)
    {
        var payload = new JsonObject
        {
            ["actor"] = actor,
            ["comment"] = string.IsNullOrWhiteSpace(comment) ? null : comment
        };
        using var content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync($"/approvals/{approvalId:D}/reject", content, cancellationToken);
        return await HandleResponse<ApprovalRequest>(response, cancellationToken);
    }

    public async Task<ApprovalRequest> UpdateApprovalAsync(
        Guid approvalId,
        string? assignee,
        string? reason,
        DateTimeOffset? expiresAt,
        string actor,
        string? comment,
        CancellationToken cancellationToken)
    {
        var payload = new JsonObject
        {
            ["assignee"] = string.IsNullOrWhiteSpace(assignee) ? null : assignee,
            ["reason"] = string.IsNullOrWhiteSpace(reason) ? null : reason,
            ["expiresAt"] = expiresAt,
            ["actor"] = actor,
            ["comment"] = string.IsNullOrWhiteSpace(comment) ? null : comment
        };
        using var content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");
        using var response = await _httpClient.PatchAsync($"/approvals/{approvalId:D}", content, cancellationToken);
        return await HandleResponse<ApprovalRequest>(response, cancellationToken);
    }

    public async Task<IReadOnlyList<AuditEvent>> GetAuditEventsAsync(AuditQuery query, CancellationToken cancellationToken)
    {
        var queryParts = new List<string>();
        AddQuery(queryParts, "take", query.Take.ToString());
        AddQuery(queryParts, "instanceId", query.InstanceId?.ToString("D"));
        AddQuery(queryParts, "workflowName", query.WorkflowName);
        AddQuery(queryParts, "category", query.Category);
        AddQuery(queryParts, "action", query.Action);
        AddQuery(queryParts, "actor", query.Actor);
        AddQuery(queryParts, "createdAfter", query.CreatedAfter?.ToString("O"));
        AddQuery(queryParts, "createdBefore", query.CreatedBefore?.ToString("O"));
        var uri = "/audit" + BuildQuerySuffix(queryParts);
        using var response = await _httpClient.GetAsync(uri, cancellationToken);
        return await HandleResponse<IReadOnlyList<AuditEvent>>(response, cancellationToken);
    }

    private static void AddQuery(List<string> queryParts, string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        queryParts.Add($"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}");
    }

    private static string BuildQuerySuffix(IReadOnlyList<string> queryParts)
    {
        return queryParts.Count == 0 ? string.Empty : "?" + string.Join("&", queryParts);
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

    public sealed record WorkflowVersionPublishResult(string Name, int Version, int Revision);
}
