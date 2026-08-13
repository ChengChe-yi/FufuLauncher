/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.Foundation;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace FufuLauncher.Controls;


[TemplatePart(Name = PartCanvas, Type = typeof(CanvasControl))]
[TemplatePart(Name = PartNoDataText, Type = typeof(TextBlock))]
[TemplatePart(Name = PartGraphGrid, Type = typeof(Grid))]
[TemplatePart(Name = PartHostGrid, Type = typeof(Grid))]
[TemplatePart(Name = PartGraphScale, Type = typeof(ScaleTransform))]
[TemplatePart(Name = PartShape, Type = typeof(Path))]
[TemplatePart(Name = PartSpeedLine, Type = typeof(Path))]
public sealed class SpeedGraph : Control
{
    private const string PartCanvas = "PART_Canvas";
    private const string PartNoDataText = "PART_SpeedGraphNoDataAvailableText";
    private const string PartGraphGrid = "PART_GraphGrid";
    private const string PartHostGrid = "PART_HostGrid";
    private const string PartGraphScale = "PART_GraphScale";
    private const string PartShape = "PART_Shape";
    private const string PartSpeedLine = "PART_SpeedLine";

    private static readonly TimeSpan LineAndTextAnimationDuration = TimeSpan.FromMilliseconds(300);

    private CanvasControl? _canvas;
    private TextBlock? _noDataTextBlock;
    private Grid? _graphGrid;
    private Grid? _hostGrid;
    private ScaleTransform? _graphScale;
    private Path? _shapePath;
    private Path? _speedLinePath;

    private SpeedGraphData? _graphData;
    private bool _hasData;

    public SpeedGraph()
    {
        DefaultStyleKey = typeof(SpeedGraph);
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        _canvas = GetTemplateChild(PartCanvas) as CanvasControl;
        _noDataTextBlock = GetTemplateChild(PartNoDataText) as TextBlock;
        _graphGrid = GetTemplateChild(PartGraphGrid) as Grid;
        _hostGrid = GetTemplateChild(PartHostGrid) as Grid;
        _graphScale = GetTemplateChild(PartGraphScale) as ScaleTransform;
        _shapePath = GetTemplateChild(PartShape) as Path;
        _speedLinePath = GetTemplateChild(PartSpeedLine) as Path;

        if (_shapePath is null || _speedLinePath is null)
        {
            return;
        }

        _graphData = new SpeedGraphData(_shapePath, _speedLinePath);

        if (_canvas is not null)
        {
            _canvas.Draw -= OnCanvasDraw;
            _canvas.Draw += OnCanvasDraw;
            _canvas.ActualThemeChanged -= OnCanvasActualThemeChanged;
            _canvas.ActualThemeChanged += OnCanvasActualThemeChanged;
        }

        SizeChanged -= OnSizeChanged;
        SizeChanged += OnSizeChanged;

        UpdateBackgroundShape();
    }
    
    public void SetSpeed(double percent, ulong speed)
    {
        if (_graphData is null || _shapePath is null)
        {
            return;
        }

        var result = _graphData.SetSpeed(percent, speed);

        if (result.NewScaleRatio != 1.0f)
        {
            ResizeGraphPoint(result.NewScaleRatio);
        }

        if (result.NeedAnimation)
        {
            if (AutoUpdateSpeedText)
            {
                SpeedText = GetSpeedReadable(speed);
            }

            MakeAnimation();
        }

        if (!_hasData && _graphGrid is not null && _noDataTextBlock is not null)
        {
            _graphGrid.Visibility = Visibility.Visible;
            _noDataTextBlock.Visibility = Visibility.Collapsed;
            _graphData.NewSize(new Size(ActualSize.X, ActualSize.Y));
            _hasData = true;
        }
    }
    
    public void ResetGraph()
    {
        _graphData?.Reset();
        SetSpeed(0, 0);
    }
    
    public void NormalGraph() => VisualStateManager.GoToState(this, "Normal", false);
    
    public void PauseGraph() => VisualStateManager.GoToState(this, "Pause", false);
    
