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
        
        // Query Settings
        private string _workItemType = "All";
        private string _state = "All";
        private int _maxResults = 50;
        private int _highlightDays = 7;
        private bool _enableHighlight = true;
        
        // Display Settings
        private bool _detailedMarkdown = true;

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

        // Query Settings Properties
        public string WorkItemType
        {
            get => _workItemType;
            set => SetProperty(ref _workItemType, value);
        }

        public string State
        {
            get => _state;
            set => SetProperty(ref _state, value);
        }

        public int MaxResults
        {
            get => _maxResults;
            set => SetProperty(ref _maxResults, value);
        }

        public int HighlightDays
        {
            get => _highlightDays;
            set => SetProperty(ref _highlightDays, value);
        }

        public bool EnableHighlight
        {
            get => _enableHighlight;
            set => SetProperty(ref _enableHighlight, value);
        }

        // Display Settings Properties
        public bool DetailedMarkdown
        {
            get => _detailedMarkdown;
            set => SetProperty(ref _detailedMarkdown, value);
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
                    
                    // Load query settings
                    _workItemType = settings.AzureDevOps.WorkItemType;
                    _state = settings.AzureDevOps.State;
                    _maxResults = settings.AzureDevOps.MaxResults;
                    _highlightDays = settings.AzureDevOps.HighlightDays;
                    _enableHighlight = settings.AzureDevOps.EnableHighlight;
                    
                    // Load display settings
                    _detailedMarkdown = settings.AzureDevOps.DetailedMarkdown;
                    
                    // Notify all properties changed
                    OnPropertyChanged(nameof(Organization));
                    OnPropertyChanged(nameof(PersonalAccessToken));
                    OnPropertyChanged(nameof(WorkItemType));
                    OnPropertyChanged(nameof(State));
                    OnPropertyChanged(nameof(MaxResults));
                    OnPropertyChanged(nameof(HighlightDays));
                    OnPropertyChanged(nameof(EnableHighlight));
                    OnPropertyChanged(nameof(DetailedMarkdown));
                    
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

                // Connection settings
                settings.AzureDevOps.Organization = Organization.Trim();
                settings.AzureDevOps.EncryptedPersonalAccessToken = SettingsService.EncryptString(PersonalAccessToken);

                // Query settings
                settings.AzureDevOps.WorkItemType = WorkItemType;
                settings.AzureDevOps.State = State;
                settings.AzureDevOps.MaxResults = MaxResults;
                settings.AzureDevOps.HighlightDays = HighlightDays;
                settings.AzureDevOps.EnableHighlight = EnableHighlight;

                // Display settings
                settings.AzureDevOps.DetailedMarkdown = DetailedMarkdown;

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