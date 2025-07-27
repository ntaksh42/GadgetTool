using System.Windows.Controls;

namespace GadgetTools.Core.Interfaces
{
    /// <summary>
    /// ツールプラグインの基底インターフェース
    /// </summary>
    public interface IToolPlugin
    {
        /// <summary>
        /// プラグインの一意識別子
        /// </summary>
        string Id { get; }

        /// <summary>
        /// プラグインの表示名
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// プラグインの説明
        /// </summary>
        string Description { get; }

        /// <summary>
        /// プラグインのバージョン
        /// </summary>
        Version Version { get; }

        /// <summary>
        /// プラグインのアイコン（オプション）
        /// </summary>
        string? IconPath { get; }

        /// <summary>
        /// プラグインの優先順位（タブの表示順）
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// プラグインの初期化
        /// </summary>
        Task InitializeAsync();

        /// <summary>
        /// プラグインのクリーンアップ
        /// </summary>
        Task CleanupAsync();

        /// <summary>
        /// プラグインのUIビューを作成
        /// </summary>
        UserControl CreateView();

        /// <summary>
        /// プラグインのViewModelを作成
        /// </summary>
        object CreateViewModel();

        /// <summary>
        /// プラグインが有効かどうか
        /// </summary>
        bool IsEnabled { get; set; }

        /// <summary>
        /// プラグイン設定の保存
        /// </summary>
        Task SaveSettingsAsync(object settings);

        /// <summary>
        /// プラグイン設定の読み込み
        /// </summary>
        Task<object?> LoadSettingsAsync();
    }
}