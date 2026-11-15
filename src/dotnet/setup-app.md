# Set up the app

## 1. Create an app registration

In Microsoft Entra ID, create an app registration and generate a client secret.
Record the secret's expiry date so you can rotate it before it expires.

The app uses the client-credentials flow to request an Azure Resource Manager
token for:

```text
https://management.azure.com/.default
```

No Azure Resource Manager entry is needed under **API permissions** for this
flow. The app still needs an Azure role assignment on the target resource to
authorize its metrics query.

## 2. Grant access to the Azure resource

Assign the app's service principal the **Monitoring Reader** role. Keep the
assignment scope as narrow as possible—ideally, the specific Cognitive
Services or AI Foundry resource. A resource group or subscription can be used
if broader access is required.

In the Azure portal:

1. Open the target resource (or its resource group or subscription).
2. Select **Access control (IAM)**.
3. Select **Add** > **Add role assignment**.
4. Choose **Monitoring Reader**, then select **Next**.
5. Under **Assign access to**, choose **User, group, or service principal**.
6. Select **+ Select members**, find the app registration by name (for example,
   `GetFoundryUsage`), and select its service principal.
7. Select **Review + assign**.

The role grants read-only access to monitoring data. If using a custom role
instead, it must include `Microsoft.Insights/metrics/read`.

## 3. Configure credentials

The app needs the following values:

- `tenantId`
- `clientId`
- `clientSecret`

Store them securely; do not hardcode or commit them. Choose one of these
options:

- Environment variables
- `dotnet user-secrets` for local development
- Azure Key Vault

Using Key Vault avoids storing the secret in the app's files, but the app still
needs a credential to access the vault.

## Security recommendations

- Never commit the client secret to source control.
- Rotate secrets before they expire. Consider using a certificate with
  `ClientCertificateCredential` instead of a secret for a more secure,
  non-interactive flow supported by Azure.Identity.
- Limit the role assignment to the smallest practical scope and use
  **Monitoring Reader** (or an equivalent read-only custom role); write or
  control-plane access is not needed.

## Authentication and metrics

Use `ClientSecretCredential` in place of `DefaultAzureCredential` for this
client-credentials setup. The rest of the metrics flow can remain unchanged:
`MetricsQueryClient`, metric names, and cost estimation through the Retail
Prices API.
