@echo off
echo GadgetTools インストーラーを起動します...
echo 管理者権限が必要です。UACダイアログが表示されたら「はい」を選択してください。
echo.
powershell.exe -ExecutionPolicy Bypass -File "%~dp0Install-GadgetTools.ps1"
pause