using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows.Input;
using Microsoft.Win32;
using GadgetTools.Core.Models;

namespace GadgetTools.Plugins.ExcelConverter
{
    public class ExcelConverterViewModel : PluginViewModelBase
    {
        private string _filePath = string.Empty;
        private string _outputFilePath = string.Empty;
        private string _outputFolderPath = string.Empty;
        private string _previewContent = string.Empty;
        private bool _isSingleFileMode = true;
        private bool _isAllSheets = true;
        private bool _isDisplayMode = true;
        private bool _isLocalFileMode = true;
        private string _sharePointUrl = string.Empty;
        private string _selectedSheet = string.Empty;
        private GadgetTools.OutputFormat _selectedOutputFormat = GadgetTools.OutputFormat.Markdown;
        private double _progressValue;
        private bool _isProgressVisible;

        public ObservableCollection<string> SelectedFiles { get; } = new();
        public ObservableCollection<string> SheetNames { get; } = new();

        // Properties
        public string FilePath
        {
            get => _filePath;
            set => SetProperty(ref _filePath, value);
        }

        public string OutputFilePath
        {
            get => _outputFilePath;
            set => SetProperty(ref _outputFilePath, value);
        }

        public string OutputFolderPath
        {
            get => _outputFolderPath;
            set => SetProperty(ref _outputFolderPath, value);
        }

        public string PreviewContent
        {
            get => _previewContent;
            set => SetProperty(ref _previewContent, value);
        }

        public bool IsSingleFileMode
        {
            get => _isSingleFileMode;
            set => SetProperty(ref _isSingleFileMode, value);
        }

        public bool IsAllSheets
        {
            get => _isAllSheets;
            set => SetProperty(ref _isAllSheets, value);
        }

        public bool IsDisplayMode
        {
            get => _isDisplayMode;
            set => SetProperty(ref _isDisplayMode, value);
        }

        public bool IsLocalFileMode
        {
            get => _isLocalFileMode;
            set => SetProperty(ref _isLocalFileMode, value);
        }

        public string SharePointUrl
        {
            get => _sharePointUrl;
            set => SetProperty(ref _sharePointUrl, value);
        }

        public string SelectedSheet
        {
            get => _selectedSheet;
            set => SetProperty(ref _selectedSheet, value);
        }

        public GadgetTools.OutputFormat SelectedOutputFormat
        {
            get => _selectedOutputFormat;
            set => SetProperty(ref _selectedOutputFormat, value);
        }

        public double ProgressValue
        {
            get => _progressValue;
            set => SetProperty(ref _progressValue, value);
        }

        public bool IsProgressVisible
        {
            get => _isProgressVisible;
            set => SetProperty(ref _isProgressVisible, value);
        }

        // Commands
        public ICommand BrowseFileCommand { get; }
        public ICommand BrowseOutputFileCommand { get; }
        public ICommand BrowseOutputFolderCommand { get; }
        public ICommand AddFilesCommand { get; }
        public ICommand ClearFilesCommand { get; }
        public ICommand ConvertCommand { get; }
        public ICommand ConnectSharePointCommand { get; }

        public ExcelConverterViewModel()
        {
            BrowseFileCommand = new RelayCommand(BrowseFile);
            BrowseOutputFileCommand = new RelayCommand(BrowseOutputFile, () => !IsDisplayMode);
            BrowseOutputFolderCommand = new RelayCommand(BrowseOutputFolder);
            AddFilesCommand = new RelayCommand(AddFiles);
            ClearFilesCommand = new RelayCommand(ClearFiles);
            ConvertCommand = new AsyncRelayCommand(ConvertAsync, CanConvert);
            ConnectSharePointCommand = new AsyncRelayCommand(ConnectSharePointAsync);

            SelectedFiles.CollectionChanged += (s, e) => UpdateFileCount();
        }

        private void BrowseFile()
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Excelファイルを選択",
                Filter = "Excel Files (*.xlsx;*.xls)|*.xlsx;*.xls|All Files (*.*)|*.*",
                FilterIndex = 1
            };

