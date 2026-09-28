using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Elinor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        private readonly AppSettings _settings = App.Settings;
        private readonly ProfileStore _profiles = new ProfileStore(AppPaths.ProfilesDir);
        private Profile profile = new Profile();

        private DirectoryInfo _logdir;
        private MarketLogWatcher? _watcher;
        private MarketSnapshot? _lastSnapshot;
        private OverlayWindow? _overlay;

        private double _buy = -1;
        private double _sell = -1;
        private MarginLevel _marginLevel = MarginLevel.None;
        private bool? _copyStatus;
        private bool _copyStatusVisible;

        /// <summary>False while controls are being filled at startup, so handlers don't act on it.</summary>
        private bool _ready;
        private bool _exiting;

        public MainWindow()
        {
            InitializeComponent();

            _logdir = new DirectoryInfo(string.IsNullOrWhiteSpace(_settings.LogPath)
                ? AppPaths.DefaultMarketLogDir()
                : _settings.LogPath);
        }

        #region Startup / shutdown

        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            tbCorpStanding.TextChanged += TbCorpStandingTextChanged;
            tbFactionStanding.TextChanged += TbFactionStandingTextChanged;
            tbCorpStanding.LostFocus += TbStandingOnLostFocus;
            tbFactionStanding.LostFocus += TbStandingOnLostFocus;

            cbBrokerRelations.SelectionChanged += CbBrokerRelationsSelectionChanged;
            cbAccounting.SelectionChanged += CbAccountingSelectionChanged;

            cbSellRange.SelectionChanged += cbSellRangeSelectionChanged;
            cbBuyRange.SelectionChanged += cbBuyRangeSelectionChanged;

            for (int i = 0; i < 6; i++)
            {
                cbBrokerRelations.Items.Add(i);
                cbAccounting.Items.Add(i);
            }

            foreach (Profile.Ranges range in Enum.GetValues<Profile.Ranges>())
            {
                var memInfo = typeof(Profile.Ranges).GetMember(range.ToString());
                var attributes = memInfo[0].GetCustomAttributes(typeof(DescriptionAttribute), false);
                var description = ((DescriptionAttribute)attributes[0]).Description;

                var item = new ComboboxItem { Text = description, Value = (int)range };

                cbBuyRange.Items.Add(item);
                cbSellRange.Items.Add(item);
            }

            // App settings tab
            foreach (string mode in ThemeManager.Modes)
                cbTheme.Items.Add(new ComboboxItem { Text = mode == ThemeManager.System ? "Use Windows setting" : mode, Value = Array.IndexOf(ThemeManager.Modes, mode) });
            cbTheme.SelectedIndex = Math.Max(0, Array.IndexOf(ThemeManager.Modes, _settings.Theme));

            slOverlayOpacity.Value = Math.Clamp(_settings.OverlayOpacity * 100, slOverlayOpacity.Minimum, slOverlayOpacity.Maximum);
            lblOverlayOpacity.Text = string.Format("{0:0}%", slOverlayOpacity.Value);
            btnUpdate.IsChecked = _settings.CheckForUpdates;
            lblAppVersion.Text = "Elinor " + Updates.CurrentVersion.ToString(3);

            if (!_settings.LegacyImportDone)
            {
                int imported = LegacyProfileImporter.ImportAll(AppPaths.LegacyProfileDirs(), _profiles);
                Log.Info("Legacy profile import: " + imported + " imported");
                _settings.LegacyImportDone = true;
                _settings.Save();
            }

            profile = new Profile();
            cbProfiles.Items.Add(profile);
            UpdateProfiles();

            // Select by name; setting cbProfiles.Text to an unknown name left SelectedItem null.
            cbProfiles.SelectedItem = cbProfiles.Items.OfType<Profile>()
                .FirstOrDefault(p => p.profileName == _settings.SelectedProfile) ?? profile;

            SetPinned(_settings.Pin);

            if (_settings.AutoCopy == 0)
            {
                cbAutoCopy.IsChecked = false;
                rbSell.IsChecked = true;
            }
            else
            {
                cbAutoCopy.IsChecked = true;
                rbSell.IsChecked = _settings.AutoCopy > 0;
                rbBuy.IsChecked = _settings.AutoCopy < 0;
            }

            _ready = true;

            if (_settings.CheckForUpdates) _ = Updates.CheckForUpdatesAsync(this);

            _logdir.Refresh();
            if (!_logdir.Exists)
            {
                selectLogPath(_logdir.FullName);
            }
            else
            {
                StartWatcher();
            }

            UpdateStatus();

            if (_settings.OverlayOpen)
                Dispatcher.BeginInvoke(new Action(ShowOverlay));
        }

        private void WindowClosed(object sender, EventArgs e)
        {
            _exiting = true;
            _settings.OverlayOpen = _overlay != null;
            _overlay?.Close();
            _watcher?.Dispose();

            _settings.Pin = btnStayOnTop.IsChecked == true;

            if (cbAutoCopy.IsChecked != true)
                _settings.AutoCopy = 0;
            else if (rbSell.IsChecked == true)
                _settings.AutoCopy = 1;
            else if (rbBuy.IsChecked == true)
                _settings.AutoCopy = -1;

            _settings.SelectedProfile = profile.profileName;
            _settings.Save();
            _profiles.Save(profile);
        }

        #endregion

        #region Market log watching

        public void selectLogPath(string logdir)
        {
            var dlg = new SelectLogPathWindow(logdir) { Owner = this, Topmost = Topmost };

            if (dlg.ShowDialog() == true && dlg.Logpath != null)
            {
                _logdir = dlg.Logpath;

                _settings.LogPath = _logdir.FullName;
                _settings.Save();

                StartWatcher();
            }
        }

        private void StartWatcher()
        {
            _watcher?.Dispose();
            _watcher = null;

            try
            {
                _logdir.Refresh();
                if (!_logdir.Exists) _logdir.Create();

                _watcher = new MarketLogWatcher(_logdir.FullName, OnExportStarted, OnExportParsed, OnExportFailed);
                Log.Info("Watching " + _logdir.FullName);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException)
            {
                Log.Error("Cannot watch " + _logdir.FullName, ex);
                MessageBox.Show(this,
                    "Elinor cannot watch the market log folder:\n" + _logdir.FullName + "\n\n" + ex.Message +
                    "\n\nPick another one in Settings > Market logs.",
                    "Elinor", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            UpdateStatus();
        }

        private void OnExportStarted(string path)
        {
            lblItemName.Text = "Fetching...";
            lblItemName.ToolTip = null;

            if (cbAutoCopy.IsChecked == true)
                SetCopyStatus(null);

            UpdateOverlay();
        }

        private async void OnExportParsed(MarketSnapshot snapshot)
        {
            _lastSnapshot = snapshot;
            Recalculate();
            RefreshHubSuggestions();
            UpdateStatus();
            await AutoCopyAsync();
        }

        private void OnExportFailed(string path, Exception? ex)
        {
            lblItemName.Text = "Could not read export";
            lblItemName.ToolTip = ex == null ? "The file was removed before it could be read" : ex.Message;

            if (cbAutoCopy.IsChecked == true)
                SetCopyStatus(false);

            UpdateStatus();
            UpdateOverlay();
        }

        private void UpdateStatus()
        {
            long size = CacheTools.MarketLogsSize(_logdir);
            string sizeText = String.Format("{0:n0} KB", size / 1024);

            tbStatus.Text = (_watcher != null ? "Watching " : "Not watching ") + _logdir.FullName + "  ·  " + sizeText;
            tbStatus.ToolTip = _logdir.FullName;
            tbLogPath.Text = _logdir.FullName;
            lblLogSize.Text = "Market logs use " + sizeText + ".";
        }

        #endregion

        #region Prices and results

        /// <summary>Recomputes prices for the current profile from the last export (no file I/O).</summary>
        private void Recalculate()
        {
            if (_lastSnapshot == null)
            {
                resetCurrentExportValues();
                return;
            }

            var hubs = profile.HubIds();
            _sell = _lastSnapshot.BestSell((Profile.Ranges)profile.sellRange, hubs);
            _buy = _lastSnapshot.BestBuy((Profile.Ranges)profile.buyRange, hubs);

            string itemName = _lastSnapshot.ItemName;
            lblItemName.Text = itemName.Length != 0 ? itemName : "Unknown";
            lblItemName.ToolTip = itemName.Length != 0 ? itemName : "Product not found";

            lblSell.Text = _sell >= 0
                                ? String.Format("{0:n} ISK", _sell)
                                : "No orders in range";
            lblBuy.Text = _buy >= 0
                                ? String.Format("{0:n} ISK", _buy)
                                : "No orders in range";

            ShowTradeResult(TradeCalculator.Calculate(_sell, _buy, profile));
            UpdateStepExample();
        }

        /// <summary>Settings changed: refresh numbers if an export is showing.</summary>
        private void RecalculateIfReady()
        {
            if (_ready && _lastSnapshot != null) Recalculate();
        }

        private void ShowTradeResult(TradeResult? result)
        {
            _marginLevel = ThemeManager.LevelFor(result, profile);
            ThemeManager.ApplyMarginForeground(lblMargin, _marginLevel);
            ThemeManager.ApplyMarginBorder(brdImportant, _marginLevel);

            if (result is not TradeResult r)
            {
                lblRevenue.Text = "- ISK";
                lblCoS.Text = "- ISK";
                lblProfit.Text = "- ISK";
                lblMargin.Text = "- %";
                lblMarkup.Text = "- %";
                UpdateOverlay();
                return;
            }

            lblRevenue.Text = String.Format("{0:n} ISK", r.Revenue);
            lblCoS.Text = String.Format("{0:n} ISK", r.CostOfSales);

            lblBuyOrderCost.Text = String.Format("{0:n} ISK", r.BuyOrderCost);
            lblSellOrderCost.Text = String.Format("{0:n} ISK", r.SellOrderCost);

            lblProfit.Text = String.Format("{0:n} ISK", r.Profit);

            lblMargin.Text = Math.Abs(r.Margin) < 10000
                                    ? String.Format("{0:n}%", r.Margin)
                                    : (r.Margin > 0 ? "∞%" : "-∞%");

            lblMarkup.Text = Math.Abs(r.Markup) < 10000
                                    ? String.Format("{0:n}%", r.Markup)
                                    : (r.Markup > 0 ? "∞%" : "-∞%");

            UpdateOverlay();
        }

        private void resetCurrentExportValues()
        {
            _sell = -1;
            _buy = -1;
            ShowTradeResult(null);

            lblItemName.Text = "No item selected";
            lblItemName.ToolTip = null;
            lblSell.Text = "0.00 ISK";
            lblBuy.Text = "0.00 ISK";
            lblBuyOrderCost.Text = "0.00 ISK";
            lblSellOrderCost.Text = "0.00 ISK";
            UpdateStepExample();
            UpdateOverlay();
        }

        #endregion

        #region Clipboard

        private async Task AutoCopyAsync()
        {
            if (cbAutoCopy.IsChecked != true) return;

            bool isSell = rbSell.IsChecked == true;
            double price = isSell
                ? ClipboardTools.GetSellPrice(_sell, profile)
                : ClipboardTools.GetBuyPrice(_buy, profile);

            bool copied = await ClipboardTools.TrySetPriceAsync(price);
            bool hasPrice = isSell ? _sell > 0 : _buy > 0;

            SetCopyStatus(copied && hasPrice);
        }

        private void SetCopyStatus(bool? ok)
        {
            _copyStatus = ok;
            _copyStatusVisible = true;
            ThemeManager.ApplyStatus(tbCopyStatus, ok);
            _overlay?.SetCopyStatus(ok, true);
        }

        private void ClearCopyStatus()
        {
            _copyStatusVisible = false;
            tbCopyStatus.Text = "";
            _overlay?.SetCopyStatus(null, false);
        }

        private async void LblSellMouseDown(object sender, MouseButtonEventArgs e) => await CopySellAsync();

        private async void LblBuyMouseDown(object sender, MouseButtonEventArgs e) => await CopyBuyAsync();

        private async Task CopySellAsync()
        {
            await ClipboardTools.TrySetPriceAsync(ClipboardTools.GetSellPrice(_sell, profile));
        }

        private async Task CopyBuyAsync()
        {
            await ClipboardTools.TrySetPriceAsync(ClipboardTools.GetBuyPrice(_buy, profile));
        }

        /// <summary>0 = auto copy off, 1 = sell price, -1 = buy price (same encoding as AppSettings.AutoCopy).</summary>
        private int AutoCopyMode => cbAutoCopy.IsChecked != true ? 0 : rbBuy.IsChecked == true ? -1 : 1;

        private void CbAutoCopyChecked(object sender, RoutedEventArgs e)
        {
            gbAutocopy.IsEnabled = true;
            ClearCopyStatus();
            _overlay?.SetAutoCopyMode(AutoCopyMode);
        }

        private void CbAutoCopyUnchecked(object sender, RoutedEventArgs e)
        {
            gbAutocopy.IsEnabled = false;
            ClearCopyStatus();
            _overlay?.SetAutoCopyMode(AutoCopyMode);
        }

        /// <summary>From the overlay: turn auto copy on for the chosen price.</summary>
        private void SetAutoCopyModeFromOverlay(int mode)
        {
            (mode < 0 ? rbBuy : rbSell).IsChecked = true;
            cbAutoCopy.IsChecked = true;
            _overlay?.SetAutoCopyMode(AutoCopyMode);
        }

        private void AutoCopy(object sender, ExecutedRoutedEventArgs e)
        {
            cbAutoCopy.IsChecked = !cbAutoCopy.IsChecked;
        }

        private async void RbChecked(object sender, RoutedEventArgs e)
        {
            _overlay?.SetAutoCopyMode(AutoCopyMode);

            // Also fires while settings are restored at startup; don't wipe the clipboard then.
            if (_lastSnapshot == null) return;

            double price = rbSell.IsChecked == true
                               ? ClipboardTools.GetSellPrice(_sell, profile)
                               : ClipboardTools.GetBuyPrice(_buy, profile);
            await ClipboardTools.TrySetPriceAsync(price);
        }

        #endregion

        #region Compact overlay

        private void CompactMode(object sender, ExecutedRoutedEventArgs e) => ShowOverlay();

        private void BtnCompactClick(object sender, RoutedEventArgs e) => ShowOverlay();

        private void ShowOverlay()
        {
            if (_overlay != null)
            {
                _overlay.Activate();
                return;
            }

            _overlay = new OverlayWindow { Opacity = _settings.OverlayOpacity };
            _overlay.PlaceAt(_settings.OverlayLeft, _settings.OverlayTop, this);
            _overlay.ExpandRequested += CloseOverlay;
            _overlay.ExitRequested += () => Close();
            _overlay.SellClicked += async () => await CopySellAsync();
            _overlay.BuyClicked += async () => await CopyBuyAsync();
            _overlay.AutoCopyModeRequested += SetAutoCopyModeFromOverlay;
            _overlay.Closed += OverlayClosed;

            UpdateOverlay();
            _overlay.SetAutoCopyMode(AutoCopyMode);
            if (_copyStatusVisible) _overlay.SetCopyStatus(_copyStatus, true);

            _overlay.Show();
            Hide();
        }

        private void CloseOverlay() => _overlay?.Close();

        private void OverlayClosed(object? sender, EventArgs e)
        {
            if (_overlay != null)
            {
                _settings.OverlayLeft = _overlay.Left;
                _settings.OverlayTop = _overlay.Top;
                _overlay = null;
            }

            if (_exiting) return;

            _settings.Save();
            Show();
            Activate();
        }

        private void UpdateOverlay()
        {
            _overlay?.Update(lblItemName.Text, lblSell.Text, lblBuy.Text, lblMargin.Text, _marginLevel);
        }

        private void SlOverlayOpacityChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (lblOverlayOpacity == null) return; // during InitializeComponent

            lblOverlayOpacity.Text = string.Format("{0:0}%", e.NewValue);
            if (!_ready) return;

            _settings.OverlayOpacity = e.NewValue / 100;
            if (_overlay != null) _overlay.Opacity = _settings.OverlayOpacity;
            _settings.Save();
        }

        #endregion

        #region App settings

        private void CbThemeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_ready || cbTheme.SelectedItem is not ComboboxItem item) return;

            _settings.Theme = ThemeManager.Modes[item.Value];
            ThemeManager.Apply(_settings.Theme);
            _settings.Save();
        }

        private void SetPinned(bool pinned)
        {
            Topmost = pinned;
            btnStayOnTop.IsChecked = pinned;
            cbStayOnTop.IsChecked = pinned;
            _settings.Pin = pinned;
        }

        private void BtnStayOnTopClick(object sender, RoutedEventArgs e)
        {
            SetPinned(btnStayOnTop.IsChecked == true);
        }

        private void CbStayOnTopChanged(object sender, RoutedEventArgs e)
        {
            if (_ready) SetPinned(cbStayOnTop.IsChecked == true);
        }

        private void PinWindow(object sender, ExecutedRoutedEventArgs e)
        {
            SetPinned(btnStayOnTop.IsChecked != true);
        }

        private async void BtnUpdateClick(object sender, RoutedEventArgs e)
        {
            if (!_ready) return;

            _settings.CheckForUpdates = btnUpdate.IsChecked == true;
            _settings.Save();

            if (_settings.CheckForUpdates) await Updates.CheckForUpdatesAsync(this);
        }

        private async void BtnCheckNowClick(object sender, RoutedEventArgs e)
        {
            await Updates.CheckForUpdatesAsync(this, manual: true);
        }

        private void BtnPath(object sender, RoutedEventArgs e)
        {
            selectLogPath(_logdir.FullName);
        }

        private void BtnOpenLogFolderClick(object sender, RoutedEventArgs e)
        {
            _logdir.Refresh();
            if (_logdir.Exists) MiscTools.OpenUrl(_logdir.FullName);
        }

        private void BtnClearLogsClick(object sender, RoutedEventArgs e)
        {
            CacheTools.ClearMarketLogs(_logdir);
            UpdateStatus();
        }

        private void BtnAboutClick(object sender, RoutedEventArgs e)
        {
            var abt = new AboutWindow { Owner = this, Topmost = Topmost };
            abt.ShowDialog();
        }

        private void MiSubmitBugClick(object sender, RoutedEventArgs e)
        {
            MiscTools.OpenUrl(@"https://github.com/dsipal/elinor-re-reloaded/issues");
        }

        #endregion

        #region Profiles

        private void UpdateProfiles()
        {
            foreach (Profile p in _profiles.LoadAll())
            {
                cbProfiles.Items.Add(p);
            }
        }

        private async void CbProfilesSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbProfiles.SelectedItem is not Profile selected) return;

            btnDelete.IsEnabled = selected.profileName != Profile.DefaultName;

            if (!ReferenceEquals(selected, profile))
            {
                _profiles.Save(profile);
                profile = selected;
            }

            updateSettingsDisplay();

            if (_lastSnapshot != null)
            {
                Recalculate();
                await AutoCopyAsync();
            }
            else
            {
                resetCurrentExportValues();
            }
        }

        private void BtnNewClick(object sender, RoutedEventArgs e)
        {
            var window = new ProfileNameWindow(_profiles) { Owner = this, Topmost = Topmost };

            if (window.ShowDialog() != true) return;

            var newProfile = new Profile { profileName = window.ProfileName };
            _profiles.Save(newProfile);
            cbProfiles.Items.Add(newProfile);
            cbProfiles.SelectedItem = newProfile;
            tcMain.SelectedItem = tiSettings;
        }

        private void BtnDeleteClick(object sender, RoutedEventArgs e)
        {
            if (cbProfiles.SelectedItem is not Profile toDelete || toDelete.profileName == Profile.DefaultName) return;

            if (MessageBox.Show(this, "Delete the profile \"" + toDelete.profileName + "\"?", "Delete profile",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            // "Default" is always item 0, so there is always a previous item.
            int i = cbProfiles.SelectedIndex;
            var previous = (Profile)cbProfiles.Items[i - 1];

            // Switch first so the selection handler doesn't re-save the profile being deleted.
            profile = previous;
            cbProfiles.SelectedIndex = i - 1;
            cbProfiles.Items.RemoveAt(i);
            _profiles.Delete(toDelete.profileName);
        }

        private void TiSettingsLostFocus(object sender, RoutedEventArgs e)
        {
            _profiles.Save(profile);
        }

        private void tiTradeSettingsLostFocus(object sender, RoutedEventArgs e)
        {
            _profiles.Save(profile);
        }

        private void tiRangeSettingsLostFocus(object sender, RoutedEventArgs e)
        {
            _profiles.Save(profile);
        }

        private void updateSettingsDisplay()
        {
            // Charaters settings
            tbPreferred.Text = (profile.marginThreshold * 100).ToString(CultureInfo.InvariantCulture);
            tbMinimum.Text = (profile.minimumThreshold * 100).ToString(CultureInfo.InvariantCulture);

            tbCorpStanding.Text =
                string.Format(
                    CultureInfo.InvariantCulture, "{0:n2}",
                    profile.corpStanding
                );
            tbFactionStanding.Text =
                string.Format(
                    CultureInfo.InvariantCulture, "{0:n2}",
                    profile.factionStanding
                );

            cbBrokerRelations.SelectedIndex = profile.brokerRelations;
            cbAccounting.SelectedIndex = profile.accounting;

            // Range settings
            cbSellRange.SelectedIndex = profile.sellRange;
            cbBuyRange.SelectedIndex = profile.buyRange;
            RefreshHubList();

            // Broker settings
            cbUseCustomBuyBroker.IsChecked = profile.useBuyCustomBroker;
            tbCustomBuyBroker.Text = (profile.buyCustomBroker * 100).ToString(CultureInfo.InvariantCulture);

            cbUseCustomSellBroker.IsChecked = profile.useSellCustomBroker;
            tbCustomSellBroker.Text = (profile.sellCustomBroker * 100).ToString(CultureInfo.InvariantCulture);

            // Price step
            tbCustomStep.Text = profile.customPriceStep.ToString(CultureInfo.InvariantCulture);
            switch ((Profile.PriceSteps)profile.priceStep)
            {
                case Profile.PriceSteps.MINIMUM: rbStepMinimum.IsChecked = true; break;
                case Profile.PriceSteps.CUSTOM: rbStepCustom.IsChecked = true; break;
                default: rbStepSmart.IsChecked = true; break;
            }
            UpdateStepExample();
        }

        #endregion

        #region Character settings

        private void CbBrokerRelationsSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbBrokerRelations.SelectedIndex < 0) return;
            profile.brokerRelations = cbBrokerRelations.SelectedIndex;
            UpdateBrokerFee();
            RecalculateIfReady();
        }

        private void CbAccountingSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbAccounting.SelectedIndex < 0) return;
            profile.accounting = cbAccounting.SelectedIndex;
            lblSalesTax.Text = String.Format("Sales tax: {0:n}%", TradeCalculator.SalesTax(profile.accounting) * 100);
            RecalculateIfReady();
        }

        private void UpdateBrokerFee()
        {
            lblBrokerRelations.Text = String.Format("Broker fee: {0:n}%",
                TradeCalculator.NpcBroker(profile) * 100);
        }

        private void TbStandingOnLostFocus(object sender, RoutedEventArgs routedEventArgs)
        {
            var tbSender = (TextBox)sender;
            if (double.TryParse(tbSender.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double standing))
            {
                if (standing > 10) tbSender.Text = "10";
                if (standing < -10) tbSender.Text = "-10";
            }
        }

        private void TbCorpStandingTextChanged(object sender, TextChangedEventArgs e)
        {
            if (double.TryParse(tbCorpStanding.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double standing))
            {
                bool valid = standing <= 10 && standing >= -10;
                errCorpStanding.Visibility = valid ? Visibility.Collapsed : Visibility.Visible;
                if (!valid) return;

                profile.corpStanding = standing;
                UpdateBrokerFee();
                RecalculateIfReady();
            }
        }

        private void TbFactionStandingTextChanged(object sender, TextChangedEventArgs e)
        {
            if (double.TryParse(tbFactionStanding.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double standing))
            {
                bool valid = standing <= 10 && standing >= -10;
                errFactionStanding.Visibility = valid ? Visibility.Collapsed : Visibility.Visible;
                if (!valid) return;

                profile.factionStanding = standing;
                UpdateBrokerFee();
                RecalculateIfReady();
            }
        }

        private void BtnResetCharClick(object sender, RoutedEventArgs e)
        {
            profile.accounting = 0;
            profile.brokerRelations = 0;

            profile.corpStanding = 0;
            profile.factionStanding = 0;

            _profiles.Save(profile);
            updateSettingsDisplay();
        }

        #endregion

        #region Trade settings

        private void BtnResetTradeClick(object sender, RoutedEventArgs e)
        {
            profile.marginThreshold = 0.1;
            profile.minimumThreshold = 0.02;

            profile.useBuyCustomBroker = false;
            profile.buyCustomBroker = 0.01;
            profile.useSellCustomBroker = false;
            profile.sellCustomBroker = 0.01;

            profile.priceStep = (int)Profile.PriceSteps.SMART;
            profile.customPriceStep = 1000;

            _profiles.Save(profile);
            updateSettingsDisplay();
            RecalculateIfReady();
        }

        private void RbStepChecked(object sender, RoutedEventArgs e)
        {
            if (rbStepCustom == null || tbCustomStep == null) return; // during InitializeComponent

            profile.priceStep = rbStepCustom.IsChecked == true ? (int)Profile.PriceSteps.CUSTOM
                : rbStepMinimum.IsChecked == true ? (int)Profile.PriceSteps.MINIMUM
                : (int)Profile.PriceSteps.SMART;

            tbCustomStep.IsEnabled = rbStepCustom.IsChecked == true;
            UpdateStepExample();
        }

        private void TbCustomStepChanged(object sender, TextChangedEventArgs e)
        {
            if (double.TryParse(tbCustomStep.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double d) && d >= 0.01)
            {
                profile.customPriceStep = d;
                UpdateStepExample();
            }
        }

        private void UpdateStepExample()
        {
            if (lblStepExample == null) return;

            double sell = _sell > 0 ? _sell : 10_750_000;
            double buy = _buy > 0 ? _buy : 8_111_000;

            lblStepExample.Text = String.Format(
                "{0}sell {1:n2} → {2:n2}, buy {3:n2} → {4:n2}",
                _sell > 0 ? "Current item: " : "Example: ",
                sell, ClipboardTools.GetSellPrice(sell, profile),
                buy, ClipboardTools.GetBuyPrice(buy, profile));
        }

        private void TbPreferredTextChanged(object sender, TextChangedEventArgs e)
        {
            if (double.TryParse(tbPreferred.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double d))
            {
                d /= 100;

                if (d <= 1 && d >= 0)
                {
                    profile.marginThreshold = d;
                    RecalculateIfReady();
                }
            }
        }

        private void TbMinimumTextChanged(object sender, TextChangedEventArgs e)
        {
            if (double.TryParse(tbMinimum.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double d))
            {
                d /= 100;

                if (d <= profile.marginThreshold && d >= 0)
                {
                    profile.minimumThreshold = d;
                    RecalculateIfReady();
                }
            }
        }

        private void TbCustomBuyBrokerChanged(object sender, TextChangedEventArgs e)
        {
            if (double.TryParse(tbCustomBuyBroker.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double d))
            {
                d /= 100;

                if (d <= 1 && d >= 0)
                {
                    profile.buyCustomBroker = d;
                    RecalculateIfReady();
                }
            }
        }

        private void TbCustomSellBrokerChanged(object sender, TextChangedEventArgs e)
        {
            if (double.TryParse(tbCustomSellBroker.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double d))
            {
                d /= 100;

                if (d <= 1 && d >= 0)
                {
                    profile.sellCustomBroker = d;
                    RecalculateIfReady();
                }
            }
        }

        private void cbUseCustomBuyBrokerChecked(object sender, RoutedEventArgs e)
        {
            profile.useBuyCustomBroker = true;
            RecalculateIfReady();
        }

        private void cbUseCustomBuyBrokerUnchecked(object sender, RoutedEventArgs e)
        {
            profile.useBuyCustomBroker = false;
            RecalculateIfReady();
        }

        private void cbUseCustomSellBrokerChecked(object sender, RoutedEventArgs e)
        {
            profile.useSellCustomBroker = true;
            RecalculateIfReady();
        }

        private void cbUseCustomSellBrokerUnchecked(object sender, RoutedEventArgs e)
        {
            profile.useSellCustomBroker = false;
            RecalculateIfReady();
        }

        #endregion

        #region Range settings and trade hubs

        private void cbSellRangeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbSellRange.SelectedItem is ComboboxItem item)
            {
                profile.sellRange = item.Value;
                RecalculateIfReady();
            }
        }

        private void cbBuyRangeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbBuyRange.SelectedItem is ComboboxItem item)
            {
                profile.buyRange = item.Value;
                RecalculateIfReady();
            }
        }

        private void RefreshHubList()
        {
            lbHubs.ItemsSource = null;
            lbHubs.ItemsSource = profile.hubs;
            RefreshHubSuggestions();
        }

        /// <summary>Offers the stations from the last export that aren't hubs yet.</summary>
        private void RefreshHubSuggestions()
        {
            string typed = cbHubStation.Text;
            cbHubStation.Items.Clear();

            if (_lastSnapshot != null)
            {
                var hubs = profile.HubIds();
                foreach (var (stationId, orders) in _lastSnapshot.Stations().Where(s => !hubs.Contains(s.StationId)))
                {
                    cbHubStation.Items.Add(new ComboBoxItem
                    {
                        Content = stationId.ToString(CultureInfo.InvariantCulture),
                        ToolTip = orders + " orders in the last export",
                    });
                }
            }

            cbHubStation.Text = typed;
        }

        private void LbHubsSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            btnRemoveHub.IsEnabled = lbHubs.SelectedItem != null;
        }

        private void BtnAddHubClick(object sender, RoutedEventArgs e)
        {
            string text = cbHubStation.Text.Trim();

            if (!long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long id) || id <= 0)
            {
                ShowHubError("Enter a numeric station ID, e.g. 60003760 for Jita 4-4.");
                return;
            }

            if (profile.hubs.Any(h => h.id == id))
            {
                ShowHubError("That station is already a hub.");
                return;
            }

            errHub.Visibility = Visibility.Collapsed;
            profile.hubs.Add(new HubStation { id = id, name = tbHubName.Text.Trim() });
            cbHubStation.Text = "";
            tbHubName.Text = "";
            HubsChanged();
        }

        private void ShowHubError(string message)
        {
            errHub.Text = message;
            errHub.Visibility = Visibility.Visible;
        }

        private void BtnRemoveHubClick(object sender, RoutedEventArgs e)
        {
            if (lbHubs.SelectedItem is not HubStation hub) return;

            profile.hubs.Remove(hub);
            HubsChanged();
        }

        private void BtnResetHubsClick(object sender, RoutedEventArgs e)
        {
            profile.hubs = HubStation.Defaults();
            errHub.Visibility = Visibility.Collapsed;
            HubsChanged();
        }

        private void HubsChanged()
        {
            _profiles.Save(profile);
            RefreshHubList();
            RecalculateIfReady();
        }

        #endregion
    }
}
