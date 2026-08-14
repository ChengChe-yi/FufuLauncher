/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Diagnostics;
using System.Runtime.InteropServices.WindowsRuntime;
using FufuLauncher.Services.GameAnnouncement;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;

namespace FufuLauncher.Views
{
    public sealed partial class GameAnnouncementBanner : UserControl
    {
        public static readonly DependencyProperty BannerUrlProperty = DependencyProperty.Register(
            nameof(BannerUrl),
            typeof(string),
            typeof(GameAnnouncementBanner),
            new PropertyMetadata(null, OnBannerUrlChanged));

        private readonly Brush _placeholderBrush;

        private int _loadVersion;
        private CancellationTokenSource? _loadCancellation;

        public string? BannerUrl
        {
            get => (string?)GetValue(BannerUrlProperty);
            set => SetValue(BannerUrlProperty, value);
        }

        public GameAnnouncementBanner()
        {
            InitializeComponent();

            // 捕获主题解析后的占位画刷，加载失败时恢复
            _placeholderBrush = RootGrid.Background;

            Unloaded += GameAnnouncementBanner_Unloaded;
        }
        
        public void AnimateZoom(double target)
        {
            if (RootGrid.Background is not ImageBrush brush || brush.RelativeTransform is not ScaleTransform transform)
            {
                return;
            }

            Storyboard storyboard = new();
            DoubleAnimation scaleX = CreateScaleAnimation(target);
            Storyboard.SetTarget(scaleX, transform);
            Storyboard.SetTargetProperty(scaleX, "ScaleX");
            storyboard.Children.Add(scaleX);

            DoubleAnimation scaleY = CreateScaleAnimation(target);
            Storyboard.SetTarget(scaleY, transform);
            Storyboard.SetTargetProperty(scaleY, "ScaleY");
            storyboard.Children.Add(scaleY);

            storyboard.Begin();
        }

        private void GameAnnouncementBanner_Unloaded(object sender, RoutedEventArgs e)
        {
            _loadCancellation?.Cancel();
        }

        private static void OnBannerUrlChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((GameAnnouncementBanner)d).LoadBannerAsync((string?)e.NewValue);
        }

        private async void LoadBannerAsync(string? url)
        {
            int version = ++_loadVersion;
            _loadCancellation?.Cancel();
            CancellationTokenSource cts = new();
            _loadCancellation = cts;
            CancellationToken token = cts.Token;

            if (string.IsNullOrWhiteSpace(url))
            {
                ShowPlaceholder();
                return;
            }

            try
            {
                IGameAnnouncementImageService imageService = App.GetService<IGameAnnouncementImageService>();
                byte[]? bytes = await imageService.GetImageBytesAsync(url, token);

                if (version != _loadVersion || token.IsCancellationRequested || bytes is null)
                {
                    if (version == _loadVersion)
                    {
                        ShowPlaceholder();
                    }

                    return;
                }

                using var stream = new MemoryStream(bytes).AsRandomAccessStream();
                BitmapImage bitmap = new();
                await bitmap.SetSourceAsync(stream);

                if (version != _loadVersion)
                {
                    return;
                }

                RootGrid.Background = new ImageBrush
                {
                    ImageSource = bitmap,
                    Stretch = Stretch.UniformToFill,
                    RelativeTransform = new ScaleTransform { CenterX = 0.5, CenterY = 0.5 }
                };
                PlaceholderIcon.Visibility = Visibility.Collapsed;
            }
            catch (OperationCanceledException)
            {
                // 卡片被回收或地址变化导致的取消，静默处理
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GameAnnouncementBanner] 图片加载失败: {ex.Message}");
                if (version == _loadVersion)
                {
                    ShowPlaceholder();
                }
            }
        }

        private void ShowPlaceholder()
        {
            RootGrid.Background = _placeholderBrush;
            PlaceholderIcon.Visibility = Visibility.Visible;
        }

        private static DoubleAnimation CreateScaleAnimation(double target)
        {
            return new DoubleAnimation
            {
                To = target,
                Duration = new Duration(TimeSpan.FromMilliseconds(180)),
                EnableDependentAnimation = true,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
        }
    }
}
