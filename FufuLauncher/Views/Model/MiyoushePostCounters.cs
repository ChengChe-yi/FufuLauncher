using FufuLauncher.Helpers;
using FufuLauncher.Models.Miyoushe;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System.Text.RegularExpressions;

namespace FufuLauncher.Views;

public sealed class MiyoushePostCounters : UserControl
{
    public static readonly DependencyProperty CountersProperty = DependencyProperty.Register(nameof(Counters), typeof(string),
        typeof(MiyoushePostCounters), new PropertyMetadata("", OnCountersChanged));
    public static readonly DependencyProperty PostProperty = DependencyProperty.Register(nameof(Post), typeof(CommunityPost),
        typeof(MiyoushePostCounters), new PropertyMetadata(null, OnCountersChanged));
    public static readonly DependencyProperty IsInteractiveProperty = DependencyProperty.Register(nameof(IsInteractive), typeof(bool),
        typeof(MiyoushePostCounters), new PropertyMetadata(false, OnCountersChanged));
    public static readonly DependencyProperty IsInteractionEnabledProperty = DependencyProperty.Register(nameof(IsInteractionEnabled), typeof(bool),
        typeof(MiyoushePostCounters), new PropertyMetadata(true, OnEnabledChanged));
    public CommunityPost? Post { get => (CommunityPost?)GetValue(PostProperty); set => SetValue(PostProperty, value); }
    public bool IsInteractive { get => (bool)GetValue(IsInteractiveProperty); set => SetValue(IsInteractiveProperty, value); }
    public bool IsInteractionEnabled { get => (bool)GetValue(IsInteractionEnabledProperty); set => SetValue(IsInteractionEnabledProperty, value); }
    public event RoutedEventHandler? LikeRequested;
    public event RoutedEventHandler? CommentRequested;

    private readonly StackPanel _panel = new() { Orientation = Orientation.Horizontal, Spacing = 12 };

    public MiyoushePostCounters() => Content = _panel;

    public string Counters
    {
        get => GetCounters(this);
        set => SetCounters(this, value);
    }

    public static string GetCounters(DependencyObject target) => (string)target.GetValue(CountersProperty);
    public static void SetCounters(DependencyObject target, string value) => target.SetValue(CountersProperty, value);

    private static void OnCountersChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var control = (MiyoushePostCounters)sender;
        control.Render(control.Post?.Counters ?? control.Counters);
    }

    private static void OnEnabledChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        foreach (var button in ((MiyoushePostCounters)sender)._panel.Children.OfType<ButtonBase>()) button.IsEnabled = (bool)args.NewValue;
    }

    private void Render(string value)
    {
        _panel.Children.Clear();
        var matches = Regex.Matches(value, "[◉♡☏]");
        for (int i = 0; i < matches.Count; i++)
        {
            var match = matches[i];
            var icon = new MiyousheIcon { Kind = match.Value switch
            {
                "◉" => "Eye",
                "♡" => "Like",
                _ => "Comment"
            } };
            icon.SetBinding(ForegroundProperty, new Binding { Source = this, Path = new PropertyPath(nameof(Foreground)) });
            int end = i + 1 < matches.Count ? matches[i + 1].Index : value.Length;
            var count = new TextBlock { Text = value[(match.Index + match.Length)..end].Trim(), VerticalAlignment = VerticalAlignment.Center };
            count.SetBinding(TextBlock.ForegroundProperty, new Binding { Source = this, Path = new PropertyPath(nameof(Foreground)) });
            count.SetBinding(TextBlock.FontSizeProperty, new Binding { Source = this, Path = new PropertyPath(nameof(FontSize)) });
            var group = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
            group.Children.Add(icon);
            group.Children.Add(count);
            if (IsInteractive && Post != null && match.Value != "◉")
            {
                ButtonBase button;
                if (match.Value == "♡")
                {
                    var like = new ToggleButton { IsChecked = Post.IsLiked };
                    like.Click += (_, args) => { like.IsChecked = Post?.IsLiked == true; LikeRequested?.Invoke(this, args); };
                    button = like;
                }
                else
                {
                    var comment = new Button();
                    comment.Click += (_, args) => CommentRequested?.Invoke(this, args);
                    button = comment;
                }
                button.Content = group; button.MinWidth = button.MinHeight = 0;
                button.Padding = new Thickness(3, 2, 3, 2); button.BorderThickness = new Thickness(0);
                button.CornerRadius = new CornerRadius(6); button.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                button.IsEnabled = IsInteractionEnabled;
                button.SetBinding(ForegroundProperty, new Binding { Source = this, Path = new PropertyPath(nameof(Foreground)) });
                icon.SetBinding(ForegroundProperty, new Binding { Source = button, Path = new PropertyPath(nameof(Foreground)) });
                count.SetBinding(TextBlock.ForegroundProperty, new Binding { Source = button, Path = new PropertyPath(nameof(Foreground)) });
                string label = (match.Value == "♡" ? Post.IsLiked ? "Miyoushe_Liked" : "Miyoushe_Like" : "Miyoushe_PublishComment").GetLocalized();
                ToolTipService.SetToolTip(button, label); AutomationProperties.SetName(button, label + " " + count.Text);
                _panel.Children.Add(button);
            }
            else _panel.Children.Add(group);
        }
    }
}
