# Azure DevOpsのプロジェクトとエリア構造を確認するスクリプト

param(
    [Parameter(Mandatory=$true)]
    [string]$Organization,
    
    [Parameter(Mandatory=$true)]
    [string]$PersonalAccessToken
)

# Base64エンコードされたPAT
$base64AuthInfo = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes(":$PersonalAccessToken"))
$headers = @{
    Authorization = "Basic $base64AuthInfo"
    'Content-Type' = 'application/json'
}

Write-Host "Checking Azure DevOps Organization: $Organization" -ForegroundColor Green

try {
    # プロジェクト一覧を取得
    $projectsUrl = "https://dev.azure.com/$Organization/_apis/projects?api-version=7.0"
    Write-Host "Fetching projects..." -ForegroundColor Cyan
    
    $projectsResponse = Invoke-RestMethod -Uri $projectsUrl -Method Get -Headers $headers
    
    Write-Host ""
    Write-Host "============== Available Projects ==============" -ForegroundColor Green
    
    if ($projectsResponse.value.Count -eq 0) {
        Write-Host "No projects found in organization: $Organization" -ForegroundColor Red
        return
    }
    
    foreach ($project in $projectsResponse.value) {
        Write-Host "- Name: $($project.name)" -ForegroundColor White
        Write-Host "  ID: $($project.id)" -ForegroundColor Gray
        Write-Host "  URL: $($project.url)" -ForegroundColor Gray
        Write-Host "  State: $($project.state)" -ForegroundColor Gray
        Write-Host ""
        
        # 各プロジェクトのエリアパスを取得
        try {
            $areasUrl = "https://dev.azure.com/$Organization/$($project.name)/_apis/wit/classificationnodes/areas?`$depth=10&api-version=7.0"
            Write-Host "  Fetching area paths for project: $($project.name)" -ForegroundColor Cyan
            
            $areasResponse = Invoke-RestMethod -Uri $areasUrl -Method Get -Headers $headers
            
            Write-Host "  Available Area Paths:" -ForegroundColor Yellow
            
            function DisplayAreas($node, $depth = 0) {
                $indent = "  " + ("  " * $depth)
                Write-Host "$indent- $($node.name) (Path: $($node.path))" -ForegroundColor White
                
                if ($node.children) {
                    foreach ($child in $node.children) {
                        DisplayAreas $child ($depth + 1)
                    }
                }
            }
            
            DisplayAreas $areasResponse
            
        } catch {
            Write-Host "  Error fetching areas: $($_.Exception.Message)" -ForegroundColor Red
        }
        
        Write-Host "  ========================================" -ForegroundColor Gray
        Write-Host ""
    }
    
} catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Please check your organization name and Personal Access Token" -ForegroundColor Yellow
}