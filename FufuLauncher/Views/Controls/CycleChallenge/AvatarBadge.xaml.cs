/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using FufuLauncher.Data.Repositories;
using FufuLauncher.Helpers;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace FufuLauncher.Views.Controls.CycleChallenge;

public sealed partial class AvatarBadge : UserControl
{
    public static readonly DependencyProperty IconUrlProperty = DependencyProperty.Register(
        nameof(IconUrl),
        typeof(string),
        typeof(AvatarBadge),
        new PropertyMetadata(string.Empty, OnIconUrlChanged));


    public static readonly DependencyProperty LevelProperty = DependencyProperty.Register(
        nameof(Level),
        typeof(int),
        typeof(AvatarBadge),
        new PropertyMetadata(0, OnLevelChanged));


    public static readonly DependencyProperty AvatarIdProperty = DependencyProperty.Register(
        nameof(AvatarId),
        typeof(int),
        typeof(AvatarBadge),
        new PropertyMetadata(0, OnAvatarIdChanged));


    public static readonly DependencyProperty RarityProperty = DependencyProperty.Register(
        nameof(Rarity),
        typeof(int),
        typeof(AvatarBadge),
        new PropertyMetadata(0, OnRarityChanged));


    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
        nameof(Size),
        typeof(double),
        typeof(AvatarBadge),
        new PropertyMetadata(40d, OnSizeChanged));


    public static readonly DependencyProperty ShowRarityRingProperty = DependencyProperty.Register(
        nameof(ShowRarityRing),
        typeof(bool),
        typeof(AvatarBadge),
        new PropertyMetadata(true, OnShowRarityRingChanged));


    private static readonly SolidColorBrush RarityFiveBrush = CreateBrush(0xC6, 0xA0, 0x60);
    private static readonly SolidColorBrush RarityFourBrush = CreateBrush(0x95, 0x76, 0xC1);
    private static readonly SolidColorBrush RarityDefaultBrush = CreateBrush(0x66, 0xA8, 0xD1);


    private static readonly object AvatarNamesLock = new();
    private static Dictionary<int, string>? _avatarNames;

    private string _currentIconUrl = string.Empty;

    public AvatarBadge()
    {
        InitializeComponent();
        AvatarRoot.SizeChanged += OnAvatarRootSizeChanged;
        Loaded += (_, _) => UpdateAvatarClip();

        UpdateIcon();
        UpdateLevel();
        UpdateRarityRing();
        UpdateSize();
        UpdateToolTip();
    }


    public string? IconUrl
    {
        get => (string?)GetValue(IconUrlProperty);
        set => SetValue(IconUrlProperty, value);
    }


    public int Level
    {
        get => (int)GetValue(LevelProperty);
        set => SetValue(LevelProperty, value);
    }


    public int AvatarId
    {
        get => (int)GetValue(AvatarIdProperty);
        set => SetValue(AvatarIdProperty, value);
    }


    public int Rarity
    {
        get => (int)GetValue(RarityProperty);
        set => SetValue(RarityProperty, value);
    }


    public double Size
    {
        get => (double)GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }


    public bool ShowRarityRing
    {
        get => (bool)GetValue(ShowRarityRingProperty);
        set => SetValue(ShowRarityRingProperty, value);
    }

    #region 属性回调

    private static void OnIconUrlChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AvatarBadge badge)
        {
            badge.UpdateIcon();
        }
    }

    private static void OnLevelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AvatarBadge badge)
        {
            badge.UpdateLevel();
            badge.UpdateToolTip();
        }
    }

    private static void OnAvatarIdChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AvatarBadge badge)
        {
            badge.UpdateToolTip();
        }
    }

    private static void OnRarityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AvatarBadge badge)
        {
            badge.UpdateRarityRing();
            badge.UpdateToolTip();
        }
    }

    private static void OnSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AvatarBadge badge)
        {
            badge.UpdateSize();
        }
    }

    private static void OnShowRarityRingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AvatarBadge badge)
        {
            badge.UpdateRarityRing();
        }
    }

    #endregion

    #region 视觉更新

    private void UpdateIcon()
    {
        string url = IconUrl?.Trim() ?? string.Empty;
        bool hasIcon = url.Length > 0;

        AvatarImage.Visibility = hasIcon ? Visibility.Visible : Visibility.Collapsed;
        InitialText.Visibility = hasIcon ? Visibility.Collapsed : Visibility.Visible;

        if (string.Equals(_currentIconUrl, url, StringComparison.Ordinal))
        {
            return;
        }

        _currentIconUrl = url;
        AvatarImage.Source = url;
    }

    private void UpdateLevel()
    {
        int level = Level;
        bool show = level > 0;

        LevelBadge.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        LevelText.Text = show ? level.ToString(CultureInfo.InvariantCulture) : string.Empty;
    }

    private void UpdateRarityRing()
    {
        SolidColorBrush brush = Rarity switch
        {
            5 => RarityFiveBrush,
            4 => RarityFourBrush,
            _ => RarityDefaultBrush,
        };

        RarityRing.Stroke = brush;
        RarityGlow.Stroke = brush;

        // 星级信息缺失（Rarity <= 0）时不显示星级描边，也不影响头像 / 等级 / ToolTip 名称
        bool visible = ShowRarityRing && Rarity > 0;
        RarityRing.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        RarityGlow.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;

        UpdateAvatarClip();
    }

    private void UpdateSize()
    {
        double size = NormalizeSize(Size);

        AvatarRoot.Width = size;
        AvatarRoot.Height = size;
        AvatarImage.Width = size;
        AvatarImage.Height = size;

        InitialText.FontSize = Math.Clamp(size * 0.4, 10, 26);
        LevelText.FontSize = Math.Clamp(size * 0.24, 8, 14);

        double badgeSize = Math.Max(14, size * 0.4);
        LevelBadge.MinWidth = badgeSize;
        LevelBadge.Height = badgeSize;
        LevelBadge.CornerRadius = new CornerRadius(badgeSize / 2);

        UpdateAvatarClip();
    }

    private void UpdateToolTip()
    {
        // 复用已有资源键，避免为控件单独新增 resw 条目
        string levelLabel = "PlayerInfo_Level".GetLocalized();
        string rarityLabel = "GachaAnalysis_Rarity".GetLocalized();
        string? avatarName = ResolveAvatarName(AvatarId);

        var parts = new List<string>(3);
        if (!string.IsNullOrWhiteSpace(avatarName))
        {
            parts.Add(avatarName);
        }

        // 等级不可用（<=0）时整段省略，不影响名称 / 稀有度等其它展示
        if (Level > 0)
        {
            parts.Add($"{levelLabel}{Level}");
        }

        // 星级信息缺失（<=0）时整段省略
        if (Rarity > 0)
        {
            parts.Add($"{rarityLabel} {Rarity}★");
        }

        if (parts.Count == 0)
        {
            // 名称 / 等级 / 星级都不可用时不显示空白 ToolTip
            ToolTipService.SetToolTip(AvatarRoot, null);
            AutomationProperties.SetName(this, string.Empty);
            return;
        }

        string text = string.Join(" · ", parts);
        ToolTipService.SetToolTip(AvatarRoot, text);
        AutomationProperties.SetName(this, text);
    }

    /// <summary>
    ///     按角色 ID 从元数据（<see cref="MetadataRepository" />，ItemId → Name）解析角色名。
    ///     首次调用时一次性建缓存；任何异常都退化为无名称，不影响 ToolTip 其它内容。
    /// </summary>
    private static string? ResolveAvatarName(int avatarId)
    {
        if (avatarId <= 0)
        {
            return null;
        }

        if (_avatarNames is null)
        {
            lock (AvatarNamesLock)
            {
                if (_avatarNames is null)
                {
                    var names = new Dictionary<int, string>();
                    try
                    {
                        MetadataRepository repository = App.GetService<MetadataRepository>();
                        foreach (var item in repository.GetAllMetadata())
                        {
                            if (!string.IsNullOrWhiteSpace(item.ItemId)
                                && int.TryParse(item.ItemId, NumberStyles.Integer, CultureInfo.InvariantCulture,
                                    out int id)
                                && id > 0
                                && !string.IsNullOrWhiteSpace(item.Name))
                            {
                                names.TryAdd(id, item.Name);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[AvatarBadge] 元数据角色名解析失败: {ex.Message}");
                    }

                    // 空结果（元数据表尚未填充 / 查询失败）不缓存，后续 Tooltip 更新时允许重试
                    if (names.Count > 0)
                    {
                        _avatarNames = names;
                    }
                }
            }
        }

        return _avatarNames is not null
               && _avatarNames.TryGetValue(avatarId, out string? resolvedName)
            ? resolvedName
            : null;
    }

    /// <summary>
    ///     把头像图片裁剪成圆形。WinUI 的 <c>UIElement.Clip</c> 只保证矩形裁剪，
    ///     因此这里用合成层的 <see cref="CompositionEllipseGeometry" />（与项目里圆角裁剪的写法一致）。
    /// </summary>
    private void UpdateAvatarClip()
    {
        try
        {
            double size = NormalizeSize(Size);
            bool hasRarityRing = ShowRarityRing && Rarity > 0;
            float radius = (float)(Math.Max(0, size - (hasRarityRing ? 3 : 1)) / 2);
            if (radius <= 0.5f)
            {
                return;
            }

            Visual visual = ElementCompositionPreview.GetElementVisual(AvatarImage);
            Compositor compositor = visual.Compositor;

            CompositionEllipseGeometry geometry = compositor.CreateEllipseGeometry();
            geometry.Center = new Vector2((float)(size / 2), (float)(size / 2));
            geometry.Radius = new Vector2(radius, radius);
            visual.Clip = compositor.CreateGeometricClip(geometry);
        }
        catch (Exception ex)
        {
            // 裁剪失败不能影响主流程，最多退回方形头像
            Debug.WriteLine($"[AvatarBadge] 设置圆形裁剪失败: {ex.Message}");
        }
    }

    private void OnAvatarRootSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateAvatarClip();
    }

    private static double NormalizeSize(double size)
    {
        if (double.IsNaN(size) || double.IsInfinity(size) || size <= 0)
        {
            return 40;
        }

        return size;
    }

    private static SolidColorBrush CreateBrush(byte r, byte g, byte b)
    {
        return new SolidColorBrush(Color.FromArgb(0xFF, r, g, b));
    }

    #endregion
}