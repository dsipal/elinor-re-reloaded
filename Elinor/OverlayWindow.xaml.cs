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
        /// <summary>User picked which price auto copy uses: 1 = sell, -1 = buy.</summary>
        internal event Action<int>? AutoCopyModeRequested;

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

        /// <summary>Marks the row auto copy uses. 0 = auto copy off, 1 = sell, -1 = buy.</summary>
        internal void SetAutoCopyMode(int mode)
        {
            icoSellCopy.Visibility = mode > 0 ? Visibility.Visible : Visibility.Collapsed;
            icoBuyCopy.Visibility = mode < 0 ? Visibility.Visible : Visibility.Collapsed;
            SetEmphasis(lblSellMode, mode > 0);
            SetEmphasis(lblBuyMode, mode < 0);

            string state = mode == 0 ? "Auto copy is off" : "Auto copy: " + (mode > 0 ? "sell" : "buy") + " price";
            pnlSellMode.ToolTip = state + ". Click to auto copy the sell price.";
            pnlBuyMode.ToolTip = state + ". Click to auto copy the buy price.";
        }

        private static void SetEmphasis(System.Windows.Controls.TextBlock label, bool active)
        {
            label.FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal;
            label.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty,
                active ? "TextFillColorPrimaryBrush" : "TextFillColorSecondaryBrush");
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
            // DragMove captures the mouse until release, so let the clickable labels get their click.
            if (e.OriginalSource is DependencyObject source &&
                (IsWithin(source, lblSell) || IsWithin(source, lblBuy) || IsWithin(source, pnlSellMode) || IsWithin(source, pnlBuyMode)))
                return;
            if (e.ButtonState == MouseButtonState.Pressed) DragMove();
        }

        private static bool IsWithin(DependencyObject source, DependencyObject container)
        {
            for (DependencyObject? d = source; d != null;
                 d = d is System.Windows.Media.Visual ? System.Windows.Media.VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d))
                if (d == container) return true;
            return false;
        }

        private void SellModeClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            AutoCopyModeRequested?.Invoke(1);
        }

        private void BuyModeClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            AutoCopyModeRequested?.Invoke(-1);
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
