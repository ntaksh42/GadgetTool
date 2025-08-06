# テストデータ作成スクリプト

このスクリプトは、Azure DevOpsのPersonalProjectにダミーのバグ票を45件作成し、チャート機能をテストするためのデータを準備します。

## 使用方法

### 1. PowerShellスクリプトの実行

```powershell
# パラメータを指定して実行
.\CreateDummyWorkItems.ps1 -Organization "your-org" -Project "PersonalProject" -PersonalAccessToken "your-pat"

# 例
.\CreateDummyWorkItems.ps1 -Organization "contoso" -Project "PersonalProject" -PersonalAccessToken "abc123xyz"
```

### 2. 必要な権限

Personal Access Token (PAT) に以下の権限が必要です：
- **Work Items**: Read & Write
- **Project and Team**: Read

### 3. 作成されるデータの内容

#### **機能別分類** (15種類)
- ユーザー管理、認証システム、レポート機能、データ同期、UI改善
- パフォーマンス、セキュリティ、バックアップ、通知システム、検索機能  
- ファイル管理、設定画面、監査ログ、エクスポート、インポート

#### **エリアパス** (10種類)
- PersonalProject\Frontend\UI
- PersonalProject\Frontend\Components
- PersonalProject\Backend\API
- PersonalProject\Backend\Database
- PersonalProject\Backend\Services
- PersonalProject\Infrastructure\Security
- PersonalProject\Infrastructure\Monitoring
- PersonalProject\Testing\Automation
- PersonalProject\Testing\Manual
- PersonalProject\Documentation

#### **優先度ランク** (重み付け)
- 1 (高優先度): 2件程度
- 2 (中優先度): 15件程度 
- 3 (低優先度): 20件程度
- 4 (最低優先度): 8件程度

#### **状態** 
- Active: 約27件 (60%)
- Resolved: 約9件 (20%)
- Closed: 約9件 (20%)

#### **タイトルパターン**
機能名の抽出テストのため、以下のパターンを使用：
- `[機能名] 問題内容`
- `機能名: 問題内容`
- `機能名 - 問題内容`
- `機能名_問題内容`

## テスト手順

1. スクリプト実行後、GadgetToolsのTicketManage機能でワークアイテムを取得
2. 「📊 Area Chart」ボタンをクリック
3. 以下の分類でチャートを確認：
   - **機能別**: 15の機能に分散されたデータ
   - **優先度別**: 4つの優先度ランクに分散されたデータ  
   - **エリア別**: 10のエリアに分散されたデータ
   - **状態別**: Active/Resolved/Closedの分布

## 注意事項

- API制限を考慮して200ms間隔で作成
- エラーが発生した場合は1秒待機後にリトライ
- 作成結果は画面に表示されます
- Azure DevOps Webで結果を確認できます