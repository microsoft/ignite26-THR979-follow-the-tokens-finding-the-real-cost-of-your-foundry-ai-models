<#
.SYNOPSIS
    Fetches retail prices for all Foundry Models meters from the public Azure Retail Prices API.

.DESCRIPTION
    Queries https://prices.azure.com/api/retail/prices (no authentication required) for every
    meter under serviceName 'Foundry Models', following pagination (NextPageLink) until all
    pages are retrieved. Results can be filtered by region/currency and exported to JSON.

.PARAMETER ArmRegionName
    Limit results to a single Azure region (e.g. 'eastus'). Omit or use -AllRegions to fetch every region.

.PARAMETER AllRegions
    Fetch prices across all regions instead of filtering by -ArmRegionName. This returns a much
    larger result set, since most meters are duplicated per region/data-zone.

.PARAMETER CurrencyCode
    Currency for returned prices (default 'USD').

.PARAMETER MeterNameContains
    Optional substring filter applied server-side to meterName (e.g. 'gpt-4o-mini').

.PARAMETER ExportJsonPath
    Optional path to export results as JSON.

.EXAMPLE
    ./prices.ps1 -ArmRegionName eastus -ExportJsonPath ./foundry-prices.json

.EXAMPLE
    ./prices.ps1 -AllRegions -MeterNameContains 'gpt-4o-mini' -ExportJsonPath ./gpt-4o-mini-prices.json
#>
s
param(
    [string] $ArmRegionName = 'westus2',
    [switch] $AllRegions,
    [string] $CurrencyCode = 'USD',
    [string] $MeterNameContains,
    [string] $ExportJsonPath = 'prices.json'
)

$ErrorActionPreference = 'Stop'

$filterClauses = @("serviceName eq 'Foundry Models'")
if (-not $AllRegions) {
    $filterClauses += "armRegionName eq '$ArmRegionName'"
}
if ($MeterNameContains) {
    $filterClauses += "contains(meterName, '$MeterNameContains')"
}
$filter = $filterClauses -join ' and '

# https://learn.microsoft.com/en-us/rest/api/cost-management/retail-prices/azure-retail-prices
# $uri = "$baseUri`?api-version=2023-01-01-preview&currencyCode='$CurrencyCode'&`$filter=$filter"
$baseUri = 'https://prices.azure.com/api/retail/prices'
$uri = "$baseUri`?currencyCode='$CurrencyCode'&`$filter=$filter"

$allItems = [System.Collections.Generic.List[object]]::new()
$pageCount = 0

Write-Host "Fetching Foundry Models prices (filter: $filter, currency: $CurrencyCode)..."

# Fetch all pages of Foundry Models prices from the Azure Retail Prices API
while ($uri) {
    $pageCount++
    Write-Host "  Page $pageCount..."
    $response = Invoke-RestMethod -Uri $uri -Method Get

    foreach ($item in $response.Items) {
        $allItems.Add($item)
    }

    $uri = $response.NextPageLink
}

Write-Host "Retrieved $($allItems.Count) price records across $pageCount page(s)."

# Export the retrieved price records to JSON if an export path is specified
if ($ExportJsonPath) {
    $json = ConvertTo-Json -InputObject @(
        $allItems | Sort-Object productName, skuName, meterName
    ) -Depth 5
    Set-Content -Path $ExportJsonPath -Value $json -Encoding utf8
    Write-Host "Exported JSON to $ExportJsonPath"
}
