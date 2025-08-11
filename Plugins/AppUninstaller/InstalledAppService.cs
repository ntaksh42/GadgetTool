using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GadgetTools.Plugins.AppUninstaller
{
    public class InstalledAppService
    {
        private readonly string[] _uninstallPaths = 
        {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
        };
        
        public async Task<List<InstalledApp>> GetInstalledAppsAsync()
        {
            return await Task.Run(() =>
            {
                var apps = new List<InstalledApp>();
                
                foreach (var path in _uninstallPaths)
                {
                    apps.AddRange(GetAppsFromRegistry(Registry.LocalMachine, path));
                }
                
                // 重複を除去し、名前順にソート
                return apps
                    .GroupBy(app => app.Name.ToLower())
                    .Select(group => group.First())
                    .Where(app => !string.IsNullOrEmpty(app.Name) && !string.IsNullOrEmpty(app.UninstallString))
                    .OrderBy(app => app.Name)
                    .ToList();
            });
        }
        
        private List<InstalledApp> GetAppsFromRegistry(RegistryKey rootKey, string subKeyPath)
        {
            var apps = new List<InstalledApp>();
            
            try
            {
                using var uninstallKey = rootKey.OpenSubKey(subKeyPath);
                if (uninstallKey == null) return apps;
                
                foreach (var subKeyName in uninstallKey.GetSubKeyNames())
                {
                    try
                    {
                        using var appKey = uninstallKey.OpenSubKey(subKeyName);
                        if (appKey == null) continue;
                        
                        var app = CreateAppFromRegistry(appKey);
                        if (app != null && ShouldIncludeApp(app))
                        {
                            apps.Add(app);
                        }
                    }
                    catch (Exception ex)
                    {
                        // 個別のアプリで例外が発生しても処理を継続
                        System.Diagnostics.Debug.WriteLine($"Error reading app {subKeyName}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error reading registry path {subKeyPath}: {ex.Message}");
            }
            
            return apps;
        }
        
        private InstalledApp? CreateAppFromRegistry(RegistryKey appKey)
        {
            var name = appKey.GetValue("DisplayName")?.ToString();
            var uninstallString = appKey.GetValue("UninstallString")?.ToString();
            
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(uninstallString))
                return null;
            
            var app = new InstalledApp
            {
                Name = name,
                Publisher = appKey.GetValue("Publisher")?.ToString() ?? "不明",
                Version = appKey.GetValue("DisplayVersion")?.ToString() ?? "不明",
                UninstallString = uninstallString,
                InstallLocation = appKey.GetValue("InstallLocation")?.ToString() ?? string.Empty,
                Icon = appKey.GetValue("DisplayIcon")?.ToString() ?? string.Empty
            };
            
            // インストール日の取得
            if (appKey.GetValue("InstallDate") is string installDateStr && 
                installDateStr.Length == 8 && 
                DateTime.TryParseExact(installDateStr, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var installDate))
            {
                app.InstallDate = installDate;
            }
            
            // サイズの取得
            if (appKey.GetValue("EstimatedSize") is int sizeKB)
            {
                app.EstimatedSize = sizeKB * 1024L;
            }
            
            return app;
        }
        
        private static bool ShouldIncludeApp(InstalledApp app)
        {
            // システムアップデートやドライバーなど、通常のユーザーがアンインストールしないものを除外
            var excludeKeywords = new[]
            {
                "KB", "Hotfix", "Security Update", "Update for Microsoft",
                "Microsoft Visual C++ Redistributable",
                "Windows SDK", "Microsoft .NET Framework",
                "DirectX", "Microsoft Windows"
            };
            
            return !excludeKeywords.Any(keyword => 
                app.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                app.Publisher.Contains("Microsoft Corporation", StringComparison.OrdinalIgnoreCase) && 
                (app.Name.Contains("Update") || app.Name.Contains("Runtime"))
            );
        }
    }
}