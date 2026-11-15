using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.Monitor.Query;
using Azure.Monitor.Query.Models;

DotEnv.Load(Path.Combine(Directory.GetCurrentDirectory(), ".env"));

var config = AppConfig.LoadFromEnvironment();
string accountResourceId;
try
{
    accountResourceId = AppConfig.GetAccountResourceId(config.ResourceId);
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine(ex.Message);
    Environment.ExitCode = 2;
    return;
}

var isProject = !string.Equals(config.ResourceId.TrimEnd('/'), accountResourceId, StringComparison.OrdinalIgnoreCase);
var credential = new ClientSecretCredential(config.TenantId, config.ClientId, config.ClientSecret);
var metricsClient = new MetricsQueryClient(credential);

var resourceSegments = config.ResourceId.TrimEnd('/').Split('/');
var subscriptionId = resourceSegments[2];
var subscriptionName = await ResourceDetails.GetSubscriptionNameAsync(credential, subscriptionId);
Console.WriteLine($"Subscription name:    {subscriptionName ?? "(unavailable; see warning)"}");
Console.WriteLine($"Subscription ID:      {subscriptionId}");
Console.WriteLine($"Foundry project name: {(isProject ? resourceSegments[10] : "(not specified; account-level resource ID configured)")}");
Console.WriteLine($"Foundry project ID:   {(isProject ? config.ResourceId.TrimEnd('/') : "(not specified)")}");
Console.WriteLine();

if (isProject)
{
    Console.WriteLine($"Using parent account for model usage: {accountResourceId}");
    Console.WriteLine();
    // Console.WriteLine("Results cover the account's deployments, not just the configured project.");
}

var endTime = DateTimeOffset.UtcNow;
var startTime = endTime.AddDays(-config.LookbackDays);

// '*' splits results per model deployment when no specific deployment is requested
var dimensionFilter = string.IsNullOrWhiteSpace(config.ModelDeploymentName)
    ? "ModelDeploymentName eq '*'"
    : $"ModelDeploymentName eq '{config.ModelDeploymentName.Replace("'", "''")}'";

var options = new MetricsQueryOptions
{
    TimeRange = new QueryTimeRange(startTime, endTime),
    Granularity = TimeSpan.FromDays(1),
    Filter = dimensionFilter,
    MetricNamespace = "Microsoft.CognitiveServices/accounts",
};
options.Aggregations.Add(MetricAggregationType.Total);

// Define the metric names to query for model usage. We are interested in tracking requests and token usage for each model deployment.
var metricNames = new[] { "ModelRequests", "TotalTokens", "InputTokens", "OutputTokens" };

Response<MetricsQueryResult> response;
try
{
    //---------------------------------------------------------------------------------
    // Get model usage metrics for the specified account resource ID and metric names.
    //---------------------------------------------------------------------------------
    response = await metricsClient.QueryResourceAsync(accountResourceId, metricNames, options);
}
catch (RequestFailedException ex)
{
    Console.Error.WriteLine($"Model usage query failed ({ex.Status}, {ex.ErrorCode}): {ex.Message}");
    if (ex.Status == 403)
    {
        Console.Error.WriteLine($"Check that Monitoring Reader is assigned on the account: {accountResourceId}");
    }
    Environment.ExitCode = 1;
    return;
}

var usageByDeployment = new Dictionary<string, DeploymentUsage>();

// Initialize a dictionary to store usage metrics for each model deployment. Time series data for each metric and deployment.
foreach (var metric in response.Value.Metrics)
{
    // Iterate over each time series (30 days here) for the current metric. Each series corresponds to a specific model deployment.
    foreach (var series in metric.TimeSeries)
    {
        // Extract the model deployment name from the series metadata, like "gpt-5". If not available, use the configured deployment name or a default value.
        var deploymentName = series.Metadata
            .FirstOrDefault(kv => kv.Key.Equals("ModelDeploymentName", StringComparison.OrdinalIgnoreCase))
            .Value ?? config.ModelDeploymentName ?? "(all deployments)";
        // Calculate the total value for the current metric across all time series points.
        var total = series.Values.Sum(v => v.Total ?? 0);
        // Update the usage metrics for the current deployment and metric.
        if (!usageByDeployment.TryGetValue(deploymentName, out var usage))
        {
            usage = new DeploymentUsage();
            usageByDeployment[deploymentName] = usage;
        }

        switch (metric.Name)
        {
            case "ModelRequests": usage.TotalRequests += total; break;
            case "TotalTokens": usage.TotalTokens += total; break;
            case "InputTokens": usage.InputTokens += total; break;
            case "OutputTokens": usage.OutputTokens += total; break;
        }
    }
}

Console.WriteLine($"Foundry model usage from {startTime:yyyy-MM-dd} to {endTime:yyyy-MM-dd} (UTC)");
Console.WriteLine(new string('-', 100));

// Print the usage metrics for each model deployment in a readable format.
foreach (var (deploymentName, usage) in usageByDeployment.OrderBy(kv => kv.Key))
{
    // Estimated cost is not exposed by Azure Monitor; it's derived from configured price rates.
    var estimatedCost = ((decimal)usage.InputTokens / 1000m) * config.InputPricePerThousandTokens
        + ((decimal)usage.OutputTokens / 1000m) * config.OutputPricePerThousandTokens;

    Console.WriteLine($"Model deployment: {deploymentName}");
    Console.WriteLine($"  Total requests:        {usage.TotalRequests:N0}");
    Console.WriteLine($"  Total token count:     {usage.TotalTokens:N0}");
    Console.WriteLine($"  Input tokens:          {usage.InputTokens:N0}");
    Console.WriteLine($"  Output tokens:         {usage.OutputTokens:N0}");
    Console.WriteLine($"  Estimated total cost:  {estimatedCost:C2}");
    Console.WriteLine();
}

