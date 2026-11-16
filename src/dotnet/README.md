# GetFoundryModelUsage

Console app (.NET 10) that reports model usage for a Microsoft Foundry / Azure OpenAI resource: total requests, total token count, input tokens, output tokens, and an estimated cost — per model deployment, over a configurable time window.

## How it works

Usage data is **not** retrieved via the Agent Framework — it comes from **Azure Monitor Metrics** on the underlying `Microsoft.CognitiveServices/accounts` resource, queried with the `Azure.Monitor.Query` SDK:

| Reported value | Azure Monitor metric |
|---|---|
| Total requests | `ModelRequests` |
| Total token count | `TotalTokens` |
| Input tokens | `InputTokens` |
| Output tokens | `OutputTokens` |
| Estimated total cost | Not a native metric — computed from token counts × configured price rates |

Results are split per model deployment (`ModelDeploymentName` dimension) unless a specific deployment is requested.

These are Foundry account metrics covering multiple model providers, not project agent metrics. Do not substitute the Azure OpenAI-specific metrics (`AzureOpenAIRequests`, `TokenTransaction`, `ProcessedPromptTokens`, `GeneratedTokens`): they can omit requests and token usage for non-OpenAI models such as Claude and Mistral. If `FOUNDRY_RESOURCE_ID` ends in `/projects/<project-name>`, the app queries the parent account and prints a notice. Results cover the account, not only that project's activity; use `FOUNDRY_MODEL_DEPLOYMENT_NAME` to limit the report to a deployment. The app needs **Monitoring Reader** on the account even when a project ID is supplied.

Authentication is non-interactive, using an Entra ID app registration + client secret (`ClientSecretCredential`) — no user sign-in required.

## Setup (one-time, in order)

### 1. Create the Entra ID app registration

