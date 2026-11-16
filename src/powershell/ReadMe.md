# PowerShell demos

Scripts and reference material for exploring Microsoft Foundry model prices and agent responses.

## Files

| File | Description |
| --- | --- |
| [get-prices.ps1](./get-prices.ps1) | Retrieves Foundry Models retail prices from the public Azure Retail Prices API, follows pagination, and exports the original records as a sorted JSON array. Supports region, currency, and meter-name filters. Defaults to `westus2`, `USD`, and `prices.json` in the current working directory. No authentication is required. |
| [run-agent.ps1](./run-agent.ps1) | Creates a conversation and sends a question to a configured Foundry agent. Prints the text response and saves the full response to `response.json` alongside the script. |
| [available-azure-regions.md](./available-azure-regions.md) | Reference list of Azure region identifiers for choosing a region filter. Model and service availability varies by region. |

## Before running

- Use PowerShell with internet access.
- For `run-agent.ps1`, install Azure CLI and sign in with `az login` using an account with access to the Foundry project. Replace `$endpoint` and `$agentName` with your project endpoint and agent name, and adjust `$userMessage` as needed.
- Keep credentials and sensitive agent responses out of source control.
