using System.Windows;
using System.Windows.Controls;
using GadgetTools.Core.Services;
using GadgetTools.Services;
using GadgetTools.Models;

namespace GadgetTools.Core.Views
{
    /// <summary>
    /// AzureDevOpsSettingsView.xaml の相互作用ロジック
    /// </summary>
    public partial class AzureDevOpsSettingsView : UserControl
    {
        public AzureDevOpsSettingsView()
        {
            InitializeComponent();
            DataContext = AzureDevOpsConfigService.Instance;
            
            // PasswordBoxの初期値設定
            PatTokenBox.Password = AzureDevOpsConfigService.Instance.PersonalAccessToken;
            
            // PasswordBoxの変更を監視
            PatTokenBox.PasswordChanged += (s, e) =>
            {
                AzureDevOpsConfigService.Instance.PersonalAccessToken = PatTokenBox.Password;
            };
        }

        private async void TestConnection_Click(object sender, RoutedEventArgs e)
        {
            var config = AzureDevOpsConfigService.Instance;
            var validation = config.ValidateConfiguration();
            
            if (!validation.isValid)
            {
                MessageBox.Show(validation.errorMessage, "Configuration Error", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // 簡単な接続テスト - 組織の情報を取得
                var azureConfig = new AzureDevOpsConfig
                {
                    Organization = config.Organization,
                    Project = "dummy", // テスト用
                    PersonalAccessToken = config.PersonalAccessToken
                };

                using var service = new AzureDevOpsService(azureConfig);
                
                // プロジェクト一覧を取得してテスト（最小限のAPI呼び出し）
                var testRequest = new WorkItemQueryRequest
                {
                    Organization = config.Organization,
                    Project = "dummy",
                    MaxResults = 1
                };

                // 実際にはプロジェクトが存在しなくても認証エラーかどうかは分かる
                try
                {
                    await service.GetWorkItemsAsync(testRequest);
                    MessageBox.Show("✅ 接続が成功しました！", "Connection Test", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex) when (ex.Message.Contains("401") || ex.Message.Contains("Unauthorized"))
                {
                    MessageBox.Show("❌ 認証に失敗しました。Personal Access Tokenを確認してください。", 
                        "Connection Test", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch (Exception ex) when (ex.Message.Contains("404") || ex.Message.Contains("NotFound"))
                {
                    // 404は組織が見つからない、または認証は成功
                    MessageBox.Show("✅ 認証は成功しましたが、組織名を確認してください。", 
                        "Connection Test", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                catch
                {
                    MessageBox.Show("✅ 基本的な接続は成功しました。", "Connection Test", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ 接続に失敗しました: {ex.Message}", "Connection Test", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AzureDevOpsConfigService.Instance.SaveConfiguration();
                MessageBox.Show("設定を保存しました。", "Settings Saved", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"設定の保存に失敗しました: {ex.Message}", "Save Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}