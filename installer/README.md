# GadgetTools インストーラー

## インストール方法

### 方法1: バッチファイルを使用（推奨）
1. `Install.bat` を右クリック
2. "管理者として実行" を選択
3. 画面の指示に従ってインストール

### 方法2: PowerShellスクリプトを直接実行
1. PowerShellを管理者として実行
2. 以下のコマンドを実行:
   ```powershell
   Set-ExecutionPolicy Bypass -Scope Process
   .\Install-GadgetTools.ps1
   ```

## インストール内容

- **インストール先**: `C:\Program Files\GadgetTools\`
- **デスクトップショートカット**: 作成されます
- **スタートメニュー**: ショートカットが作成されます
- **アンインストール情報**: Windowsの「プログラムと機能」に登録

## アンインストール

### 方法1: Windowsの設定から
1. 設定 → アプリ → インストールされているアプリ
2. "GadgetTools" を検索してアンインストール

### 方法2: アンインストーラーを直接実行
1. `Uninstall-GadgetTools.ps1` を管理者として実行

## 注意事項

- インストールには管理者権限が必要です
- ウイルス対策ソフトが警告を出す場合がありますが、安全なファイルです
- 初回実行時にWindows Defenderの警告が表示される場合があります

## トラブルシューティング

**PowerShellの実行ポリシーエラーが出る場合:**
```powershell
Set-ExecutionPolicy Bypass -Scope CurrentUser
```

**インストールに失敗する場合:**
- 管理者権限で実行しているか確認
- ウイルス対策ソフトを一時的に無効化
- インストール先フォルダが使用中でないか確認