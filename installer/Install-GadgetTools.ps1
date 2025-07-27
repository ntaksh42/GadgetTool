# GadgetTools 簡易インストーラー
# 管理者権限で実行してください

param(
    [string]$InstallPath = "$env:ProgramFiles\GadgetTools"
)

Write-Host "=== GadgetTools インストーラー ===" -ForegroundColor Green
Write-Host "インストール先: $InstallPath" -ForegroundColor Yellow

# 管理者権限チェック
if (-NOT ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
    Write-Host "エラー: このスクリプトは管理者権限で実行してください。" -ForegroundColor Red
    Write-Host "PowerShellを右クリック -> '管理者として実行' を選択してください。" -ForegroundColor Yellow
    Read-Host "Enterキーを押して終了..."
    exit 1
}

try {
    # インストールディレクトリ作成
    Write-Host "インストールディレクトリを作成中..." -ForegroundColor Cyan
    if (!(Test-Path $InstallPath)) {
        New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null
    }

    # ファイルコピー
    Write-Host "ファイルをコピー中..." -ForegroundColor Cyan
    $sourceDir = Join-Path $PSScriptRoot "..\bin\Release\net8.0-windows\win-x64\publish"
    
    if (!(Test-Path $sourceDir)) {
        Write-Host "エラー: パブリッシュファイルが見つかりません。" -ForegroundColor Red
        Write-Host "先に 'dotnet publish -c Release' を実行してください。" -ForegroundColor Yellow
        Read-Host "Enterキーを押して終了..."
        exit 1
    }

    Copy-Item -Path "$sourceDir\*" -Destination $InstallPath -Recurse -Force

    # デスクトップショートカット作成
    Write-Host "デスクトップショートカットを作成中..." -ForegroundColor Cyan
    $desktopPath = [Environment]::GetFolderPath("Desktop")
    $shortcutPath = Join-Path $desktopPath "GadgetTools.lnk"
    $targetPath = Join-Path $InstallPath "GadgetTools.exe"
    
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = $targetPath
    $shortcut.WorkingDirectory = $InstallPath
    $shortcut.Description = "Excel・Azure DevOpsデータをMarkdown、CSV、JSON、HTML形式に変換するツール"
    $shortcut.IconLocation = $targetPath
    $shortcut.Save()

    # スタートメニューショートカット作成
    Write-Host "スタートメニューショートカットを作成中..." -ForegroundColor Cyan
    $startMenuPath = [Environment]::GetFolderPath("CommonPrograms")
    $startMenuShortcut = Join-Path $startMenuPath "GadgetTools.lnk"
    
    $startShortcut = $shell.CreateShortcut($startMenuShortcut)
    $startShortcut.TargetPath = $targetPath
    $startShortcut.WorkingDirectory = $InstallPath
    $startShortcut.Description = "Excel・Azure DevOpsデータをMarkdown、CSV、JSON、HTML形式に変換するツール"
    $startShortcut.IconLocation = $targetPath
    $startShortcut.Save()

    # レジストリにアンインストール情報追加（オプション）
    Write-Host "アンインストール情報を登録中..." -ForegroundColor Cyan
    $uninstallKey = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\GadgetTools"
    New-Item -Path $uninstallKey -Force | Out-Null
    Set-ItemProperty -Path $uninstallKey -Name "DisplayName" -Value "GadgetTools"
    Set-ItemProperty -Path $uninstallKey -Name "DisplayVersion" -Value "1.0.0"
    Set-ItemProperty -Path $uninstallKey -Name "Publisher" -Value "GadgetTools"
    Set-ItemProperty -Path $uninstallKey -Name "InstallLocation" -Value $InstallPath
    Set-ItemProperty -Path $uninstallKey -Name "UninstallString" -Value "powershell.exe -ExecutionPolicy Bypass -File `"$InstallPath\Uninstall-GadgetTools.ps1`""
    Set-ItemProperty -Path $uninstallKey -Name "DisplayIcon" -Value $targetPath

    Write-Host ""
    Write-Host "=== インストール完了 ===" -ForegroundColor Green
    Write-Host "インストール先: $InstallPath" -ForegroundColor Yellow
    Write-Host "デスクトップとスタートメニューにショートカットが作成されました。" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "GadgetToolsを起動しますか？ (Y/N)" -ForegroundColor Cyan
    $response = Read-Host
    
    if ($response -eq "Y" -or $response -eq "y") {
        Start-Process $targetPath
    }

} catch {
    Write-Host "エラーが発生しました: $($_.Exception.Message)" -ForegroundColor Red
    Read-Host "Enterキーを押して終了..."
    exit 1
}

Write-Host "インストーラーを終了します。" -ForegroundColor Green
Read-Host "Enterキーを押して終了..."