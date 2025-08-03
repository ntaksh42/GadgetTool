using System.Collections.Generic;
using Newtonsoft.Json;

namespace GadgetTools.Shared.Models
{
    /// <summary>
    /// DataGrid列の設定情報
    /// </summary>
    public class DataGridColumnSettings
    {
        /// <summary>
        /// 列のヘッダー名（識別子として使用）
        /// </summary>
        public string Header { get; set; } = string.Empty;

        /// <summary>
        /// 列の表示状態（表示/非表示）
        /// </summary>
        public bool IsVisible { get; set; } = true;

        /// <summary>
        /// 列の幅
        /// </summary>
        public double Width { get; set; } = double.NaN;

        /// <summary>
        /// 列の表示順序
        /// </summary>
        public int DisplayIndex { get; set; } = 0;

        /// <summary>
        /// 列の最小幅
        /// </summary>
        public double MinWidth { get; set; } = 20;

        /// <summary>
        /// 列の最大幅
        /// </summary>
        public double MaxWidth { get; set; } = double.PositiveInfinity;

        /// <summary>
        /// 自動サイズ調整の種類
        /// </summary>
        public string SizeToHeader { get; set; } = "None";
    }

    /// <summary>
    /// DataGrid全体の設定情報
    /// </summary>
    public class DataGridSettings
    {
        /// <summary>
        /// DataGridの識別子（プラグイン名やView名など）
        /// </summary>
        public string GridId { get; set; } = string.Empty;

        /// <summary>
        /// 列の設定リスト
        /// </summary>
        public List<DataGridColumnSettings> Columns { get; set; } = new List<DataGridColumnSettings>();

        /// <summary>
        /// 設定が最後に更新された日時
        /// </summary>
        public DateTime LastUpdated { get; set; } = DateTime.Now;

        /// <summary>
        /// 設定のバージョン
        /// </summary>
        public int Version { get; set; } = 1;
    }

    /// <summary>
    /// すべてのDataGrid設定を管理するコンテナ
    /// </summary>
    public class DataGridSettingsContainer
    {
        /// <summary>
        /// GridIdをキーとした設定のディクショナリ
        /// </summary>
        public Dictionary<string, DataGridSettings> GridSettings { get; set; } = new Dictionary<string, DataGridSettings>();

        /// <summary>
        /// 設定ファイルのバージョン
        /// </summary>
        public int FileVersion { get; set; } = 1;

        /// <summary>
        /// 最後に保存された日時
        /// </summary>
        public DateTime LastSaved { get; set; } = DateTime.Now;
    }
}