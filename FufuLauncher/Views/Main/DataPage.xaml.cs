/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using FufuLauncher.Constants;
using FufuLauncher.Helpers;
using FufuLauncher.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Web.WebView2.Core;
using Windows.UI;

namespace FufuLauncher.Views;

public sealed partial class DataPage : Page
{
    public DataViewModel ViewModel { get; }

    public DataPage()
    {
        ViewModel = App.GetService<DataViewModel>();
        InitializeComponent();
        Loaded += (_, _) => EntranceStoryboard.Begin();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.InitializeAsync();
    }

    private void OnTabClick(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton button) return;
        
        button.IsChecked = true;

        if (!int.TryParse(button.Tag?.ToString(), out var index)) return;

        var view = (DataCenterView)index;
        ViewModel.SetView(view);
        
        if (view == DataCenterView.Timeline) EnsureTimelineLoaded();
    }

    private async void OnExportPdfClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.ExportPdfAsync(App.MainWindow);
    }

    private async void OnRefreshClick(object sender, RoutedEventArgs e)
    {
        RefreshSpinStoryboard.Begin();
        try
        {
            await ViewModel.RefreshAsync();
        }
        finally
        {
            RefreshSpinStoryboard.Stop();
            RefreshIconRotate.Angle = 0;
        }
    }

    private void OnCharacterSearchTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.ProgrammaticChange) return;
        ViewModel.SearchCharacters(sender.Text);
    }

    private void OnCharacterStarFilterChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox { SelectedItem: DcOption option } &&
            int.TryParse(option.Value, out var star))
        {
            ViewModel.SetCharacterStarFilter(star);
        }
    }

    private void OnCharacterSortChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox { SelectedItem: DcOption option })
        {
            ViewModel.SetCharacterSort(option.Value);
        }
    }

    private void OnShowMoreCharactersClick(object sender, RoutedEventArgs e) => ViewModel.ShowMoreCharacters();

    private async void OnCharacterItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not DcCharacterCard card) return;

        ViewModel.SelectCharacter(card);
        if (ViewModel.SelectedCharacter == null) return;

        var host = new ContentControl
        {
            ContentTemplate = (DataTemplate)Resources["CharacterDetailTemplate"],
            Content = ViewModel.SelectedCharacter,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch
        };

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = ViewModel.SelectedCharacterTitle,
            Content = host,
            CloseButtonText = "DataPage_Close".GetLocalized(),
            DefaultButton = ContentDialogButton.Close
        };
        
        dialog.Resources["ContentDialogMaxWidth"] = 900d;

        await dialog.ShowAsync();
    }

    private void OnAbyssSubViewClick(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton button) return;
        button.IsChecked = true;

        if (button.DataContext is DcAbyssSection section && int.TryParse(button.Tag?.ToString(), out var subView))
        {
            ViewModel.SetAbyssSubView(section, subView);
        }
    }

    private void OnAbyssRankSortChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox { SelectedItem: DcOption option, DataContext: DcAbyssSection section })
        {
            ViewModel.SetAbyssRankSort(section, option.Value);
        }
    }

    private async void OnAbyssVersionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox { SelectedItem: DcOption option, DataContext: DcAbyssSection section }) return;
        
        if (string.Equals(option.Value, section.LoadedVersion, StringComparison.Ordinal)) return;

        section.LoadedVersion = option.Value;
        await ViewModel.ChangeAbyssVersionAsync(section, option.Value);
    }

    private async void OnAbyssTeamFilterChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox { SelectedItem: DcOption option, DataContext: DcAbyssSection section }) return;
        if (string.Equals(option.Value, section.LoadedTeamFilter, StringComparison.Ordinal)) return;

        section.LoadedTeamFilter = option.Value;
        await ViewModel.ChangeTeamFilterAsync(section, option.Value);
    }

    private void OnShowMoreTeamsClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: DcAbyssSection section })
        {
            ViewModel.ShowMoreTeams(section);
        }
    }

    private void OnWishCategoryClick(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton button) return;
        button.IsChecked = true;
        ViewModel.SetWishCategory(button.Tag?.ToString() == "1");
    }

    private void OnWishSearchTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.ProgrammaticChange) return;
        ViewModel.SearchWish(sender.Text);
    }

    private void OnShowMoreWishClick(object sender, RoutedEventArgs e) => ViewModel.ShowMoreWish();

    private async void OnWishImageTapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: DcWishBanner banner }) return;
        if (string.IsNullOrEmpty(banner.Avatar)) return;
        if (!Uri.TryCreate(banner.Avatar, UriKind.Absolute, out var uri)) return;

        var image = new Image
        {
            Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(uri),
            Stretch = Stretch.Uniform
        };

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = banner.Version + "WishBannerImage".GetLocalized(),
            Content = image,
            CloseButtonText = "CloseBtn".GetLocalized(),
            DefaultButton = ContentDialogButton.Close,
            MaxWidth = double.PositiveInfinity
        };

        await dialog.ShowAsync();
    }


    private void OnRerunGroupChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox { SelectedItem: DcOption option } && int.TryParse(option.Value, out var group))
        {
            ViewModel.SetRerunGroup(group);
        }
    }

    private void OnRerunSortChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox { SelectedItem: DcOption option })
        {
            ViewModel.SetRerunSort(option.Value);
        }
    }

    private void OnRerunSearchTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.ProgrammaticChange) return;
        ViewModel.SearchRerun(sender.Text);
    }
    
    private void EnsureTimelineLoaded()
    {
        if (TimelineWebView.Source != null)
        {
            TimelineWebView.Opacity = 1;
            TimelineLoadingTip.Visibility = Visibility.Collapsed;
            return;
        }

        TimelineLoadingTip.Visibility = Visibility.Visible;
        TimelineWebView.Opacity = 0;
        TimelineWebView.DefaultBackgroundColor = Color.FromArgb(255, 28, 28, 34);
        TimelineWebView.NavigationCompleted += TimelineWebView_NavigationCompleted;
        TimelineWebView.WebMessageReceived += TimelineWebView_WebMessageReceived;
        TimelineWebView.Source = new Uri(ApiEndpoints.PaimonTimelineUrl);
    }

    private void TimelineWebView_WebMessageReceived(WebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        if (args.TryGetWebMessageAsString() != "TimelineReady") return;

        TimelineLoadingTip.Visibility = Visibility.Collapsed;
        TimelineWebView.Opacity = 1;
    }

    private async void TimelineWebView_NavigationCompleted(WebView2 sender,
        CoreWebView2NavigationCompletedEventArgs args)
    {
        if (!args.IsSuccess)
        {
            TimelineLoadingTip.Visibility = Visibility.Collapsed;
            return;
        }

        const string script = """
            (function () {
                var maxRetries = 50;
                var attempts = 0;
                var checkExist = setInterval(function () {
                    var target = document.querySelector('div.w-full.overflow-x-auto.px-4.md\\:px-8.svelte-1ga4ett');
                    if (target) {
                        clearInterval(checkExist);
                        document.body.innerHTML = '';
                        document.body.appendChild(target);
                        document.body.style.backgroundColor = '#1c1c22';
                        document.body.style.paddingTop = '20px';
                        target.style.display = 'block';
                        target.style.width = '100%';
                        setTimeout(function () {
                            window.chrome.webview.postMessage('TimelineReady');
                        }, 50);
                    } else {
                        attempts++;
                        if (attempts >= maxRetries) {
                            clearInterval(checkExist);
                            window.chrome.webview.postMessage('TimelineReady');
                        }
                    }
                }, 100);
            })();
            """;

        await sender.ExecuteScriptAsync(script);
    }
}

