using System.ComponentModel;
using GadgetTools.Core.Models;
using GadgetTools.Services;
using GadgetTools.Models;

namespace GadgetTools.Core.Services
{
    /// <summary>
    /// Azure DevOps設定の共通管理サービス
    /// </summary>
    public class AzureDevOpsConfigService : ViewModelBase
    {
        private static AzureDevOpsConfigService? _instance;
        public static AzureDevOpsConfigService Instance => _instance ??= new AzureDevOpsConfigService();

        private string _organization = string.Empty;
        private string _personalAccessToken = string.Empty;
        private bool _isConfigured = false;

        public string Organization
        {
            get => _organization;
            set
            {
                if (SetProperty(ref _organization, value))
                {
                    UpdateConfiguredStatus();
                    OnConfigurationChanged();
                }
            }
        }

        public string PersonalAccessToken
        {
            get => _personalAccessToken;
            set
            {
                if (SetProperty(ref _personalAccessToken, value))
                {
                    UpdateConfiguredStatus();
                    OnConfigurationChanged();
                }
            }
        }

        public bool IsConfigured
        {
            get => _isConfigured;
            private set => SetProperty(ref _isConfigured, value);
        }

        public event EventHandler? ConfigurationChanged;

        private AzureDevOpsConfigService()
        {
            LoadConfiguration();
        }

        private void UpdateConfiguredStatus()
        {
            IsConfigured = !string.IsNullOrWhiteSpace(Organization) && 
                          !string.IsNullOrWhiteSpace(PersonalAccessToken);
        }

        private void OnConfigurationChanged()
        {
            ConfigurationChanged?.Invoke(this, EventArgs.Empty);
        }

        public void LoadConfiguration()
        {
            try
            {
                var settings = SettingsService.LoadSettings();
                if (settings.AzureDevOps != null)
                {
                    _organization = settings.AzureDevOps.Organization;
                    if (!string.IsNullOrEmpty(settings.AzureDevOps.EncryptedPersonalAccessToken))
                    {
                        _personalAccessToken = SettingsService.DecryptString(settings.AzureDevOps.EncryptedPersonalAccessToken);
                    }
                    
                    OnPropertyChanged(nameof(Organization));
                    OnPropertyChanged(nameof(PersonalAccessToken));
                    UpdateConfiguredStatus();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Azure DevOps設定読み込みエラー: {ex.Message}");
            }
        }

        public void SaveConfiguration()
        {
            try
            {
                var settings = SettingsService.LoadSettings();
                
                if (settings.AzureDevOps == null)
                {
                    settings.AzureDevOps = new SettingsService.AzureDevOpsSettings();
                }

                settings.AzureDevOps.Organization = Organization.Trim();
                settings.AzureDevOps.EncryptedPersonalAccessToken = SettingsService.EncryptString(PersonalAccessToken);

                SettingsService.SaveSettings(settings);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Azure DevOps設定保存エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// 共通のAzureDevOpsConfigを作成
        /// </summary>
        public AzureDevOpsConfig CreateConfig(string project, string? repository = null)
        {
            return new AzureDevOpsConfig
            {
                Organization = Organization,
                Project = project,
                PersonalAccessToken = PersonalAccessToken
            };
        }

        /// <summary>
        /// 設定の妥当性を検証
        /// </summary>
        public (bool isValid, string errorMessage) ValidateConfiguration()
        {
            if (string.IsNullOrWhiteSpace(Organization))
            {
                return (false, "Azure DevOps組織名を入力してください。");
            }

            if (string.IsNullOrWhiteSpace(PersonalAccessToken))
            {
                return (false, "Personal Access Tokenを入力してください。");
            }

            return (true, string.Empty);
        }
    }
}