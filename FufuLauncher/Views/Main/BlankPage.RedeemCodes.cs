/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Diagnostics;
using System.Text.Json;
using FufuLauncher.Constants;
using FufuLauncher.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using File = System.IO.File;

namespace FufuLauncher.Views;

public sealed partial class BlankPage
{
    #region 兑换码展示

    private async Task LoadRedeemCodesAsync()
    {
        try
        {
            CodesLoadingRing.IsActive = true;
            CodesLoadingRing.Visibility = Visibility.Visible;
            NoCodesText.Visibility = Visibility.Collapsed;
            RedeemCodesList.Visibility = Visibility.Collapsed;

            bool isOs = false;
            if (_currentConfig?.GamePath != null)
            {
                var dir = _currentConfig.GamePath;
                if (File.Exists(dir))
                    dir = Path.GetDirectoryName(dir) ?? dir;
                isOs = dir != null && File.Exists(Path.Combine(dir, "GenshinImpact.exe"));
            }

            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            };

            List<RedeemCodeItem>? codes = null;

            if (isOs)
            {
                var json = await client.GetStringAsync(ApiEndpoints.RedeemCodesOsUrl);
                var response = JsonSerializer.Deserialize<HoyoCodeResponse>(json, options);
                codes = response?.Codes?
                    .Where(c => string.Equals(c.Status, "OK", StringComparison.OrdinalIgnoreCase))
                    .Select(c => new RedeemCodeItem
                    {
                        Title = c.Rewards,
                        Codes = new List<string> { c.Code }
                    })
                    .ToList();
            }
            else
            {
                var json = await client.GetStringAsync(ApiEndpoints.RedeemCodesUrl);
                codes = JsonSerializer.Deserialize<List<RedeemCodeItem>>(json, options);
            }

            if (codes != null && codes.Count > 0)
            {
                RedeemCodesList.ItemsSource = codes;
                RedeemCodesList.Visibility = Visibility.Visible;
            }
            else
            {
                NoCodesText.Text = "Msg_NoNewRedeemCodes".GetLocalized();
                NoCodesText.Visibility = Visibility.Visible;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RedeemCodes] 获取失败: {ex.Message}");
            NoCodesText.Text = "Err_FetchFailedCheckNetwork".GetLocalized();
            NoCodesText.Visibility = Visibility.Visible;
        }
        finally
        {
            CodesLoadingRing.IsActive = false;
            CodesLoadingRing.Visibility = Visibility.Collapsed;
        }
    }

    private void ToggleCodes_Click(object sender, RoutedEventArgs e)
    {
        StopRedeemExpandAnimation();

        if (_redeemCodesExpanded)
        {
            // 平滑收起：从当前实际高度动画到 0
            var fromHeight = RedeemContentPanel.ActualHeight;
            var fromOpacity = RedeemContentPanel.Opacity;
            RedeemContentPanel.Height = fromHeight;

            var sb = new Storyboard();
            sb.Children.Add(CreateRedeemPanelAnimation("Height", fromHeight, 0, 260, EasingMode.EaseIn));
            sb.Children.Add(CreateRedeemPanelAnimation("Opacity", fromOpacity, 0, 200, EasingMode.EaseIn));
            sb.Children.Add(CreateChevronAnimation(RedeemChevronRotate.Angle, 0, 260, EasingMode.EaseIn));
            sb.Completed += (_, _) =>
            {
                RedeemContentPanel.Visibility = Visibility.Collapsed;
                RedeemContentPanel.Height = double.NaN;
                RedeemContentPanel.Opacity = 1;
                _redeemCodesExpanded = false;
                _redeemExpandStoryboard = null;
            };
            _redeemExpandStoryboard = sb;
            sb.Begin();
        }
        else
        {
            // 平滑展开：先测量内容的目标高度，再从 0 动画到该高度
            RedeemContentPanel.Visibility = Visibility.Visible;
            RedeemContentPanel.Height = double.NaN;
            RedeemContentPanel.UpdateLayout();
            var toHeight = RedeemContentPanel.ActualHeight;

            RedeemContentPanel.Height = 0;
            RedeemContentPanel.Opacity = 0;

            var sb = new Storyboard();
            sb.Children.Add(CreateRedeemPanelAnimation("Height", 0, toHeight, 300, EasingMode.EaseOut));
            sb.Children.Add(CreateRedeemPanelAnimation("Opacity", 0, 1, 240, EasingMode.EaseOut));
            sb.Children.Add(CreateChevronAnimation(RedeemChevronRotate.Angle, 180, 300, EasingMode.EaseOut));
            sb.Completed += (_, _) =>
            {
                RedeemContentPanel.Height = double.NaN;
                RedeemContentPanel.Opacity = 1;
                _redeemCodesExpanded = true;
                _redeemExpandStoryboard = null;
            };
            _redeemExpandStoryboard = sb;
            sb.Begin();
        }
    }

    /// <summary>
    /// 若上一次展开/收起动画仍在进行，则以当前动画值作为基准值再停止，避免视觉跳变。
    /// </summary>
    private void StopRedeemExpandAnimation()
    {
        if (_redeemExpandStoryboard == null)
            return;

        RedeemContentPanel.Height = RedeemContentPanel.ActualHeight;
        RedeemContentPanel.Opacity = RedeemContentPanel.Opacity;
        RedeemChevronRotate.Angle = RedeemChevronRotate.Angle;
        _redeemExpandStoryboard.Stop();
        _redeemExpandStoryboard = null;
    }

    private DoubleAnimation CreateRedeemPanelAnimation(string property, double from, double to, int durationMs, EasingMode easing)
    {
        var animation = new DoubleAnimation
        {
            EnableDependentAnimation = true,
            From = from,
            To = to,
            Duration = new Duration(TimeSpan.FromMilliseconds(durationMs)),
            EasingFunction = new CubicEase { EasingMode = easing },
        };
        Storyboard.SetTarget(animation, RedeemContentPanel);
        Storyboard.SetTargetProperty(animation, property);
        return animation;
    }

    private DoubleAnimation CreateChevronAnimation(double from, double to, int durationMs, EasingMode easing)
    {
        var animation = new DoubleAnimation
        {
            From = from,
            To = to,
            Duration = new Duration(TimeSpan.FromMilliseconds(durationMs)),
            EasingFunction = new CubicEase { EasingMode = easing },
        };
        Storyboard.SetTarget(animation, RedeemChevronRotate);
        Storyboard.SetTargetProperty(animation, nameof(RotateTransform.Angle));
        return animation;
    }

    private void CopyCode_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string code)
        {
            var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
            package.SetText(code);
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);

            var originalContent = btn.Content;
            btn.Content = "Btn_Copied".GetLocalized();
            btn.IsEnabled = false;

            Task.Delay(1000).ContinueWith(_ =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    btn.Content = originalContent;
                    btn.IsEnabled = true;
                });
            });
        }
    }

    #endregion
}
