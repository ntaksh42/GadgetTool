using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Runtime.Versioning;

namespace GadgetTools.Plugins.PullRequestManagement
{
    /// <summary>
    /// 暗号化サービスのインターフェース
    /// </summary>
    public interface IEncryptionService
    {
        string Encrypt(string plainText);
        string Decrypt(string encryptedText);
    }

    /// <summary>
    /// Windows専用の暗号化サービス
    /// </summary>
    [SupportedOSPlatform("windows")]
    public class WindowsEncryptionService : IEncryptionService
    {
        public string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return string.Empty;

            try
            {
                var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
                var encryptedBytes = ProtectedData.Protect(plainTextBytes, null, DataProtectionScope.CurrentUser);
                return Convert.ToBase64String(encryptedBytes);
            }
            catch
            {
                // If encryption fails, return original text (fallback)
                return plainText;
            }
        }

        public string Decrypt(string encryptedText)
        {
            if (string.IsNullOrEmpty(encryptedText))
                return string.Empty;

            try
            {
                var encryptedBytes = Convert.FromBase64String(encryptedText);
                var decryptedBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(decryptedBytes);
            }
            catch
            {
                // If decryption fails, return original text (might be unencrypted)
                return encryptedText;
            }
        }
    }

    /// <summary>
    /// プラグイン設定サービスのインターフェース
    /// </summary>
    public interface IPullRequestSettingsService
    {
        Task<PullRequestManagementSettings> LoadSettingsAsync();
        Task SaveSettingsAsync(PullRequestManagementSettings settings);
    }

    /// <summary>
    /// プラグイン設定サービス
    /// </summary>
    public class PullRequestSettingsService : IPullRequestSettingsService
    {
        private readonly string _settingsFilePath;
        private readonly IEncryptionService _encryptionService;

        public PullRequestSettingsService() : this(new WindowsEncryptionService())
        {
        }

        public PullRequestSettingsService(IEncryptionService encryptionService)
        {
            _encryptionService = encryptionService;
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appFolder = Path.Combine(appDataPath, "GadgetTools", "PullRequestManagement");
            Directory.CreateDirectory(appFolder);
            _settingsFilePath = Path.Combine(appFolder, "settings.json");
        }

        public async Task<PullRequestManagementSettings> LoadSettingsAsync()
        {
            try
            {
                if (!File.Exists(_settingsFilePath))
                {
                    return new PullRequestManagementSettings();
                }

                var json = await File.ReadAllTextAsync(_settingsFilePath);
                var settings = JsonSerializer.Deserialize<PullRequestManagementSettings>(json);
                
                // PullRequestManagement now uses shared Azure DevOps configuration
                // No need to decrypt PAT as it's handled by AzureDevOpsConfigService
                
                return settings ?? new PullRequestManagementSettings();
            }
            catch
            {
                return new PullRequestManagementSettings();
            }
        }

        public async Task SaveSettingsAsync(PullRequestManagementSettings settings)
        {
            try
            {
                // Create a copy for saving (no encryption needed for plugin-specific settings)
                var settingsToSave = new PullRequestManagementSettings
                {
                    Project = settings.Project,
                    Repository = settings.Repository,
                    AuthorFilter = settings.AuthorFilter,
                    TargetBranchFilter = settings.TargetBranchFilter,
                    FromDate = settings.FromDate,
                    WindowWidth = settings.WindowWidth,
                    WindowHeight = settings.WindowHeight,
                    WindowLeft = settings.WindowLeft,
                    WindowTop = settings.WindowTop,
                    ColumnVisibility = settings.ColumnVisibility,
                    SavedSearches = settings.SavedSearches
                };

                var json = JsonSerializer.Serialize(settingsToSave, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                await File.WriteAllTextAsync(_settingsFilePath, json);
            }
            catch
            {
                // Silent fail - settings save is not critical
            }
        }
    }
}