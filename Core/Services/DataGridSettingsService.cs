using System.IO;
using System.Windows;
using System.Windows.Controls;
using GadgetTools.Shared.Models;
using GadgetTools.Shared.Utilities;

namespace GadgetTools.Core.Services
{
    /// <summary>
    /// DataGrid設定の保存・復元を管理するサービス
    /// </summary>
    public class DataGridSettingsService
    {
        private static DataGridSettingsService? _instance;
        private readonly string _settingsFilePath;
        private DataGridSettingsContainer _settingsContainer;

        public static DataGridSettingsService Instance => _instance ??= new DataGridSettingsService();

        private DataGridSettingsService()
        {
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "GadgetTools"
            );
            
            if (!Directory.Exists(appDataPath))
            {
                Directory.CreateDirectory(appDataPath);
            }

            _settingsFilePath = Path.Combine(appDataPath, "datagrid-settings.json");
            _settingsContainer = LoadSettings();
        }

        /// <summary>
        /// DataGridの設定を保存
        /// </summary>
        /// <param name="dataGrid">対象のDataGrid</param>
        /// <param name="gridId">DataGridの識別子</param>
        public void SaveGridSettings(DataGrid dataGrid, string gridId)
        {
            try
            {
                if (dataGrid == null || string.IsNullOrWhiteSpace(gridId))
                    return;

                var settings = new DataGridSettings
                {
                    GridId = gridId,
                    LastUpdated = DateTime.Now,
                    Version = 1
                };

                // 各列の設定を保存
                foreach (var column in dataGrid.Columns)
                {
                    var columnSetting = new DataGridColumnSettings
                    {
                        Header = column.Header?.ToString() ?? string.Empty,
                        IsVisible = column.Visibility == Visibility.Visible,
                        Width = column.Width.IsAbsolute ? column.Width.Value : double.NaN,
                        DisplayIndex = column.DisplayIndex,
                        MinWidth = column.MinWidth,
                        MaxWidth = column.MaxWidth,
                        SizeToHeader = column.Width.UnitType.ToString()
                    };

                    settings.Columns.Add(columnSetting);
                }

                // 設定コンテナに保存
                _settingsContainer.GridSettings[gridId] = settings;
                _settingsContainer.LastSaved = DateTime.Now;

                // ファイルに保存
                SaveSettingsToFile();

                System.Diagnostics.Debug.WriteLine($"DataGrid settings saved for: {gridId}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving DataGrid settings: {ex.Message}");
            }
        }

        /// <summary>
        /// DataGridの設定を復元
        /// </summary>
        /// <param name="dataGrid">対象のDataGrid</param>
        /// <param name="gridId">DataGridの識別子</param>
        public void RestoreGridSettings(DataGrid dataGrid, string gridId)
        {
            try
            {
                if (dataGrid == null || string.IsNullOrWhiteSpace(gridId))
                    return;

                if (!_settingsContainer.GridSettings.TryGetValue(gridId, out var settings))
                {
                    System.Diagnostics.Debug.WriteLine($"No saved settings found for grid: {gridId}");
                    return;
                }

                // 列の設定をマップで管理
                var columnSettingsMap = settings.Columns.ToDictionary(c => c.Header, c => c);

                // DataGridの列を設定に合わせて更新
                foreach (var column in dataGrid.Columns)
                {
                    var header = column.Header?.ToString() ?? string.Empty;
                    
                    if (columnSettingsMap.TryGetValue(header, out var columnSetting))
                    {
                        // 表示状態を復元
                        column.Visibility = columnSetting.IsVisible ? Visibility.Visible : Visibility.Collapsed;

                        // 列幅を復元
                        if (!double.IsNaN(columnSetting.Width))
                        {
                            column.Width = new DataGridLength(columnSetting.Width);
                        }

                        // 列の順序を復元
                        if (columnSetting.DisplayIndex >= 0 && columnSetting.DisplayIndex < dataGrid.Columns.Count)
                        {
                            column.DisplayIndex = columnSetting.DisplayIndex;
                        }

                        // 最小・最大幅を復元
                        column.MinWidth = columnSetting.MinWidth;
                        column.MaxWidth = columnSetting.MaxWidth;
                    }
                }

                System.Diagnostics.Debug.WriteLine($"DataGrid settings restored for: {gridId}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error restoring DataGrid settings: {ex.Message}");
            }
        }

        /// <summary>
        /// 特定のGridの設定を削除
        /// </summary>
        /// <param name="gridId">DataGridの識別子</param>
        public void DeleteGridSettings(string gridId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(gridId))
                    return;

                if (_settingsContainer.GridSettings.Remove(gridId))
                {
                    SaveSettingsToFile();
                    System.Diagnostics.Debug.WriteLine($"DataGrid settings deleted for: {gridId}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deleting DataGrid settings: {ex.Message}");
            }
        }

        /// <summary>
        /// すべての設定をリセット
        /// </summary>
        public void ResetAllSettings()
        {
            try
            {
                _settingsContainer = new DataGridSettingsContainer();
                SaveSettingsToFile();
                System.Diagnostics.Debug.WriteLine("All DataGrid settings reset");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error resetting DataGrid settings: {ex.Message}");
            }
        }

        /// <summary>
        /// 設定ファイルから読み込み
        /// </summary>
        private DataGridSettingsContainer LoadSettings()
        {
            try
            {
                if (!File.Exists(_settingsFilePath))
                {
                    return new DataGridSettingsContainer();
                }

                var json = File.ReadAllText(_settingsFilePath);
                var container = JsonHelper.Deserialize<DataGridSettingsContainer>(json);
                
                return container ?? new DataGridSettingsContainer();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading DataGrid settings: {ex.Message}");
                return new DataGridSettingsContainer();
            }
        }

        /// <summary>
        /// 設定ファイルに保存
        /// </summary>
        private void SaveSettingsToFile()
        {
            try
            {
                var json = JsonHelper.Serialize(_settingsContainer);
                File.WriteAllText(_settingsFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving DataGrid settings to file: {ex.Message}");
            }
        }

        /// <summary>
        /// 特定のGridの設定が存在するかチェック
        /// </summary>
        /// <param name="gridId">DataGridの識別子</param>
        /// <returns>設定が存在する場合true</returns>
        public bool HasSettings(string gridId)
        {
            return !string.IsNullOrWhiteSpace(gridId) && _settingsContainer.GridSettings.ContainsKey(gridId);
        }

        /// <summary>
        /// 利用可能な設定IDのリストを取得
        /// </summary>
        /// <returns>設定IDのリスト</returns>
        public IEnumerable<string> GetAvailableGridIds()
        {
            return _settingsContainer.GridSettings.Keys;
        }
    }
}