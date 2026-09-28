using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;

namespace Elinor
{
    internal static class ClipboardTools
    {
        private const int Attempts = 5;

        /// <summary>
        /// Copies the price to the clipboard. The clipboard is often briefly held by another
        /// process (clipboard history, cloud clipboard, EVE itself), which makes every clipboard
        /// call throw CLIPBRD_E_CANT_OPEN. We retry without blocking the UI and never throw.
        /// Must be called on the UI (STA) thread.
        /// </summary>
        internal static async Task<bool> TrySetPriceAsync(double d)
        {
            string text = FormatPrice(d);

            for (int attempt = 1; attempt <= Attempts; attempt++)
            {
                try
                {
                    if (text.Length == 0)
                        Clipboard.Clear();
                    else
                        Clipboard.SetDataObject(text, copy: true);
                    return true;
                }
                catch (Exception ex) when (ex is ExternalException || ex is COMException)
                {
                    if (attempt == Attempts)
                    {
                        Log.Warn("Clipboard unavailable, giving up", ex);
                        return false;
                    }

                    await Task.Delay(50 * attempt);
                }
            }

            return false;
        }

        internal static string FormatPrice(double d)
        {
            return d > .01 ? Math.Round(d, 2).ToString(CultureInfo.InvariantCulture) : string.Empty;
        }

        internal static double GetSellPrice(double sell, Profile? settings)
        {
            if (settings == null || sell <= 0) return .0;

            return sell - PriceStep(sell, settings);
        }

        internal static double GetBuyPrice(double buy, Profile? settings)
        {
            if (settings == null || buy <= 0) return .0;

            return buy + PriceStep(buy, settings);
        }

        /// <summary>How far to undercut / outbid <paramref name="price"/>. Never less than 0.01 ISK.</summary>
        internal static double PriceStep(double price, Profile settings)
        {
            switch ((Profile.PriceSteps)settings.priceStep)
            {
                case Profile.PriceSteps.MINIMUM:
                    return 0.01;
                case Profile.PriceSteps.CUSTOM:
                    return Math.Max(settings.customPriceStep, 0.01);
                default:
                    // One unit in the 4th significant digit: 10,750,000 -> 10,000 step.
                    return Math.Max(Math.Pow(10, Math.Floor(Math.Log10(price / 1000))), 0.01);
            }
        }
    }
}
