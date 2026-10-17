using System.Globalization;
using FufuLauncher.Helpers;
using FufuLauncher.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace FufuLauncher.Controls;

public sealed partial class ServiceUptimeGraph : UserControl
{
    private const double Gap = 2;
    private const double StripHeight = 26;

    private static readonly SolidColorBrush DarkStrongGreen = new(Color.FromArgb(255, 57, 211, 83));
    private static readonly SolidColorBrush DarkMidGreen = new(Color.FromArgb(255, 46, 160, 67));
    private static readonly SolidColorBrush DarkAmber = new(Color.FromArgb(255, 210, 153, 34));
    private static readonly SolidColorBrush DarkRed = new(Color.FromArgb(255, 248, 81, 73));
    private static readonly SolidColorBrush DarkDeepRed = new(Color.FromArgb(255, 218, 54, 51));
    private static readonly SolidColorBrush DarkEmpty = new(Color.FromArgb(255, 48, 54, 61));
    private static readonly SolidColorBrush LightStrongGreen = new(Color.FromArgb(255, 45, 164, 78));
    private static readonly SolidColorBrush LightMidGreen = new(Color.FromArgb(255, 74, 194, 107));
    private static readonly SolidColorBrush LightAmber = new(Color.FromArgb(255, 191, 135, 0));
    private static readonly SolidColorBrush LightRed = new(Color.FromArgb(255, 207, 34, 46));
    private static readonly SolidColorBrush LightDeepRed = new(Color.FromArgb(255, 164, 14, 38));
    private static readonly SolidColorBrush LightEmpty = new(Color.FromArgb(255, 234, 238, 242));
    private static readonly SolidColorBrush DarkSecondary = new(Color.FromArgb(179, 255, 255, 255));
    private static readonly SolidColorBrush LightSecondary = new(Color.FromArgb(153, 0, 0, 0));

    public ServiceUptimeGraph()
    {
        InitializeComponent();

        Loaded += (_, _) => Render();
        DataContextChanged += (_, _) => Render();
        ActualThemeChanged += (_, _) => Render();
    }

    private void Render()
    {
        RootGrid.Children.Clear();
        RootGrid.ColumnDefinitions.Clear();
        RootGrid.RowDefinitions.Clear();

        RootGrid.ColumnSpacing = Gap;
        RootGrid.RowSpacing = 4;

        var isDark = ActualTheme == ElementTheme.Dark;
        var secondary = isDark ? DarkSecondary : LightSecondary;

        var days = DataContext switch
        {
            ServiceStatusMonitor monitor => monitor.Days,
            ServiceMonitorDetail detail => detail.Days,
            _ => null
        };

        if (days == null || days.Count == 0)
        {
            RootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(StripHeight) });
            var empty = new TextBlock
            {
                Text = "ServiceStatus_NoData".GetLocalized(),
                FontSize = 12,
                Foreground = secondary,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(empty, 0);
            RootGrid.Children.Add(empty);
            return;
        }

        var culture = GetCulture();

        RootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
        RootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(StripHeight) });

        for (var i = 0; i < days.Count; i++)
        {
            RootGrid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(1, GridUnitType.Star)
            });
        }

        var lastMonth = -1;
        for (var i = 0; i < days.Count; i++)
        {
            var day = days[i];
            if (i != 0 && day.Date.Day != 1)
            {
                continue;
            }

            if (day.Date.Month == lastMonth)
            {
                continue;
            }

            lastMonth = day.Date.Month;
            var monthLabel = new TextBlock
            {
                Text = day.Date.ToString("MMM", culture),
                FontSize = 9,
                Foreground = secondary,
                VerticalAlignment = VerticalAlignment.Bottom,
                HorizontalAlignment = HorizontalAlignment.Left,
                IsHitTestVisible = false
            };
            Grid.SetRow(monthLabel, 0);
            Grid.SetColumn(monthLabel, i);
            Grid.SetColumnSpan(monthLabel, Math.Min(6, days.Count - i));
            RootGrid.Children.Add(monthLabel);
        }

        for (var i = 0; i < days.Count; i++)
        {
            var day = days[i];
            var ratio = day.Ratio.ToString("0.00", CultureInfo.InvariantCulture);
            var dateText = day.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var tooltip = day.HasData
                ? $"{dateText} · {ratio}%"
                : $"{dateText} · {"ServiceStatus_NoData".GetLocalized()}";

            var block = new Border
            {
                Background = BrushFor(day, isDark),
                CornerRadius = new CornerRadius(2),
                Height = StripHeight,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            ToolTipService.SetToolTip(block, tooltip);
            AutomationProperties.SetName(block, tooltip);
            Grid.SetColumn(block, i);
            Grid.SetRow(block, 1);
            RootGrid.Children.Add(block);
        }
    }

    private static Brush BrushFor(ServiceStatusDay day, bool isDark)
    {
        if (!day.HasData)
        {
            return isDark ? DarkEmpty : LightEmpty;
        }

        if (day.Ratio >= 99.5)
        {
            return isDark ? DarkStrongGreen : LightStrongGreen;
        }

        if (day.Ratio >= 99)
        {
            return isDark ? DarkMidGreen : LightMidGreen;
        }

        if (day.Ratio >= 95)
        {
            return isDark ? DarkAmber : LightAmber;
        }

        if (day.Ratio > 0)
        {
            return isDark ? DarkRed : LightRed;
        }

        return isDark ? DarkDeepRed : LightDeepRed;
    }

    private static CultureInfo GetCulture()
    {
        var culture = ResourceExtensions.CurrentCulture;
        if (string.IsNullOrEmpty(culture))
        {
            return CultureInfo.CurrentUICulture;
        }

        try
        {
            return CultureInfo.GetCultureInfo(culture);
        }
        catch
        {
            return CultureInfo.CurrentUICulture;
        }
    }
}