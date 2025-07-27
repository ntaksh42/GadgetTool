using System.Windows.Controls;
using GadgetTools.Core.Interfaces;
using GadgetTools.Services;

namespace GadgetTools.Plugins.ExcelConverter
{
    /// <summary>
    /// Excel変換プラグイン
    /// </summary>
    public class ExcelConverterPlugin : IToolPlugin
    {
        public string Id => "GadgetTools.ExcelConverter";
        public string DisplayName => "Excel Converter";
        public string Description => "Excel・Azure DevOpsデータをMarkdown、CSV、JSON、HTML形式に変換するツール";
        public Version Version => new Version(1, 0, 0);
        public string? IconPath => null;
        public int Priority => 1;
        public bool IsEnabled { get; set; } = true;

        public Task InitializeAsync()
        {
            // 初期化処理（必要に応じて）
            return Task.CompletedTask;
        }

        public Task CleanupAsync()
        {
            // クリーンアップ処理（必要に応じて）
            return Task.CompletedTask;
        }

        public UserControl CreateView()
        {
            return new ExcelConverterView();
        }

        public object CreateViewModel()
        {
            return new ExcelConverterViewModel();
        }

        public async Task SaveSettingsAsync(object settings)
        {
            if (settings is ExcelConverterSettings excelSettings)
            {
                // プラグイン固有の設定を保存
                var appSettings = SettingsService.LoadSettings();
                
                // 設定にExcelConverter設定を追加/更新
                if (appSettings.UI == null)
                    appSettings.UI = new SettingsService.UISettings();

                // 設定を保存（実装は後で詳細化）
                SettingsService.SaveSettings(appSettings);
            }
            await Task.CompletedTask;
        }

        public Task<object?> LoadSettingsAsync()
        {
            try
            {
                var appSettings = SettingsService.LoadSettings();
                
                // ExcelConverter固有の設定を読み込み
                // 実装は後で詳細化
                return Task.FromResult<object?>(new ExcelConverterSettings());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ExcelConverter設定読み込みエラー: {ex.Message}");
                return Task.FromResult<object?>(null);
            }
        }
    }

    /// <summary>
    /// ExcelConverter固有の設定
    /// </summary>
    public class ExcelConverterSettings
    {
        public string LastUsedFolder { get; set; } = string.Empty;
        public GadgetTools.OutputFormat DefaultOutputFormat { get; set; } = GadgetTools.OutputFormat.Markdown;
        public bool DefaultToDisplayMode { get; set; } = true;
        public bool DefaultToAllSheets { get; set; } = true;
    }
}