1. Azure portal → **Microsoft Entra ID** → **App registrations** → **New registration**.
2. Give it a name (e.g. `GetFoundryUsage`), leave the default supported account type, no redirect URI needed → **Register**.
3. Note the **Application (client) ID** and **Directory (tenant) ID** from the app's Overview page.
4. Left menu → **Certificates & secrets** → **Client secrets** → **New client secret** → set an expiry → **Add**.
5. Copy the secret **value** immediately (it's only shown once).

> Skip the "API permissions" blade — it's for OAuth scopes like Microsoft Graph and isn't used here. Access to Azure Monitor metrics is granted entirely via the RBAC role assignment below.

### 2. Grant the app read access to the resource (RBAC)

The credential alone doesn't authorize anything — the app also needs an Azure role assignment on the target resource:

1. Azure portal → navigate to the Cognitive Services / Foundry resource (or its resource group/subscription for broader scope).
2. Left menu → **Access control (IAM)** → **+ Add** → **Add role assignment**.
3. Role: search for and select **Monitoring Reader** → **Next**.
4. **Assign access to**: "User, group, or service principal" → **+ Select members** → search for the app registration by name (e.g. `GetFoundryUsage`) → select it → **Review + assign**.

### 3. Configure the app

1. Copy [.env.example](.env.example) to `.env`.
2. Fill in the values:

| Variable | Required | Description |
|---|---|---|
| `AZURE_TENANT_ID` | yes | Entra ID tenant ID |
| `AZURE_CLIENT_ID` | yes | App registration (client) ID |
| `AZURE_CLIENT_SECRET` | yes | App registration client secret |
| `FOUNDRY_RESOURCE_ID` | yes | Full ARM resource ID of the Cognitive Services/Foundry account or a project under it (queries the parent account) |
| `FOUNDRY_MODEL_DEPLOYMENT_NAME` | no | Limit results to one deployment; omit to see all deployments |
| `FOUNDRY_LOOKBACK_DAYS` | no | Time window in days (default `30`) |
| `FOUNDRY_INPUT_PRICE_PER_1K` | no | Price per 1,000 input tokens, for the cost estimate (default `0`) |
| `FOUNDRY_OUTPUT_PRICE_PER_1K` | no | Price per 1,000 output tokens, for the cost estimate (default `0`) |

`.env` is listed in `.gitignore` and must never be committed — it holds a live secret.

#### Price GPT‑5.4

Input: $2.50 per 1,000,000 tokens
Output: $15.00 per 1,000,000 tokens

~~~~conf
FOUNDRY_INPUT_PRICE_PER_1K=0.0025
FOUNDRY_OUTPUT_PRICE_PER_1K=0.015
~~~~

#### Price GPT‑5.4-mini

Input: $0.75 per 1,000,000 tokens
Output: $4.50 per 1,000,000 tokens

~~~~conf
FOUNDRY_INPUT_PRICE_PER_1K=0.00075
FOUNDRY_OUTPUT_PRICE_PER_1K=0.0045
~~~~

## Running the app

```powershell
dotnet run
```

Example output:

```
Subscription name:    Example subscription
Subscription ID:      00000000-0000-0000-0000-000000000000
Foundry project name: example-project
Foundry project ID:   /subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/example-rg/providers/Microsoft.CognitiveServices/accounts/example-account/projects/example-project

Using parent account for model usage: /subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/example-rg/providers/Microsoft.CognitiveServices/accounts/example-account
Results cover the account's deployments, not just the configured project.
Foundry model usage from 2026-10-16 to 2026-11-15 (UTC)
----------------------------------------------------------------------------------------------------
Model deployment: gpt-5.4
  Total requests:        1
  Total token count:     21.938
  Input tokens:          20.938
  Output tokens:         1.000
  Estimated total cost:  0,00 €
```

The subscription display name is fetched from Azure Resource Manager using the same app credential. Reading it requires subscription-level read permission (for example, **Reader**); if the lookup fails, the app prints a warning and still reports usage. Project name and full ARM ID come from `FOUNDRY_RESOURCE_ID`. If an account ID is configured without `/projects/<project-name>`, the project fields are shown as not specified; the app does not guess a project. Metrics remain account-level.

## How data is read

Token usage data (not auth tokens) is fetched from the **Azure Monitor Metrics API**, specifically via the `Azure.Monitor.Query` SDK's `MetricsQueryClient`.

**Flow in `Program.cs`:**

1. **Auth**: A `ClientSecretCredential` is built from `TenantId`/`ClientId`/`ClientSecret` (loaded from `.env` or real env vars via `AppConfig.LoadFromEnvironment()`). This credential is used by `MetricsQueryClient` to authenticate against Azure Monitor — it's a standard Azure AD client-credentials flow, not something manually parsed from a response.

2. **Metric query**:
   - `FOUNDRY_RESOURCE_ID` is validated and any project suffix is removed to select the parent account.
   - Metric names requested: `ModelRequests`, `TotalTokens`, `InputTokens`, `OutputTokens`, with the `Microsoft.CognitiveServices/accounts` metric namespace.
   - `metricsClient.QueryResourceAsync(resourceId, metricNames, options)` calls the Azure Monitor Query REST API (`Microsoft.Insights/metrics`) under the hood. Query failures are reported to stderr with a nonzero exit code.

3. **Parsing response**: The response's `Metrics` → `TimeSeries` → `Values` are summed per metric name and grouped by the named `ModelDeploymentName` dimension.

4. **Cost estimate**: Not from any API — it's computed locally as `tokens / 1000 * pricePerThousand`, using price rates read from `.env` (`FOUNDRY_INPUT_PRICE_PER_1K` / `FOUNDRY_OUTPUT_PRICE_PER_1K`).

Token *usage counts* come from Azure Monitor's metrics API against the Foundry resource; the auth *token* (OAuth) is obtained implicitly by `ClientSecretCredential` from Azure AD, not read/parsed manually anywhere in this code.

## Libraries

It uses two official Azure .NET SDKs:

1. **`Azure.Identity`** → provides `ClientSecretCredential` for authenticating against Azure AD (gets the OAuth token behind the scenes).
2. **`Azure.Monitor.Query`** → provides `MetricsQueryClient` for querying the Azure Monitor Metrics REST API (`QueryResourceAsync`). 

See `GetFoundryUsage.csproj`: the project references NuGet packages `Azure.Identity` (1.21.0) and `Azure.Monitor.Query` (1.7.1) — both part of the official Azure SDK for .NET. Authentication and metric querying are handled by these SDK clients. The subscription display name is retrieved separately with `HttpClient` from the Azure Resource Manager subscriptions API.

## Author

[Toni Pohl](https://github.com/tonipohl)
