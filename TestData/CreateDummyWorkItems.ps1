# Azure DevOps REST API を使用してダミーのワークアイテムを作成するスクリプト

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

# API URL
$baseUrl = "https://dev.azure.com/$Organization/$Project/_apis/wit/workitems"

# テスト用の機能名
$features = @(
    "UserManagement", "AuthSystem", "ReportFeature", "DataSync", "UIImprovement",
    "Performance", "Security", "Backup", "NotificationSystem", "SearchFeature",
    "FileManagement", "Settings", "AuditLog", "Export", "Import"
)

# エリアパス
$areaPaths = @(
    "$Project\Frontend\UI",
    "$Project\Frontend\Components", 
    "$Project\Backend\API",
    "$Project\Backend\Database",
    "$Project\Backend\Services",
    "$Project\Infrastructure\Security",
    "$Project\Infrastructure\Monitoring",
    "$Project\Testing\Automation",
    "$Project\Testing\Manual",
    "$Project\Documentation"
)

# 優先度 (1=高, 2=中, 3=低, 4=最低)
$priorities = @(1, 1, 2, 2, 2, 3, 3, 3, 4)

# 状態
$states = @("Active", "Active", "Active", "Resolved", "Closed")

# バグのタイトルテンプレート
$bugTitleTemplates = @(
    "[{0}] Login error occurs",
    "[{0}] Data not displayed correctly", 
    "[{0}] Save button not responsive",
    "[{0}] UI layout broken",
    "[{0}] Performance degradation",
    "{0}: Memory leak detected",
    "{0}: Exception handling improper",
    "{0}: Validation error",
    "{0}: Timeout occurs",
    "{0} - UI not working correctly",
    "{0} - Data integrity error",
    "{0} - Permission check fails",
    "{0}_API call error",
    "{0}_Database connection failed",
    "{0}_File read error"
)

# 詳細説明テンプレート
$descriptions = @(
    "Reproduction steps:<br/>1. Login to system<br/>2. Execute function<br/>3. Error occurs<br/><br/>Expected: Normal operation<br/>Actual: Error displayed",
    "Overview:<br/>System does not work correctly under specific conditions.<br/><br/>Impact:<br/>- Function unavailable<br/>- UX degradation",
    "Details:<br/>User expected behavior is not realized.<br/><br/>Reproduction conditions:<br/>- Specific data state<br/>- Specific user permissions<br/>- Specific browser environment",
    "Symptoms:<br/>System response becomes slow or returns unexpected results.<br/><br/>Response priority:<br/>Set according to user impact level"
)

Write-Host "Creating 45 dummy bug tickets in Azure DevOps PersonalProject..." -ForegroundColor Green
Write-Host "Organization: $Organization" -ForegroundColor Yellow
Write-Host "Project: $Project" -ForegroundColor Yellow

$createdCount = 0
$errors = @()

for ($i = 1; $i -le 45; $i++) {
    try {
        # ランダムにデータを選択
        $feature = Get-Random -InputObject $features
        $areaPath = Get-Random -InputObject $areaPaths  
        $priority = Get-Random -InputObject $priorities
        $state = Get-Random -InputObject $states
        $titleTemplate = Get-Random -InputObject $bugTitleTemplates
        $description = Get-Random -InputObject $descriptions
        
        # タイトル生成
        $title = $titleTemplate -f $feature
        
        # ワークアイテム作成用のJSON
        $workItemData = @(
            @{
                op = "add"
                path = "/fields/System.Title"
                value = $title
            },
            @{
                op = "add"
                path = "/fields/System.Description" 
                value = $description
            },
            @{
                op = "add"
                path = "/fields/System.AreaPath"
                value = $areaPath
            },
            @{
                op = "add"
                path = "/fields/Microsoft.VSTS.Common.Priority"
                value = $priority
            },
            @{
                op = "add"
                path = "/fields/System.State"
                value = $state
            },
            @{
                op = "add"
                path = "/fields/Microsoft.VSTS.Common.Severity"
                value = if($priority -le 2) { "2 - High" } elseif($priority -eq 3) { "3 - Medium" } else { "4 - Low" }
            }
        )

        $json = $workItemData | ConvertTo-Json -Depth 10
        $url = "$baseUrl/`$Bug?api-version=7.0"
        
        Write-Host "[$i/45] Creating: $title (Priority:$priority, Area:$($areaPath.Split('\')[-1]))" -ForegroundColor Cyan
        
        $response = Invoke-RestMethod -Uri $url -Method Post -Headers $headers -Body $json
        
        if ($response.id) {
            $createdCount++
            Write-Host "  Success - Created ID: $($response.id)" -ForegroundColor Green
        }
        
        # API制限を避けるため少し待機
        Start-Sleep -Milliseconds 200
        
    } catch {
        $errorMsg = "[$i/45] Error: $($_.Exception.Message)"
        $errors += $errorMsg
        Write-Host $errorMsg -ForegroundColor Red
        
        # エラーが続く場合は少し長めに待機
        Start-Sleep -Seconds 1
    }
}

Write-Host ""
Write-Host "============== Creation Results ==============" -ForegroundColor Green
Write-Host "Success: $createdCount items" -ForegroundColor Green
Write-Host "Errors: $($errors.Count) items" -ForegroundColor Red

if ($errors.Count -gt 0) {
    Write-Host ""
    Write-Host "Error Details:" -ForegroundColor Red
    $errors | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
}

Write-Host ""
Write-Host "Dummy data creation completed." -ForegroundColor Green
Write-Host "Check in Azure DevOps: https://dev.azure.com/$Organization/$Project/_workitems" -ForegroundColor Yellow