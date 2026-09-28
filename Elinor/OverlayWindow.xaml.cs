using System;
using System.Windows;
using System.Windows.Input;

namespace Elinor
{
    /// <summary>
    /// Compact always-on-top view of the current export. The main window owns all state
    /// and pushes updates here through <see cref="Update"/>.
    /// </summary>
    public partial class OverlayWindow
    {
        internal event Action? ExpandRequested;
        internal event Action? ExitRequested;
        internal event Action? SellClicked;
        internal event Action? BuyClicked;

        public OverlayWindow()
        {
            InitializeComponent();
        }

        internal void Update(string itemName, string sell, string buy, string margin, MarginLevel level)
        {
            lblItemName.Text = itemName;
            lblItemName.ToolTip = itemName;
            lblSell.Text = sell;
            lblBuy.Text = buy;
            lblMargin.Text = margin;

            ThemeManager.ApplyMarginForeground(lblMargin, level);
            ThemeManager.ApplyMarginBorder(brdRoot, level);
        }

        internal void SetCopyStatus(bool? ok, bool visible)
        {
            if (visible)
                ThemeManager.ApplyStatus(tbCopyStatus, ok);
            else
                tbCopyStatus.Text = "";
        }

        /// <summary>Moves the window back on screen if its saved position is on a monitor that's gone.</summary>
        internal void PlaceAt(double? left, double? top, Window fallback)
        {
            Rect screen = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
                SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);

            if (left is double l && top is double t && screen.Contains(new Point(l + 40, t + 20)))
            {
                Left = l;
                Top = t;
            }
            else
            {
                Left = fallback.Left + Math.Max(0, fallback.ActualWidth - 320);
                Top = fallback.Top + 40;
            }
        }

        private void DragWindow(object sender, MouseButtonEventArgs e)
        {
            // DragMove captures the mouse until release, so let the price labels get their click.
            if (e.OriginalSource == lblSell || e.OriginalSource == lblBuy) return;
            if (e.ButtonState == MouseButtonState.Pressed) DragMove();
        }

        private void WindowKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) ExpandRequested?.Invoke();
        }

        private void BtnExpandClick(object sender, RoutedEventArgs e) => ExpandRequested?.Invoke();

        private void BtnCloseClick(object sender, RoutedEventArgs e) => ExitRequested?.Invoke();

        private void LblSellClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            SellClicked?.Invoke();
        }

        private void LblBuyClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            BuyClicked?.Invoke();
        }
    }
}
