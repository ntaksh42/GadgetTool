using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GadgetTools.Plugins.AppUninstaller
{
    public class UninstallService
    {
        public event EventHandler<UninstallProgressEventArgs>? ProgressUpdated;
        
        public async Task<List<UninstallResult>> UninstallAppsAsync(
            IEnumerable<InstalledApp> apps, 
            CancellationToken cancellationToken = default)
        {
            var results = new List<UninstallResult>();
            var appsList = apps.ToList();
            
            for (int i = 0; i < appsList.Count; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;
                
                var app = appsList[i];
                var progress = new UninstallProgressEventArgs
                {
                    CurrentApp = app,
                    Progress = (i + 1) * 100 / appsList.Count,
                    CurrentIndex = i + 1,
                    TotalCount = appsList.Count
                };
                
                ProgressUpdated?.Invoke(this, progress);
                
                var result = await UninstallSingleAppAsync(app, cancellationToken);
                results.Add(result);
                
                // 短い間隔を空けて次のアプリを処理
                await Task.Delay(500, cancellationToken);
            }
            
            return results;
        }
        
        private async Task<UninstallResult> UninstallSingleAppAsync(InstalledApp app, CancellationToken cancellationToken)
        {
            var result = new UninstallResult
            {
                App = app,
                Status = UninstallStatus.InProgress
            };
            
            try
            {
                var uninstallCommand = ParseUninstallCommand(app.UninstallString);
                if (uninstallCommand == null)
                {
                    result.Status = UninstallStatus.Failed;
                    result.Message = "アンインストールコマンドを解析できませんでした";
                    return result;
                }
                
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = uninstallCommand.ExecutablePath,
                        Arguments = uninstallCommand.Arguments,
                        UseShellExecute = true,
                        CreateNoWindow = false,
                        Verb = "runas"
                    }
                };
                
                process.Start();
                
                // タイムアウト付きで待機（最大5分）
                var completed = await WaitForExitAsync(process, TimeSpan.FromMinutes(5), cancellationToken);
                
                if (!completed)
                {
                    process.Kill();
                    result.Status = UninstallStatus.Failed;
                    result.Message = "アンインストールがタイムアウトしました";
                }
                else if (process.ExitCode == 0)
                {
                    result.Status = UninstallStatus.Success;
                    result.Message = "正常にアンインストールされました";
                }
                else
                {
                    result.Status = UninstallStatus.Failed;
                    result.Message = $"アンインストールに失敗しました (終了コード: {process.ExitCode})";
                }
                
                process.Dispose();
            }
            catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                result.Status = UninstallStatus.Failed;
                result.Message = "ユーザーによって管理者権限の要求がキャンセルされました";
                result.Exception = ex;
            }
            catch (Exception ex)
            {
                result.Status = UninstallStatus.Failed;
                result.Message = $"アンインストール実行エラー: {ex.Message}";
                result.Exception = ex;
            }
            
            return result;
        }
        
        private UninstallCommand? ParseUninstallCommand(string uninstallString)
        {
            if (string.IsNullOrEmpty(uninstallString))
                return null;
            
            // MsiExec.exe の場合
            if (uninstallString.Contains("msiexec", StringComparison.OrdinalIgnoreCase))
            {
                return new UninstallCommand
                {
                    ExecutablePath = "msiexec.exe",
                    Arguments = ExtractMsiExecArguments(uninstallString) + " /quiet /norestart"
                };
            }
            
            // 実行ファイルパスと引数を分離
            var parts = SplitCommandLine(uninstallString);
            if (parts.Length == 0)
                return null;
            
            var executablePath = parts[0];
            var arguments = parts.Length > 1 ? string.Join(" ", parts.Skip(1)) : "";
            
            // サイレントアンインストールオプションを追加
            arguments = AddSilentOptions(arguments, Path.GetFileNameWithoutExtension(executablePath));
            
            return new UninstallCommand
            {
                ExecutablePath = executablePath,
                Arguments = arguments
            };
        }
        
        private string ExtractMsiExecArguments(string uninstallString)
        {
            // /I を /X に置換してサイレントオプションを確保
            var args = uninstallString.Replace("msiexec.exe", "", StringComparison.OrdinalIgnoreCase)
                                    .Replace("msiexec", "", StringComparison.OrdinalIgnoreCase)
                                    .Replace("/I", "/X")
                                    .Trim();
            
            return args;
        }
        
        private string[] SplitCommandLine(string commandLine)
        {
            var parts = new List<string>();
            var current = "";
            var inQuotes = false;
            
            for (int i = 0; i < commandLine.Length; i++)
            {
                var c = commandLine[i];
                
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ' ' && !inQuotes)
                {
                    if (!string.IsNullOrEmpty(current))
                    {
                        parts.Add(current.Trim('"'));
                        current = "";
                    }
                }
                else
                {
                    current += c;
                }
            }
            
            if (!string.IsNullOrEmpty(current))
            {
                parts.Add(current.Trim('"'));
            }
            
            return parts.ToArray();
        }
        
        private string AddSilentOptions(string arguments, string executableName)
        {
            // 一般的なサイレントインストールオプション
            var silentOptions = executableName.ToLower() switch
            {
                var name when name.Contains("unins") => arguments + " /VERYSILENT /SUPPRESSMSGBOXES",
                var name when name.Contains("setup") => arguments + " /S",
                var name when name.Contains("uninst") => arguments + " /S",
                _ => arguments + " /quiet"
            };
            
            return silentOptions;
        }
        
        private async Task<bool> WaitForExitAsync(Process process, TimeSpan timeout, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            
            process.EnableRaisingEvents = true;
            process.Exited += (_, _) => tcs.TrySetResult(true);
            
            cancellationToken.Register(() => tcs.TrySetCanceled());
            
            var delayTask = Task.Delay(timeout, cancellationToken);
            var exitTask = tcs.Task;
            
            var completedTask = await Task.WhenAny(exitTask, delayTask);
            
            return completedTask == exitTask && exitTask.Result;
        }
    }
    
    public class UninstallCommand
    {
        public string ExecutablePath { get; set; } = string.Empty;
        public string Arguments { get; set; } = string.Empty;
    }
    
    public class UninstallProgressEventArgs : EventArgs
    {
        public InstalledApp CurrentApp { get; set; } = new();
        public int Progress { get; set; }
        public int CurrentIndex { get; set; }
        public int TotalCount { get; set; }
    }
}