using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using GadgetTools.Core.Views;

namespace GadgetTools.Plugins.PullRequestManagement
{
    /// <summary>
    /// Interaction logic for PullRequestManagementView.xaml
    /// </summary>
    public partial class PullRequestManagementView : UserControl
    {
        public PullRequestManagementView()
        {
            InitializeComponent();
        }

        private void TitleHyperlink_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Hyperlink hyperlink && hyperlink.Tag is PullRequest pullRequest)
            {
                try
                {
                    if (!string.IsNullOrEmpty(pullRequest.Url))
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = pullRequest.Url,
                            UseShellExecute = true
                        });
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to open URL: {ex.Message}", "Error", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
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
    }
}