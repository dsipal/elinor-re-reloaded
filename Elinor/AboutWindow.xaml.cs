using System.Windows;
using System.Windows.Input;
using System.Windows.Navigation;

namespace Elinor
{
    /// <summary>
    /// Interaction logic for AboutWindow.xaml
    /// </summary>
    public partial class AboutWindow
    {
        public AboutWindow()
        {
            InitializeComponent();
            lblVersion.Text = "Version " + Updates.CurrentVersion.ToString(3);
        }

        private void WindowKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        }

        private void LinkRequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            MiscTools.OpenUrl(e.Uri.AbsoluteUri);
            e.Handled = true;
        }

        private void BtnCloseClick(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
