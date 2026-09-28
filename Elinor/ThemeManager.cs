using System.Windows;
using System.Windows.Controls;

namespace Elinor
{
    internal enum MarginLevel
    {
        None,
        Low,
        Ok,
        Good,
    }

    /// <summary>
    /// Light / dark / follow-Windows switching using WPF's built-in Fluent theme (.NET 9+).
    /// Colours used from code are theme resource keys, so they update when the theme changes.
    /// </summary>
    internal static class ThemeManager
    {
        internal const string System = "System";
        internal const string Light = "Light";
        internal const string Dark = "Dark";

        internal static readonly string[] Modes = { System, Light, Dark };

        internal static void Apply(string mode)
        {
            Application.Current.ThemeMode = mode switch
            {
                Light => ThemeMode.Light,
                Dark => ThemeMode.Dark,
                _ => ThemeMode.System,
            };
        }

        internal static MarginLevel LevelFor(TradeResult? result, Profile profile)
        {
            if (result is not TradeResult r) return MarginLevel.None;
            if (r.Margin / 100 >= profile.marginThreshold) return MarginLevel.Good;
            if (r.Margin / 100 > profile.minimumThreshold) return MarginLevel.Ok;
            return MarginLevel.Low;
        }

        private static string? BrushKey(MarginLevel level) => level switch
        {
            MarginLevel.Good => "SystemFillColorSuccessBrush",
            MarginLevel.Ok => "SystemFillColorCautionBrush",
            MarginLevel.Low => "SystemFillColorCriticalBrush",
            _ => null,
        };

        /// <summary>Colours margin text; <see cref="MarginLevel.None"/> falls back to the normal text colour.</summary>
        internal static void ApplyMarginForeground(TextBlock text, MarginLevel level)
        {
            string? key = BrushKey(level);
            if (key == null)
                text.ClearValue(TextBlock.ForegroundProperty);
            else
                text.SetResourceReference(TextBlock.ForegroundProperty, key);
        }

        internal static void ApplyMarginBorder(Border border, MarginLevel level)
        {
            border.SetResourceReference(Border.BorderBrushProperty, BrushKey(level) ?? "CardStrokeColorDefaultBrush");
        }

        internal static void ApplyStatus(TextBlock icon, bool? ok)
        {
            // Segoe Fluent Icons: Accept, Cancel, Clock.
            icon.Text = ok switch { true => "", false => "", null => "" };
            icon.SetResourceReference(TextBlock.ForegroundProperty, ok switch
            {
                true => "SystemFillColorSuccessBrush",
                false => "SystemFillColorCriticalBrush",
                null => "TextFillColorSecondaryBrush",
            });
        }
    }
}
