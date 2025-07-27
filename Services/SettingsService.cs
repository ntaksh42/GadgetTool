using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using GadgetTools.Models;

namespace GadgetTools.Services
{
    public class SettingsService
    {
        private static readonly string SettingsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GadgetTools");
        private static readonly string SettingsFilePath = Path.Combine(SettingsFolder, "settings.json");

        public class AppSettings
        {
            public AzureDevOpsSettings? AzureDevOps { get; set; }
            public UISettings? UI { get; set; }
        }

        public class AzureDevOpsSettings
        {
            public string Organization { get; set; } = "";
            public string Project { get; set; } = "";
            public string EncryptedPersonalAccessToken { get; set; } = "";
            public string WorkItemType { get; set; } = "All";
            public string State { get; set; } = "All";
            public string Iteration { get; set; } = "All";
            public string Area { get; set; } = "All";
            public int MaxResults { get; set; } = 50;
            public bool DetailedMarkdown { get; set; } = true;
            public int HighlightDays { get; set; } = 7;
            public bool EnableHighlight { get; set; } = true;
        }

        public class UISettings
        {
            public List<string> TabOrder { get; set; } = new List<string>();
            public int SelectedTabIndex { get; set; } = 0;
        }

        public static void SaveSettings(AppSettings settings)
        {
            try
            {
                // フォルダが存在しない場合は作成
                if (!Directory.Exists(SettingsFolder))
                {
                    Directory.CreateDirectory(SettingsFolder);
                }

                var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
                File.WriteAllText(SettingsFilePath, json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                // 設定保存エラーは致命的ではないため、ログのみ記録
                System.Diagnostics.Debug.WriteLine($"設定保存エラー: {ex.Message}");
            }
        }

        public static AppSettings LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    var json = File.ReadAllText(SettingsFilePath, Encoding.UTF8);
                    var settings = JsonConvert.DeserializeObject<AppSettings>(json);
                    return settings ?? new AppSettings();
                }
            }
            catch (Exception ex)
            {
                // 設定読み込みエラーは致命的ではないため、ログのみ記録
                System.Diagnostics.Debug.WriteLine($"設定読み込みエラー: {ex.Message}");
            }

            return new AppSettings();
        }

        public static void DeleteSettings()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    File.Delete(SettingsFilePath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"設定削除エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// 文字列をDPAPIで暗号化します（現在のユーザーのみ復号化可能）
        /// </summary>
        public static string EncryptString(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return "";

            try
            {
                byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                byte[] encryptedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
                return Convert.ToBase64String(encryptedBytes);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"暗号化エラー: {ex.Message}");
                return "";
            }
        }

        /// <summary>
        /// DPAPIで暗号化された文字列を復号化します
        /// </summary>
        public static string DecryptString(string encryptedText)
        {
            if (string.IsNullOrEmpty(encryptedText))
                return "";

            try
            {
                byte[] encryptedBytes = Convert.FromBase64String(encryptedText);
                byte[] plainBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(plainBytes);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"復号化エラー: {ex.Message}");
                return "";
            }
        }
    }
}