    public void ErrorGraph() => VisualStateManager.GoToState(this, "Error", false);

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_graphData is null || _shapePath is null)
        {
            return;
        }

        _graphData.NewSize(e.NewSize);
    }

    private void OnCanvasActualThemeChanged(FrameworkElement sender, object args)
    {
        ((CanvasControl)sender).Invalidate();
    }

    private void OnCanvasDraw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        var drawingSession = args.DrawingSession;

        float width = (float)sender.ActualWidth;
        float height = (float)sender.ActualHeight;

        Windows.UI.Color color = ActualTheme == ElementTheme.Light
            ? Microsoft.UI.ColorHelper.FromArgb(32, 0, 0, 0)
            : Microsoft.UI.ColorHelper.FromArgb(32, 255, 255, 255);

        switch (BackgroundMode)
        {
            case SpeedGraphBackgroundMode.Dot:
                for (int x = 0; x < width; x += BackgroundShapeDistance)
                {
                    for (int y = 0; y < height; y += BackgroundShapeDistance)
                    {
                        drawingSession.FillCircle(x, y, 1, color);
                    }
                }

                break;

            case SpeedGraphBackgroundMode.Cross:
                for (int x = 0; x < width; x += BackgroundShapeDistance)
                {
                    drawingSession.DrawLine(x, 0, x, height, color, 1);
                }

                for (int y = 0; y < height; y += BackgroundShapeDistance)
                {
                    drawingSession.DrawLine(0, y, width, y, color, 1);
                }

                break;
        }
    }
    
    private void ResizeGraphPoint(float ratio)
    {
        if (_graphData is null)
        {
            return;
        }

        _graphData.SetRatio(ratio);
        CreateScaleAnimation().Begin();
    }

    private Storyboard CreateScaleAnimation()
    {
        var storyboard = new Storyboard();

        var easing = new ExponentialEase
        {
            EasingMode = EasingMode.EaseInOut,
            Exponent = 6,
        };

        var scaleYAnimation = new DoubleAnimation
        {
            To = _graphData!.GetRatio(),
            Duration = new Duration(TimeSpan.FromMilliseconds(300)),
            EasingFunction = easing,
        };

        Storyboard.SetTarget(scaleYAnimation, _graphScale);
        Storyboard.SetTargetProperty(scaleYAnimation, "ScaleY");

        storyboard.Children.Add(scaleYAnimation);
        return storyboard;
    }
    
    private void MakeAnimation()
    {
        if (_graphData is null || _hostGrid is null)
        {
            return;
        }

        Point lastPoint = _graphData.GetLastPoint();
        double y = _graphData.Height - ((_graphData.Height - lastPoint.Y) * _graphData.GetRatio());

        var compositor = CompositionTarget.GetCompositorForCurrentThread();
        var animation = compositor.CreateScalarKeyFrameAnimation();
        animation.InsertKeyFrame(1.0f, (float)y, compositor.CreateCubicBezierEasingFunction(
            new System.Numerics.Vector2(0.0f, 0.0f), new System.Numerics.Vector2(0.2f, 1.0f)));
        animation.Duration = LineAndTextAnimationDuration;

        var gridVisual = ElementCompositionPreview.GetElementVisual(_hostGrid);
        gridVisual.StartAnimation("Offset.Y", animation);
    }

    private void UpdateBackgroundShape()
    {
        _canvas?.Invalidate();
    }

    private static string GetSpeedReadable(ulong bytesPerSecond)
    {
        string[] units = { "B/s", "KB/s", "MB/s", "GB/s", "TB/s" };

        double size = bytesPerSecond;
        int unitIndex = 0;
        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return $"{size:0.##} {units[unitIndex]}";
    }

    #region 依赖

    public SpeedGraphBackgroundMode BackgroundMode
    {
        get => (SpeedGraphBackgroundMode)GetValue(BackgroundModeProperty);
        set => SetValue(BackgroundModeProperty, value);
    }

    public static readonly DependencyProperty BackgroundModeProperty = DependencyProperty.Register(
        nameof(BackgroundMode), typeof(SpeedGraphBackgroundMode), typeof(SpeedGraph),
        new PropertyMetadata(SpeedGraphBackgroundMode.Dot, OnBackgroundModeChanged));

    private static void OnBackgroundModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((SpeedGraph)d).UpdateBackgroundShape();
    }

    public int BackgroundShapeDistance
    {
        get => (int)GetValue(BackgroundShapeDistanceProperty);
        set => SetValue(BackgroundShapeDistanceProperty, value);
    }

    public static readonly DependencyProperty BackgroundShapeDistanceProperty = DependencyProperty.Register(
        nameof(BackgroundShapeDistance), typeof(int), typeof(SpeedGraph),
        new PropertyMetadata(6, OnBackgroundShapeDistanceChanged));

    private static void OnBackgroundShapeDistanceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((SpeedGraph)d)._canvas?.Invalidate();
    }

    public Visibility SpeedLineVisibility
    {
        get => (Visibility)GetValue(SpeedLineVisibilityProperty);
        set => SetValue(SpeedLineVisibilityProperty, value);
    }

    public static readonly DependencyProperty SpeedLineVisibilityProperty = DependencyProperty.Register(
        nameof(SpeedLineVisibility), typeof(Visibility), typeof(SpeedGraph),
        new PropertyMetadata(Visibility.Visible));

    public Visibility SpeedTextVisibility
    {
        get => (Visibility)GetValue(SpeedTextVisibilityProperty);
        set => SetValue(SpeedTextVisibilityProperty, value);
    }

    public static readonly DependencyProperty SpeedTextVisibilityProperty = DependencyProperty.Register(
        nameof(SpeedTextVisibility), typeof(Visibility), typeof(SpeedGraph),
        new PropertyMetadata(Visibility.Visible));

    public string SpeedText
    {
        get => (string)GetValue(SpeedTextProperty);
        set => SetValue(SpeedTextProperty, value);
    }

    public static readonly DependencyProperty SpeedTextProperty = DependencyProperty.Register(
        nameof(SpeedText), typeof(string), typeof(SpeedGraph),
        new PropertyMetadata("--- MB/s"));

    public string NoDataText
    {
        get => (string)GetValue(NoDataTextProperty);
        set => SetValue(NoDataTextProperty, value);
    }

    public static readonly DependencyProperty NoDataTextProperty = DependencyProperty.Register(
        nameof(NoDataText), typeof(string), typeof(SpeedGraph),
        new PropertyMetadata("No speed data available!"));

    public bool AutoUpdateSpeedText
    {
        get => (bool)GetValue(AutoUpdateSpeedTextProperty);
        set => SetValue(AutoUpdateSpeedTextProperty, value);
    }

    public static readonly DependencyProperty AutoUpdateSpeedTextProperty = DependencyProperty.Register(
        nameof(AutoUpdateSpeedText), typeof(bool), typeof(SpeedGraph),
        new PropertyMetadata(true));

    #endregion
}