public class BooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is bool flag && flag ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => value is Visibility visibility && visibility == Visibility.Visible;
}

public class DataCenterBrushConverter : IValueConverter
{
    private static readonly Dictionary<string, Brush> Cache = new(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, uint> Palette = new(StringComparer.OrdinalIgnoreCase)
    {
        ["up"] = 0xFF2FBF6B,
        ["down"] = 0xFFE15C62,
        ["flat"] = 0xFF8D94A0,
        ["muted"] = 0xFF8D94A0,
        
        ["s1"] = 0xFFFF6B6B,
        ["s"] = 0xFFFF9F45,
        ["a"] = 0xFFE0A917,
        ["b"] = 0xFF4DABF7,
        ["f"] = 0xFF8D94A0,

        ["star5"] = 0xFFFFB13B,
        ["star4"] = 0xFFAE84E0,

        ["overdue"] = 0xFFE15C62,
        ["due"] = 0xFFFF9F45,
        ["soon"] = 0xFF4DABF7,
        ["fresh"] = 0xFF8D94A0
    };

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var key = value as string;
        if (string.IsNullOrEmpty(key)) key = "accent";

        if (Cache.TryGetValue(key, out var cached)) return cached;

        Brush brush;
        if (Palette.TryGetValue(key, out var argb))
        {
            brush = new SolidColorBrush(Color.FromArgb(
                (byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb));
        }
        else
        {
            brush = Application.Current.Resources.TryGetValue("AccentTextFillColorPrimaryBrush", out var resource)
                    && resource is Brush accent
                ? accent
                : new SolidColorBrush(Colors.SteelBlue);
        }

        Cache[key] = brush;
        return brush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
