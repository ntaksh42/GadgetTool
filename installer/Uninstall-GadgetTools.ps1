# GadgetTools アンインストーラー
# 管理者権限で実行してください

param(
    [string]$InstallPath = "$env:ProgramFiles\GadgetTools"
)

Write-Host "=== GadgetTools アンインストーラー ===" -ForegroundColor Red
Write-Host "削除対象: $InstallPath" -ForegroundColor Yellow

# 管理者権限チェック
if (-NOT ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
    Write-Host "エラー: このスクリプトは管理者権限で実行してください。" -ForegroundColor Red
    Write-Host "PowerShellを右クリック -> '管理者として実行' を選択してください。" -ForegroundColor Yellow
    Read-Host "Enterキーを押して終了..."
    exit 1
}

# 確認
Write-Host ""
Write-Host "GadgetToolsをアンインストールしますか？ (Y/N)" -ForegroundColor Yellow
$response = Read-Host

if ($response -ne "Y" -and $response -ne "y") {
    Write-Host "アンインストールをキャンセルしました。" -ForegroundColor Green
    Read-Host "Enterキーを押して終了..."
    exit 0
}

try {
    # プロセス終了
    Write-Host "GadgetToolsプロセスを終了中..." -ForegroundColor Cyan
    Get-Process -Name "GadgetTools" -ErrorAction SilentlyContinue | Stop-Process -Force

    # ショートカット削除
    Write-Host "ショートカットを削除中..." -ForegroundColor Cyan
    $desktopShortcut = Join-Path ([Environment]::GetFolderPath("Desktop")) "GadgetTools.lnk"
    $startMenuShortcut = Join-Path ([Environment]::GetFolderPath("CommonPrograms")) "GadgetTools.lnk"
    
    if (Test-Path $desktopShortcut) {
        Remove-Item $desktopShortcut -Force
    }
    
    if (Test-Path $startMenuShortcut) {
        Remove-Item $startMenuShortcut -Force
    }

    # レジストリ削除
    Write-Host "レジストリ情報を削除中..." -ForegroundColor Cyan
    $uninstallKey = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\GadgetTools"
    if (Test-Path $uninstallKey) {
        Remove-Item $uninstallKey -Recurse -Force
    }

    # インストールディレクトリ削除
    Write-Host "インストールディレクトリを削除中..." -ForegroundColor Cyan
    if (Test-Path $InstallPath) {
        Remove-Item $InstallPath -Recurse -Force
    }

    Write-Host ""
    Write-Host "=== アンインストール完了 ===" -ForegroundColor Green
    Write-Host "GadgetToolsが正常に削除されました。" -ForegroundColor Yellow

} catch {
    Write-Host "エラーが発生しました: $($_.Exception.Message)" -ForegroundColor Red
    Read-Host "Enterキーを押して終了..."
    exit 1
}

Write-Host "アンインストーラーを終了します。" -ForegroundColor Green
Read-Host "Enterキーを押して終了..."