if (usageByDeployment.Count == 0)
{
    Console.WriteLine("No usage data found for the given resource, deployment and time range.");
}

sealed class DeploymentUsage
{
    public double TotalRequests { get; set; }
    public double TotalTokens { get; set; }
    public double InputTokens { get; set; }
    public double OutputTokens { get; set; }
}

static class ResourceDetails
{
    // Helper class to retrieve details about the Azure subscription.
    public static async Task<string?> GetSubscriptionNameAsync(TokenCredential credential, string subscriptionId)
    {
        //-------------------------------------------------------------------------------------------
        // Acquire an access token for the Azure Management API using the provided (app) credential.
        //-------------------------------------------------------------------------------------------
        var token = await credential.GetTokenAsync(
            new TokenRequestContext(["https://management.azure.com/.default"]), CancellationToken.None);
        using var client = new HttpClient();
        // Get the subscription name: Create an HTTP client to send the request to the Azure Management API.
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"https://management.azure.com/subscriptions/{Uri.EscapeDataString(subscriptionId)}?api-version=2022-12-01");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);

        try
        {
            // Send the request and ensure a successful response.
            using var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();
            // Parse the response content as JSON to extract the subscription display name.
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if (!document.RootElement.TryGetProperty("displayName", out var displayName)
                || displayName.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(displayName.GetString()))
            {
                throw new JsonException("The subscription response has no display name.");
            }

            return displayName.GetString();
        }
        catch (HttpRequestException ex)
        {
            Console.Error.WriteLine($"Could not read the subscription name: {ex.Message} Subscription-level read permission (for example, Reader) is needed to retrieve it. Continuing with usage data.");
            return null;
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"Could not parse the subscription details: {ex.Message} Continuing with usage data.");
            return null;
        }
    }
}

sealed class AppConfig
{
    public static string GetAccountResourceId(string resourceId)
    {
        // Validate and extract the account resource ID from the full resource ID.
        var segments = resourceId.TrimEnd('/').Split('/');
        if ((segments.Length != 9 && segments.Length != 11)
            || segments[0].Length != 0
            || !segments[1].Equals("subscriptions", StringComparison.OrdinalIgnoreCase)
            || !segments[3].Equals("resourceGroups", StringComparison.OrdinalIgnoreCase)
            || !segments[5].Equals("providers", StringComparison.OrdinalIgnoreCase)
            || !segments[6].Equals("Microsoft.CognitiveServices", StringComparison.OrdinalIgnoreCase)
            || !segments[7].Equals("accounts", StringComparison.OrdinalIgnoreCase)
            || segments.Skip(1).Any(string.IsNullOrWhiteSpace)
            || (segments.Length == 11 && !segments[9].Equals("projects", StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException("FOUNDRY_RESOURCE_ID must be a full Microsoft.CognitiveServices/accounts resource ID, optionally followed by /projects/<project-name>.");
        }

        return string.Join("/", segments.Take(9));
    }

    public required string TenantId { get; init; }
    public required string ClientId { get; init; }
    public required string ClientSecret { get; init; }
    public required string ResourceId { get; init; }
    public string? ModelDeploymentName { get; init; }
    public int LookbackDays { get; init; } = 30;
    public decimal InputPricePerThousandTokens { get; init; }
    public decimal OutputPricePerThousandTokens { get; init; }

    public static AppConfig LoadFromEnvironment()
    {
        // Load environment variables from the .env file if present.
        return new AppConfig
        {
            TenantId = RequireEnv("AZURE_TENANT_ID"),
            ClientId = RequireEnv("AZURE_CLIENT_ID"),
            ClientSecret = RequireEnv("AZURE_CLIENT_SECRET"),
            ResourceId = RequireEnv("FOUNDRY_RESOURCE_ID"),
            ModelDeploymentName = Environment.GetEnvironmentVariable("FOUNDRY_MODEL_DEPLOYMENT_NAME"),
            LookbackDays = int.TryParse(Environment.GetEnvironmentVariable("FOUNDRY_LOOKBACK_DAYS"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var days) ? days : 30,
            // .env values always use '.' as the decimal point, so parsing must be culture-invariant regardless of OS locale.
            InputPricePerThousandTokens = decimal.TryParse(Environment.GetEnvironmentVariable("FOUNDRY_INPUT_PRICE_PER_1K"), NumberStyles.Number, CultureInfo.InvariantCulture, out var inPrice) ? inPrice : 0m,
            OutputPricePerThousandTokens = decimal.TryParse(Environment.GetEnvironmentVariable("FOUNDRY_OUTPUT_PRICE_PER_1K"), NumberStyles.Number, CultureInfo.InvariantCulture, out var outPrice) ? outPrice : 0m,
        };
    }

    private static string RequireEnv(string name) =>
        Environment.GetEnvironmentVariable(name)
        ?? throw new InvalidOperationException($"Required environment variable '{name}' is not set. Set it in .env or as a real environment variable.");
}

static class DotEnv
{
    // Only sets values not already present, so real environment variables always win over the file.
    public static void Load(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim().Trim('"');

            if (Environment.GetEnvironmentVariable(key) is null)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }
}
