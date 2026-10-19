using FufuLauncher.Helpers;
using FufuLauncher.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.UI;

namespace FufuLauncher.Controls;

public sealed partial class ServiceResponseTimeGraph : UserControl
{
    private static readonly SolidColorBrush DarkLine = new(Color.FromArgb(255, 63, 185, 80));
    private static readonly SolidColorBrush LightLine = new(Color.FromArgb(255, 45, 164, 78));
    private static readonly SolidColorBrush DarkSecondary = new(Color.FromArgb(0xB3, 255, 255, 255));
    private static readonly SolidColorBrush LightSecondary = new(Color.FromArgb(0x99, 0, 0, 0));
    private static readonly SolidColorBrush DarkGrid = new(Color.FromArgb(0x33, 255, 255, 255));
    private static readonly SolidColorBrush LightGrid = new(Color.FromArgb(0x1F, 0, 0, 0));

    private bool _rendering;
    private double _lastWidth = -1;
    private double _lastHeight = -1;

    public ServiceResponseTimeGraph()
    {
        InitializeComponent();

        Loaded += (_, _) => Render();
        DataContextChanged += (_, _) => Render();
        ActualThemeChanged += (_, _) => Render();
        SizeChanged += (_, e) =>
        {
            if (Math.Abs(e.NewSize.Width - _lastWidth) < 0.5 &&
                Math.Abs(e.NewSize.Height - _lastHeight) < 0.5)
            {
                return;
            }

            _lastWidth = e.NewSize.Width;
            _lastHeight = e.NewSize.Height;
            Render();
        };
    }

    private void Render()
    {
        if (_rendering)
        {
            return;
        }

        _rendering = true;

        try
        {
            RootGrid.Children.Clear();

            var isDark = ActualTheme == ElementTheme.Dark;
            var lineBrush = isDark ? DarkLine : LightLine;
            var secondary = isDark ? DarkSecondary : LightSecondary;
            var gridBrush = isDark ? DarkGrid : LightGrid;

            if (DataContext is not ServiceMonitorDetail detail || detail.ResponseTimes.Count == 0)
            {
                var empty = new TextBlock
                {
                    Text = "ServiceDetail_NoResponseTime".GetLocalized(),
                    FontSize = 12,
                    Foreground = secondary,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                RootGrid.Children.Add(empty);
                return;
            }

            var width = ActualWidth;
            var height = ActualHeight;
            if (width <= 0 || height <= 0)
            {
                return;
            }

            const double topPadding = 18;
            const double bottomPadding = 4;
            var chartHeight = Math.Max(1, height - topPadding - bottomPadding);
            var values = detail.ResponseTimes.Select(point => point.Value).ToArray();
            var maxValue = values.Max();
            if (maxValue <= 0)
            {
                maxValue = 1;
            }

            var canvas = new Canvas
            {
                Width = width,
                Height = height,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            RootGrid.Children.Add(canvas);

            var points = new PointCollection();
            var times = detail.ResponseTimes.Select(point => point.Time).ToArray();
            var useTime = times.Length > 1 && times.All(time => time.HasValue);
            var startTime = useTime ? times[0]!.Value : default;
            var timeRange = useTime ? (times[^1]!.Value - startTime).TotalSeconds : 0;
            if (timeRange <= 0)
            {
                useTime = false;
            }

            for (var i = 0; i < values.Length; i++)
            {
                var x = values.Length == 1
                    ? width / 2
                    : useTime
                        ? (times[i]!.Value - startTime).TotalSeconds / timeRange * width
                        : width * i / (values.Length - 1);
                var y = topPadding + (1 - values[i] / maxValue) * chartHeight;
                points.Add(new Point(x, y));
            }

            var areaPoints = new PointCollection
            {
                new(0, height - bottomPadding)
            };
            foreach (var point in points)
            {
                areaPoints.Add(point);
            }

            areaPoints.Add(new Point(width, height - bottomPadding));

            var lineColor = lineBrush.Color;
            var area = new Polygon
            {
                Points = areaPoints,
                Fill = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(0, 1),
                    GradientStops =
                    {
                        new GradientStop
                        {
                            Color = Color.FromArgb(0x38, lineColor.R, lineColor.G, lineColor.B),
                            Offset = 0
                        },
                        new GradientStop
                        {
                            Color = Color.FromArgb(0x00, lineColor.R, lineColor.G, lineColor.B),
                            Offset = 1
                        }
                    }
                },
                IsHitTestVisible = false
            };
            canvas.Children.Add(area);

            var line = new Polyline
            {
                Points = points,
                Stroke = lineBrush,
                StrokeThickness = 2,
                StrokeLineJoin = PenLineJoin.Round,
                IsHitTestVisible = false
            };
            canvas.Children.Add(line);

            if (values.Length == 1)
            {
                var dot = new Ellipse
                {
                    Width = 6,
                    Height = 6,
                    Fill = lineBrush
                };
                Canvas.SetLeft(dot, points[0].X - 3);
                Canvas.SetTop(dot, points[0].Y - 3);
                canvas.Children.Add(dot);
            }

            var topLine = new Line
            {
                X1 = 0,
                Y1 = topPadding,
                X2 = width,
                Y2 = topPadding,
                Stroke = gridBrush,
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 2, 3 },
                IsHitTestVisible = false
            };
            canvas.Children.Add(topLine);

            var maxLabel = new TextBlock
            {
                Text = $"{maxValue:0}ms",
                FontSize = 9,
                Foreground = secondary
            };
            Canvas.SetLeft(maxLabel, 0);
            Canvas.SetTop(maxLabel, 0);
            canvas.Children.Add(maxLabel);
        }
        finally
        {
            _rendering = false;
        }
    }
}