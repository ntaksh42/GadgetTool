using System;
using System.Collections.Generic;
using System.Windows.Input;

namespace GadgetTools.Core.Services
{
    /// <summary>
    /// キーボードショートカット管理サービス
    /// </summary>
    public class KeyboardShortcutService
    {
        private static KeyboardShortcutService? _instance;
        public static KeyboardShortcutService Instance => _instance ??= new KeyboardShortcutService();

        private readonly Dictionary<string, KeyboardShortcut> _shortcuts;

        public event EventHandler<ShortcutExecutedEventArgs>? ShortcutExecuted;

        private KeyboardShortcutService()
        {
            _shortcuts = new Dictionary<string, KeyboardShortcut>();
            InitializeDefaultShortcuts();
        }

        private void InitializeDefaultShortcuts()
        {
            // General shortcuts
            RegisterShortcut("Refresh", new KeyboardShortcut(Key.F5, ModifierKeys.None, "Refresh/Query", "データを更新または検索を実行"));
            RegisterShortcut("Save", new KeyboardShortcut(Key.S, ModifierKeys.Control, "Save", "現在の結果を保存"));
            RegisterShortcut("Find", new KeyboardShortcut(Key.F, ModifierKeys.Control, "Find", "検索ボックスにフォーカス"));
            RegisterShortcut("ClearFilter", new KeyboardShortcut(Key.Delete, ModifierKeys.Control, "Clear Filters", "全てのフィルタをクリア"));
            
            // Navigation shortcuts
            RegisterShortcut("SelectAll", new KeyboardShortcut(Key.A, ModifierKeys.Control, "Select All", "全てのアイテムを選択"));
            RegisterShortcut("Copy", new KeyboardShortcut(Key.C, ModifierKeys.Control, "Copy", "選択したアイテムをコピー"));
            RegisterShortcut("OpenItem", new KeyboardShortcut(Key.Enter, ModifierKeys.None, "Open Item", "選択したアイテムを開く"));
            RegisterShortcut("OpenItemNewTab", new KeyboardShortcut(Key.Enter, ModifierKeys.Control, "Open in New Tab", "選択したアイテムを新しいタブで開く"));
            
            // Quick filters
            RegisterShortcut("MyItems", new KeyboardShortcut(Key.M, ModifierKeys.Control | ModifierKeys.Shift, "My Items", "自分担当のアイテムを表示"));
            RegisterShortcut("HighPriority", new KeyboardShortcut(Key.H, ModifierKeys.Control | ModifierKeys.Shift, "High Priority", "高優先度アイテムを表示"));
            RegisterShortcut("Bugs", new KeyboardShortcut(Key.B, ModifierKeys.Control | ModifierKeys.Shift, "Bugs Only", "バグのみを表示"));
            RegisterShortcut("Active", new KeyboardShortcut(Key.A, ModifierKeys.Control | ModifierKeys.Shift, "Active Items", "Activeなアイテムを表示"));
            
            // Advanced features
            RegisterShortcut("SaveQuery", new KeyboardShortcut(Key.S, ModifierKeys.Control | ModifierKeys.Shift, "Save Query", "現在のクエリを保存"));
            RegisterShortcut("AdvancedSearch", new KeyboardShortcut(Key.F, ModifierKeys.Control | ModifierKeys.Shift, "Advanced Search", "高度な検索を開く"));
            RegisterShortcut("ColumnFilter", new KeyboardShortcut(Key.L, ModifierKeys.Control, "Column Filter", "列フィルタを開く"));
            RegisterShortcut("ColumnVisibility", new KeyboardShortcut(Key.L, ModifierKeys.Control | ModifierKeys.Shift, "Column Visibility", "列の表示/非表示設定"));
        }

        public void RegisterShortcut(string id, KeyboardShortcut shortcut)
        {
            _shortcuts[id] = shortcut;
        }

        public bool HandleKeyInput(Key key, ModifierKeys modifiers, string context = "")
        {
            foreach (var kvp in _shortcuts)
            {
                var shortcut = kvp.Value;
                if (shortcut.Key == key && shortcut.Modifiers == modifiers)
                {
                    var args = new ShortcutExecutedEventArgs(kvp.Key, shortcut, context);
                    ShortcutExecuted?.Invoke(this, args);
                    return args.Handled;
                }
            }
            return false;
        }

        public List<KeyboardShortcut> GetAllShortcuts()
        {
            return new List<KeyboardShortcut>(_shortcuts.Values);
        }

        public List<KeyboardShortcut> GetShortcutsByCategory(string category)
        {
            var result = new List<KeyboardShortcut>();
            foreach (var shortcut in _shortcuts.Values)
            {
                if (shortcut.Category?.Equals(category, StringComparison.OrdinalIgnoreCase) == true)
                {
                    result.Add(shortcut);
                }
            }
            return result;
        }

        public string GetShortcutDisplayString(string shortcutId)
        {
            if (_shortcuts.TryGetValue(shortcutId, out var shortcut))
            {
                return shortcut.DisplayString;
            }
            return "";
        }
    }

    /// <summary>
    /// キーボードショートカット定義
    /// </summary>
    public class KeyboardShortcut
    {
        public Key Key { get; }
        public ModifierKeys Modifiers { get; }
        public string Name { get; }
        public string Description { get; }
        public string? Category { get; set; }

        public KeyboardShortcut(Key key, ModifierKeys modifiers, string name, string description)
        {
            Key = key;
            Modifiers = modifiers;
            Name = name;
            Description = description;
        }

        public string DisplayString
        {
            get
            {
                var parts = new List<string>();
                
                if (Modifiers.HasFlag(ModifierKeys.Control))
                    parts.Add("Ctrl");
                if (Modifiers.HasFlag(ModifierKeys.Shift))
                    parts.Add("Shift");
                if (Modifiers.HasFlag(ModifierKeys.Alt))
                    parts.Add("Alt");
                if (Modifiers.HasFlag(ModifierKeys.Windows))
                    parts.Add("Win");
                
                parts.Add(Key.ToString());
                
                return string.Join(" + ", parts);
            }
        }

        public override string ToString() => $"{Name} ({DisplayString})";
    }

    /// <summary>
    /// ショートカット実行イベント引数
    /// </summary>
    public class ShortcutExecutedEventArgs : EventArgs
    {
        public string ShortcutId { get; }
        public KeyboardShortcut Shortcut { get; }
        public string Context { get; }
        public bool Handled { get; set; }

        public ShortcutExecutedEventArgs(string shortcutId, KeyboardShortcut shortcut, string context)
        {
            ShortcutId = shortcutId;
            Shortcut = shortcut;
            Context = context;
            Handled = false;
        }
    }
}