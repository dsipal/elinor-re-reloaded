using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace Elinor
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App
    {
        /// <summary>Must match AppUserModelId in installer/Elinor.iss so taskbar pins stay linked to the Start menu shortcut.</summary>
        internal const string AppUserModelId = "dsipal.Elinor";

        private bool _showingError;

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        private static extern void SetCurrentProcessExplicitAppUserModelID(string appId);

        /// <summary>Loaded once at startup, before any window, so the theme applies from the first frame.</summary>
        internal static AppSettings Settings { get; private set; } = new AppSettings();

        protected override void OnStartup(StartupEventArgs e)
        {
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

            Log.Info("Elinor " + Updates.CurrentVersion + " starting on " + Environment.OSVersion);

            try
            {
                SetCurrentProcessExplicitAppUserModelID(AppUserModelId);
            }
            catch (Exception ex)
            {
                Log.Warn("Could not set AppUserModelID", ex);
            }

            Settings = AppSettings.Load();
            ThemeManager.Apply(Settings.Theme);

            base.OnStartup(e);
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Log.Error("Unhandled UI exception", e.Exception);

            // Keep running: nothing in Elinor holds state that an exception in one handler corrupts.
            e.Handled = true;

            if (_showingError) return;
            _showingError = true;
            try
            {
                MessageBox.Show(
                    "Elinor ran into an unexpected error and recovered.\n\n" +
                    e.Exception.Message + "\n\nDetails were written to:\n" + Log.CurrentFile,
                    "Elinor", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                _showingError = false;
            }
        }

        private static void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Log.Error("Fatal unhandled exception (terminating: " + e.IsTerminating + ")", e.ExceptionObject as Exception);
        }

        private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            Log.Error("Unobserved task exception", e.Exception);
            e.SetObserved();
        }
    }
}