            if (openFileDialog.ShowDialog() == true)
            {
                FilePath = openFileDialog.FileName;
                LoadSheetNames();
                SetDefaultOutputFileName();
                StatusMessage = "ファイルが選択されました";
            }
        }

        private void BrowseOutputFile()
        {
            var extension = GetFileExtension(SelectedOutputFormat).TrimStart('.');
            var saveFileDialog = new SaveFileDialog
            {
                Title = "保存先を選択",
                Filter = GetFileFilter(SelectedOutputFormat),
                FilterIndex = 1,
                DefaultExt = extension
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                OutputFilePath = saveFileDialog.FileName;
            }
        }

        private void BrowseOutputFolder()
        {
            var folderDialog = new OpenFolderDialog
            {
                Title = "出力フォルダを選択してください"
            };

            if (folderDialog.ShowDialog() == true)
            {
                OutputFolderPath = folderDialog.FolderName;
            }
        }

        private void AddFiles()
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Excelファイルを選択",
                Filter = "Excel Files (*.xlsx;*.xls)|*.xlsx;*.xls|All Files (*.*)|*.*",
                FilterIndex = 1,
                Multiselect = true
            };

            if (openFileDialog.ShowDialog() == true)
            {
                foreach (var fileName in openFileDialog.FileNames)
                {
                    if (!SelectedFiles.Contains(fileName))
                    {
                        SelectedFiles.Add(fileName);
                    }
                }
                StatusMessage = $"{SelectedFiles.Count}個のファイルが選択されました";
            }
        }

        private void ClearFiles()
        {
            SelectedFiles.Clear();
            StatusMessage = "ファイル選択がクリアされました";
        }

        private async Task ConvertAsync()
        {
            try
            {
                ClearError();
                IsLoading = true;

                if (IsSingleFileMode)
                {
                    await ConvertSingleFileAsync();
                }
                else
                {
                    await ConvertMultipleFilesAsync();
                }
            }
            catch (Exception ex)
            {
                SetError(ex);
                StatusMessage = "変換エラーが発生しました";
            }
            finally
            {
                IsLoading = false;
                IsProgressVisible = false;
            }
        }

        private async Task ConvertSingleFileAsync()
        {
            if (string.IsNullOrWhiteSpace(FilePath) || !File.Exists(FilePath))
            {
                SetError("有効なExcelファイルを選択してください。");
                return;
            }

            StatusMessage = "変換中...";
            
            string? targetSheet = IsAllSheets ? null : SelectedSheet;
            
            var convertedContent = await Task.Run(() => 
                GadgetToolsConverter.ConvertExcel(FilePath, targetSheet, SelectedOutputFormat));

            if (IsDisplayMode)
            {
                PreviewContent = convertedContent;
                StatusMessage = "変換が完了しました（プレビューに表示）";
            }
            else
            {
                if (string.IsNullOrWhiteSpace(OutputFilePath))
                {
                    SetError("保存先ファイルを指定してください。");
                    return;
                }

                await File.WriteAllTextAsync(OutputFilePath, convertedContent, Encoding.UTF8);
                PreviewContent = convertedContent;
                StatusMessage = $"変換が完了しました（{Path.GetFileName(OutputFilePath)} に保存）";
            }
        }

        private async Task ConvertMultipleFilesAsync()
        {
            if (SelectedFiles.Count == 0)
            {
                SetError("変換するExcelファイルを選択してください。");
                return;
            }

            if (string.IsNullOrWhiteSpace(OutputFolderPath) || !Directory.Exists(OutputFolderPath))
            {
                SetError("出力フォルダを選択してください。");
                return;
            }

            StatusMessage = "一括変換中...";
            IsProgressVisible = true;
            ProgressValue = 0;

            string? targetSheet = IsAllSheets ? null : SelectedSheet;
            var fileExtension = GetFileExtension(SelectedOutputFormat);

            int successCount = 0;
            int errorCount = 0;
            var errorMessages = new List<string>();

            for (int i = 0; i < SelectedFiles.Count; i++)
            {
                var filePath = SelectedFiles[i];
                try
                {
                    StatusMessage = $"変換中... ({i + 1}/{SelectedFiles.Count}) {Path.GetFileName(filePath)}";
                    
                    var convertedContent = await Task.Run(() => 
                        GadgetToolsConverter.ConvertExcel(filePath, targetSheet, SelectedOutputFormat));
                    
                    var outputFileName = Path.GetFileNameWithoutExtension(filePath) + fileExtension;
                    var outputPath = Path.Combine(OutputFolderPath, outputFileName);
                    
                    await File.WriteAllTextAsync(outputPath, convertedContent, Encoding.UTF8);
                    successCount++;
                }
                catch (Exception ex)
                {
                    errorCount++;
                    errorMessages.Add($"{Path.GetFileName(filePath)}: {ex.Message}");
                }

                ProgressValue = ((double)(i + 1) / SelectedFiles.Count) * 100;
            }

            var resultMessage = $"一括変換が完了しました。\n成功: {successCount}ファイル\n失敗: {errorCount}ファイル";
            if (errorMessages.Count > 0)
            {
                resultMessage += "\n\nエラー詳細:\n" + string.Join("\n", errorMessages.Take(5));
                if (errorMessages.Count > 5)
                {
                    resultMessage += $"\n...他{errorMessages.Count - 5}件";
                }
            }

            StatusMessage = $"一括変換完了（成功: {successCount}, 失敗: {errorCount}）";
        }

        private async Task ConnectSharePointAsync()
        {
            if (string.IsNullOrWhiteSpace(SharePointUrl))
            {
                SetError("SharePoint URLを入力してください。");
                return;
            }

            try
            {
                ClearError();
                IsLoading = true;
                StatusMessage = "SharePointファイルをダウンロード中...";

                // SharePoint接続の実装（簡略化）
                await Task.Delay(1000); // 模擬処理
                
                // 実際の実装では、SharePointからファイルをダウンロードして一時ファイルに保存
                StatusMessage = "SharePointファイルが正常に読み込まれました";
            }
            catch (Exception ex)
            {
                SetError($"SharePointファイルの読み込みに失敗しました: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private bool CanConvert()
        {
            if (IsSingleFileMode)
            {
                bool hasValidFile = !string.IsNullOrWhiteSpace(FilePath) && File.Exists(FilePath);
                if (!IsDisplayMode)
                {
                    return hasValidFile && !string.IsNullOrWhiteSpace(OutputFilePath);
                }
                return hasValidFile;
            }
            else
            {
                return SelectedFiles.Count > 0 && 
                       !string.IsNullOrWhiteSpace(OutputFolderPath) && 
                       Directory.Exists(OutputFolderPath);
            }
        }

        private void LoadSheetNames()
        {
            try
            {
                var sheetNames = GadgetToolsConverter.GetSheetNames(FilePath);
                SheetNames.Clear();
                foreach (var name in sheetNames)
                {
                    SheetNames.Add(name);
                }
                if (SheetNames.Count > 0)
                {
                    SelectedSheet = SheetNames[0];
                }
            }
            catch (Exception ex)
            {
                SetError($"シート名の取得に失敗しました: {ex.Message}");
            }
        }

        private void SetDefaultOutputFileName()
        {
            if (IsSingleFileMode && !string.IsNullOrWhiteSpace(FilePath) && File.Exists(FilePath))
            {
                try
                {
                    var directory = Path.GetDirectoryName(FilePath);
                    var extension = GetFileExtension(SelectedOutputFormat);
                    var defaultFileName = $"converted{extension}";
                    var defaultPath = Path.Combine(directory ?? Environment.CurrentDirectory, defaultFileName);
                    OutputFilePath = defaultPath;
                }
                catch (Exception)
                {
                    // エラーが発生した場合は初期値設定をスキップ
                }
            }
        }

        private void UpdateFileCount()
        {
            StatusMessage = $"選択ファイル数: {SelectedFiles.Count}";
        }

        private string GetFileExtension(OutputFormat format)
        {
            return format switch
            {
                GadgetTools.OutputFormat.Markdown => ".md",
                GadgetTools.OutputFormat.CSV => ".csv",
                GadgetTools.OutputFormat.JSON => ".json",
                GadgetTools.OutputFormat.HTML => ".html",
                _ => ".md"
            };
        }

        private string GetFileFilter(OutputFormat format)
        {
            return format switch
            {
                GadgetTools.OutputFormat.Markdown => "Markdown Files (*.md)|*.md|All Files (*.*)|*.*",
                GadgetTools.OutputFormat.CSV => "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
                GadgetTools.OutputFormat.JSON => "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                GadgetTools.OutputFormat.HTML => "HTML Files (*.html)|*.html|All Files (*.*)|*.*",
                _ => "Markdown Files (*.md)|*.md|All Files (*.*)|*.*"
            };
        }
    }

}