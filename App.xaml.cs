using System.Windows;
using System.IO;

namespace GadgetTools
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                File.WriteAllText("startup_debug.txt", "App starting...\n");
                base.OnStartup(e);
                File.AppendAllText("startup_debug.txt", "App started successfully\n");
            }
            catch (Exception ex)
            {
                File.WriteAllText("startup_error.txt", $"Startup error: {ex}\n");
                MessageBox.Show($"Startup error: {ex.Message}\n\nDetails: {ex}", "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Application_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            File.WriteAllText("runtime_error.txt", $"Runtime error: {e.Exception}\n");
            MessageBox.Show($"Unhandled error: {e.Exception.Message}\n\nDetails: {e.Exception}", "Runtime Error", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }
    }
}