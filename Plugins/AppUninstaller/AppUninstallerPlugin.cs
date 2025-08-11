using System;
using System.Threading.Tasks;
using System.Windows.Controls;
using GadgetTools.Core.Interfaces;

namespace GadgetTools.Plugins.AppUninstaller
{
    public class AppUninstallerPlugin : IToolPlugin
    {
        private AppUninstallerViewModel? _viewModel;
        
        public string Id => "AppUninstaller";
        
        public string DisplayName => "BatchAppUninstaller";
        
        public string Description => "インストール済みアプリケーションを一括でアンインストールできます";
        
        public Version Version => new(1, 0, 0, 0);
        
        public string? IconPath => "🗑️";
        
        public int Priority => 50;
        
        public bool IsEnabled { get; set; } = true;
        
        public async Task InitializeAsync()
        {
            if (_viewModel != null)
            {
                await _viewModel.InitializeAsync();
            }
        }
        
        public async Task CleanupAsync()
        {
            if (_viewModel != null)
            {
                await _viewModel.CleanupAsync();
            }
        }
        
        public UserControl CreateView()
        {
            var view = new AppUninstallerView();
            _viewModel = new AppUninstallerViewModel();
            view.DataContext = _viewModel;
            return view;
        }
        
        public object CreateViewModel()
        {
            _viewModel ??= new AppUninstallerViewModel();
            return _viewModel;
        }
        
        public Task SaveSettingsAsync(object settings)
        {
            // 現在は設定なし
            return Task.CompletedTask;
        }
        
        public Task<object?> LoadSettingsAsync()
        {
            // 現在は設定なし
            return Task.FromResult<object?>(null);
        }
    }
}