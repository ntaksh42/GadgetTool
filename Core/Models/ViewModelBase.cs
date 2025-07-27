using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace GadgetTools.Core.Models
{
    /// <summary>
    /// MVVM用の基底ViewModelクラス
    /// </summary>
    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }

    /// <summary>
    /// プラグイン用の基底ViewModelクラス
    /// </summary>
    public abstract class PluginViewModelBase : ViewModelBase
    {
        private bool _isLoading;
        private string _statusMessage = string.Empty;
        private bool _hasError;
        private string _errorMessage = string.Empty;

        public bool IsLoading
        {
            get => _isLoading;
            set 
            { 
                if (SetProperty(ref _isLoading, value))
                {
                    System.Windows.Input.CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public bool HasError
        {
            get => _hasError;
            set => SetProperty(ref _hasError, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        /// <summary>
        /// プラグインの初期化
        /// </summary>
        public virtual Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// プラグインのクリーンアップ
        /// </summary>
        public virtual Task CleanupAsync()
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// エラー状態をクリア
        /// </summary>
        protected void ClearError()
        {
            HasError = false;
            ErrorMessage = string.Empty;
        }

        /// <summary>
        /// エラー状態を設定
        /// </summary>
        protected void SetError(string message)
        {
            HasError = true;
            ErrorMessage = message;
        }

        /// <summary>
        /// エラー状態を設定（例外から）
        /// </summary>
        protected void SetError(Exception ex)
        {
            HasError = true;
            ErrorMessage = ex.Message;
        }
    }
}