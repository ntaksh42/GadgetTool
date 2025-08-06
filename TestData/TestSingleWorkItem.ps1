# シンプルなワークアイテムを1つ作成してテストするスクリプト

param(
    [Parameter(Mandatory=$true)]
    [string]$Organization,
    
    [Parameter(Mandatory=$true)]
    [string]$Project,
    
    [Parameter(Mandatory=$true)]
    [string]$PersonalAccessToken
)

# Base64エンコードされたPAT
$base64AuthInfo = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes(":$PersonalAccessToken"))
$headers = @{
    Authorization = "Basic $base64AuthInfo"
    'Content-Type' = 'application/json-patch+json'
}

Write-Host "Testing work item creation in: $Organization/$Project" -ForegroundColor Green

try {
    # まずワークアイテムタイプを確認
    $witUrl = "https://dev.azure.com/$Organization/$Project/_apis/wit/workitemtypes?api-version=7.0"
    Write-Host "Fetching available work item types..." -ForegroundColor Cyan
    
    $witResponse = Invoke-RestMethod -Uri $witUrl -Method Get -Headers $headers
    
    Write-Host "Available Work Item Types:" -ForegroundColor Yellow
    foreach ($wit in $witResponse.value) {
        Write-Host "- $($wit.name)" -ForegroundColor White
    }
    
    # 最初に見つかったワークアイテムタイプを使用
    $workItemType = $witResponse.value[0].name
    Write-Host ""
    Write-Host "Using work item type: $workItemType" -ForegroundColor Green
    
    # シンプルなワークアイテムを作成
    $workItemData = @(
        @{
            op = "add"
            path = "/fields/System.Title"
            value = "[TestFeature] Sample bug for testing"
        },
        @{
            op = "add" 
            path = "/fields/System.Description"
            value = "This is a test work item created by script"
        }
    )

    $json = $workItemData | ConvertTo-Json -Depth 10
    $url = "https://dev.azure.com/$Organization/$Project/_apis/wit/workitems/`$$workItemType" + "?api-version=7.0"
    
    Write-Host "Creating test work item..." -ForegroundColor Cyan
    Write-Host "URL: $url" -ForegroundColor Gray
    
    $response = Invoke-RestMethod -Uri $url -Method Post -Headers $headers -Body $json
    
    Write-Host ""
    Write-Host "✓ SUCCESS!" -ForegroundColor Green
    Write-Host "Created work item ID: $($response.id)" -ForegroundColor Green
    Write-Host "Title: $($response.fields.'System.Title')" -ForegroundColor White
    Write-Host "Type: $($response.fields.'System.WorkItemType')" -ForegroundColor White
    Write-Host "State: $($response.fields.'System.State')" -ForegroundColor White
    
} catch {
    Write-Host ""
    Write-Host "✗ ERROR:" -ForegroundColor Red
    Write-Host "Message: $($_.Exception.Message)" -ForegroundColor Red
    
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $responseBody = $reader.ReadToEnd()
        Write-Host "Response: $responseBody" -ForegroundColor Yellow
    }
}