using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GadgetTools.Core.Views;
using GadgetTools.Models;

namespace GadgetTools.Plugins.TicketManage
{
    /// <summary>
    /// TicketManageView.xaml の相互作用ロジック
    /// </summary>
    public partial class TicketManageView : UserControl
    {
        public TicketManageView()
        {
            InitializeComponent();
        }

        private void GlobalSettings_Click(object sender, RoutedEventArgs e)
        {
            var settingsView = new AzureDevOpsSettingsView();
            var window = new Window
            {
                Title = "Azure DevOps Global Settings",
                Content = settingsView,
                Width = 600,
                Height = 300,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this),
                ResizeMode = ResizeMode.NoResize
            };
            window.ShowDialog();
        }
        
        private void DataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            OpenSelectedWorkItem();
        }
        
        private void DataGrid_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                OpenSelectedWorkItem();
                e.Handled = true;
            }
        }
        
        private void OpenSelectedWorkItem()
        {
            if (DataContext is TicketManageViewModel viewModel && 
                viewModel.SelectedWorkItem is WorkItem workItem)
            {
                try
                {
                    var url = $"https://dev.azure.com/{viewModel.Organization}/{viewModel.Project}/_workitems/edit/{workItem.Id}";
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
                }
                catch (System.Exception ex)
                {
                    System.Windows.MessageBox.Show($"Failed to open work item: {ex.Message}", "Error", 
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
        }
    }
}