# Provisioning Checklist Engine (.NET 10)

Durable workflow orchestration for provisioning checklists with:

- dependency-driven ordering + parallelism
- long-running wait states resumed by external events
- outbox + DB queue worker semantics
- retries/backoff/timeouts and at-least-once activity execution
- bundle-based activity packaging (workflow + scripts in a zip)
- separate Blazor Server UI for bundle upload, preview, and registration

## Projects

- `Engine.Core`: workflow schema, validation, dependency graph, domain contracts.
- `Engine.Persistence`: EF Core persistence, repos, queue, outbox.
- `Engine.Runtime`: worker + runtime orchestration.
- `Engine.Activities`: activity runners (`RoutedActivityRunner`, `ScriptActivityRunner`, `LocalActivityRunner`).
- `Engine.Api`: FastEndpoints HTTP API and bundle endpoints.
- `Engine.BundleUi`: separate Blazor Server UI that calls `Engine.Api`.
- `Engine.Tests`: unit tests.

## Bundle Model

Bundle ZIP expected structure:

```text
workflow.json
scripts/
  create-project.sh
  create-service-accounts.sh
  finalize.sh
```

In `workflow.json`, each non-wait step uses `activityRef` as a script path relative to `scripts/` (for example `create-project.sh`).

Upload flow:

1. `POST /bundles/preview` (multipart zip)
2. API unpacks + validates and returns preview metadata:
   - workflow name/version/description/details
   - all steps and resolved scripts
   - file list
   - inferred execution plan
3. `POST /bundles/previews/{previewId}/register`
4. API stores the bundle and registers workflow definition, rewriting step refs to `bundle://{bundleId}/scripts/...`.

Runtime flow:

- worker executes bundle steps via `ScriptActivityRunner` by resolving `bundle://...` into persisted bundle storage.

### Script Output Contract

Script activities now use an output file (similar to GitHub Actions output handling):

- runner sets `OZ_OUTPUT` environment variable to a writable file path
- scripts append outputs as `key=value` lines
- runner reads `OZ_OUTPUT` and converts values into step outputs

Example:

```bash
echo "projectNumber=p-123456789" >> "$OZ_OUTPUT"
echo "serviceAccountCount=3" >> "$OZ_OUTPUT"
```

Multiline values are supported with heredoc syntax:

```bash
echo "notes<<EOF" >> "$OZ_OUTPUT"
echo "line 1" >> "$OZ_OUTPUT"
echo "line 2" >> "$OZ_OUTPUT"
echo "EOF" >> "$OZ_OUTPUT"
```

Compatibility: if `OZ_OUTPUT` is empty, the runner still falls back to parsing stdout JSON for older scripts.

### Script Input Parameters

For script steps, you can define ordered positional arguments in `workflow.json`:

```json
"scriptParameters": [
  { "name": "projectId", "required": true },
  { "name": "region", "required": false }
]
```

- `name` maps to a key in the step `inputs` object
- order in `scriptParameters` is the order passed to the script (`$1`, `$2`, ...)
- `required: true` is validated by the runner before script execution
- if `scriptParameters` is omitted, runner keeps legacy mode and passes request JSON path as first argument

Script console history:

- full stdout/stderr is stored per script step attempt
- logs are retained for 30 days and then purged by the runtime persistence path
- fetch via `GET /instances/{instanceId}/steps/{stepId}/logs`

## Run Locally

```bash
dotnet restore
dotnet build ProvisioningChecklistEngine.sln
dotnet test ProvisioningChecklistEngine.sln
```

Start API:

```bash
dotnet run --project Engine.Api --no-launch-profile
```

API URL: `http://localhost:5000`

Start Bundle UI (separate process):

```bash
dotnet run --project Engine.BundleUi --no-launch-profile
```

UI URL: `http://localhost:5000` by default for that app instance if run alone; if both are running, use launch profile ports or set `ASPNETCORE_URLS` explicitly.

For example:

```bash
ASPNETCORE_URLS=http://localhost:5000 dotnet run --project Engine.Api --no-launch-profile
ASPNETCORE_URLS=http://localhost:5100 dotnet run --project Engine.BundleUi --no-launch-profile
```

Then browse UI at `http://localhost:5100`.

Set UI API target in `Engine.BundleUi/appsettings.json`:

```json
"Api": {
  "BaseUrl": "http://localhost:5000"
}
```

## Bundle API Endpoints

- `POST /bundles/preview` (multipart form file field `bundle`)
- `GET /bundles/previews/{previewId}`
- `POST /bundles/previews/{previewId}/register`

Existing workflow endpoints remain:

- `GET /instances`
- `POST /workflows/{workflowName}/instances`
- `GET /instances/{instanceId}`
- `POST /instances/{instanceId}/cancel`
- `POST /instances/{instanceId}/steps/{stepId}/retry`
- `GET /instances/{instanceId}/steps/{stepId}/logs`
- `POST /events`

## Sample Bundle

Source sample bundle assets:

- `bundle-samples/provision-skeleton/workflow.json`
- `bundle-samples/provision-skeleton/scripts/*.sh`
- `bundle-samples/guided-inputs/workflow.json`
- `bundle-samples/guided-inputs/scripts/*.sh`

Create zip:

```bash
cd bundle-samples/provision-skeleton
zip -r ../../provision-skeleton-bundle.zip .
```

Preview + register with curl:

```bash
curl -s -X POST http://localhost:5000/bundles/preview \
  -F bundle=@provision-skeleton-bundle.zip

curl -s -X POST http://localhost:5000/bundles/previews/<PREVIEW_ID>/register
```

Start instance:

```bash
curl -s -X POST http://localhost:5000/workflows/provision-skeleton-bundle/instances \
  -H 'Content-Type: application/json' \
  -d '{"inputs":{"projectId":"demo-123"}}'
```

Resume wait step:

```bash
curl -s -X POST http://localhost:5000/events \
  -H 'Content-Type: application/json' \
  -d '{
    "eventId":"evt-approval-001",
    "eventType":"approval",
    "correlationKey":"<INSTANCE_ID>",
    "payload":{"approved":true}
  }'
```

Guided form sample (no wait step):

```bash
cd bundle-samples/guided-inputs
zip -r ../../guided-inputs-bundle.zip .

curl -s -X POST http://localhost:5000/bundles/preview \
  -F bundle=@guided-inputs-bundle.zip

curl -s -X POST http://localhost:5000/bundles/previews/<PREVIEW_ID>/register

curl -s -X POST http://localhost:5000/workflows/guided-inputs-bundle/instances \
  -H 'Content-Type: application/json' \
  -d '{
    "inputs":{
      "projectName":"platform-infra",
      "projectTier":"standard",
      "billingAccount":"000AAA-111BBB-222CCC",
      "instanceCount":3,
      "enableBudget":true,
      "labels":{"team":"platform","environment":"dev"},
      "regions":["us-central1","us-east1"]
    }
  }'
```

## Docker Compose

`docker-compose.yml` defines Postgres + API + UI for local dev and persists bundle storage with Docker volumes.

- API runs in SDK container via `dotnet run` (no image build required), which avoids Docker build-time NuGet restore issues in some enterprise environments.
- UI runs in SDK container via `dotnet run` and calls API over Docker network (`Api__BaseUrl=http://api:8080`).
- Bundle storage volumes are mounted at:
  - `/workspace/Engine.Api/App_Data/Bundles`
  - `/workspace/Engine.Api/App_Data/BundlePreviews`

Run with:

```bash
docker compose up
```

Endpoints:

- API: `http://localhost:8080`
- UI: `http://localhost:5100`
