using System;
using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace Elinor
{
    /// <summary>
    /// Interaction logic for SelectLogPathWindow.xaml
    /// </summary>
    public partial class SelectLogPathWindow
    {
        private readonly string _logdir;

        public SelectLogPathWindow(string logdir)
        {
            InitializeComponent();
            _logdir = logdir;
            tbPath.Text = logdir;
        }

        internal DirectoryInfo? Logpath { get; private set; }

        private void BtnFileSelectClick(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog { Title = "Select your EVE 'Marketlogs' folder" };

            // Start from the nearest folder that exists; the dialog rejects missing ones.
            string? start = _logdir;
            while (!string.IsNullOrEmpty(start) && !Directory.Exists(start))
                start = Path.GetDirectoryName(start);
            if (!string.IsNullOrEmpty(start)) dialog.InitialDirectory = start;

            if (dialog.ShowDialog(this) == true)
            {
                tbPath.Text = dialog.FolderName;
            }
        }

        private void BtnCancelClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void BtnOkClick(object sender, RoutedEventArgs e)
        {
            try
            {
                Logpath = new DirectoryInfo(tbPath.Text.Trim());
                DialogResult = true;
            }
            catch (Exception ex) when (ex is ArgumentException || ex is PathTooLongException || ex is NotSupportedException || ex is System.Security.SecurityException)
            {
                MessageBox.Show(this, "That is not a valid folder path.", "Select EVE log path",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
