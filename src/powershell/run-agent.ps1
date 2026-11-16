<#
.SYNOPSIS
    Sends a question to the Azure AI Foundry Chat-Agent and saves the response.

.DESCRIPTION
    Uses the Azure CLI to obtain an access token, starts a conversation with the configured
    Azure AI Foundry agent, and submits the hard-coded question about the weather in SFO, CA.
    Prints the agent's text response and writes the full response payload to response.json
    in the script's directory. Requires Azure CLI authentication and access to the project.
    az login --tenant <your-tenant> --subscription <your-subscription>

.EXAMPLE
    ./run-agent.ps1
#>

$ErrorActionPreference = 'Stop'

$endpoint = 'https://<your-foundry-project>.services.ai.azure.com/api/projects/<your-foundry-project>'
$agentName = '<your-agent>'
$userMessage = 'Hi how is weather in SFO, CA?'

Write-Host 'Starting the app...'

$accessToken = az account get-access-token --resource 'https://ai.azure.com' --query accessToken -o tsv
if ($LASTEXITCODE -ne 0) {
    throw "Azure authentication failed. Run 'az login' before running this app."
}

$headers = @{ Authorization = "Bearer $accessToken" }
$baseUri = "$endpoint/openai/v1"

Write-Host "Sending message to $agentName..."

$conversation = Invoke-RestMethod `
    -Uri "$baseUri/conversations" `
    -Method Post `
    -Headers $headers `
    -ContentType 'application/json' `
    -Body '{}'

Write-Host "Getting response from $agentName..."

$body = @{
    agent_reference = @{
        type = 'agent_reference'
        name = $agentName
    }
    conversation = $conversation.id
    input = $userMessage
} | ConvertTo-Json -Depth 5

$response = Invoke-RestMethod `
    -Uri "$baseUri/responses" `
    -Method Post `
    -Headers $headers `
    -ContentType 'application/json' `
    -Body $body

$response.output |
    Where-Object type -eq 'message' |
    ForEach-Object {
        $_.content |
            Where-Object type -eq 'output_text' |
            ForEach-Object { Write-Output $_.text }
    }

$response |
    ConvertTo-Json -Depth 100 |
    Set-Content -Path (Join-Path $PSScriptRoot 'response.json')

# save the response to a file
Write-Host "Response written to: $(Join-Path $PSScriptRoot 'response.json')"
