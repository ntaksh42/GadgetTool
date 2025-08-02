using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
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
                Title = "Azure DevOps Settings",
                Content = settingsView,
                Width = 650,
                Height = 500,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this),
                ResizeMode = ResizeMode.CanResize,
                MinWidth = 600,
                MinHeight = 400
            };
            window.ShowDialog();
        }

        private void CollapseAll_Click(object sender, RoutedEventArgs e)
        {
            // Find all Expanders in the visual tree and collapse them
            var expanders = FindVisualChildren<Expander>(this);
            foreach (var expander in expanders)
            {
                expander.IsExpanded = false;
            }
        }

        private void ExpandAll_Click(object sender, RoutedEventArgs e)
        {
            // Find all Expanders in the visual tree and expand them
            var expanders = FindVisualChildren<Expander>(this);
            foreach (var expander in expanders)
            {
                expander.IsExpanded = true;
            }
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                    if (child != null && child is T)
                    {
                        yield return (T)child;
                    }

                    foreach (T childOfChild in FindVisualChildren<T>(child))
                    {
                        yield return childOfChild;
                    }
                }
            }
        }
    }
}