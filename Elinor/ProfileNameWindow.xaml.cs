using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace Elinor
{
    /// <summary>
    /// Interaction logic for ProfileNameWindow.xaml
    /// </summary>
    public partial class ProfileNameWindow
    {
        private readonly ProfileStore _store;

        internal ProfileNameWindow(ProfileStore store)
        {
            InitializeComponent();
            _store = store;
        }

        internal string ProfileName { get; private set; } = "";

        private void BtnOkClick(object sender, RoutedEventArgs e)
        {
            string name = tbName.Text.Trim();
            if (name.Length == 0) return;

            char[] invalidFileNameChars = Path.GetInvalidFileNameChars();

            if (name.IndexOfAny(invalidFileNameChars) >= 0 || name.EndsWith("."))
            {
                string sInvalid = string.Join(" ", invalidFileNameChars.Where(c => !char.IsControl(c)));

                MessageBox.Show(
                    this,
                    string.Format("Profile name may not contain\n{0}", sInvalid),
                    "Invalid profile name",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                return;
            }

            if (name == Profile.DefaultName || _store.Exists(name))
            {
                MessageBox.Show(
                    this,
                    "You must enter an unused profile name.",
                    "Profile name already used",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                return;
            }

            ProfileName = name;
            DialogResult = true;
        }

        private void BtnCancelClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            tbName.Focus();
        }

        private void TbNameTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            btnOk.IsEnabled = tbName.Text.Trim().Length != 0;
        }

        private void WindowKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) Close();
            if (e.Key == Key.Enter && btnOk.IsEnabled) BtnOkClick(this, new RoutedEventArgs());
        }
    }
}
