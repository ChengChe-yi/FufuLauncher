/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace FufuLauncher.Controls;

public sealed class SpeedGraphData
{
    private const ulong InitialMaxSpeed = 1024 * 1024;
    private const double MinSampleGapPercent = 0.2;

    private readonly Path _shapePath;
    private readonly Path _speedLinePath;
    private readonly List<Point> _samples = [];

    private Size _graphSize;
    private ulong _currentMax = InitialMaxSpeed;
    private float _ratio = 1.0f;

    public SpeedGraphData(Path shapePath, Path speedLinePath)
    {
        _shapePath = shapePath;
        _speedLinePath = speedLinePath;
    }

    public sealed class SetSpeedResult
    {
        public float NewScaleRatio { get; set; } = 1.0f;
        public bool NeedAnimation { get; set; }
    }

    public SetSpeedResult SetSpeed(double percent, ulong speed)
    {
        var result = new SetSpeedResult();

        if (_currentMax == 0 && speed == 0)
        {
            return result;
        }

        if (_currentMax < speed)
        {
            result.NewScaleRatio = (float)_currentMax / speed;
            _currentMax = speed;
        }
        
        double x = Math.Clamp(percent, 0, 100);
        float y = 1.0f - (float)speed / _currentMax / _ratio;

        bool isFirstSample = _samples.Count == 0;

        if (isFirstSample)
        {
            _samples.Add(new Point(x, y));
        }
        else
        {
            Point last = _samples[^1];
            if (x - last.X >= MinSampleGapPercent)
            {
                _samples.Add(new Point(x, y));
            }
            else
            {
                _samples[^1] = new Point(last.X, y);
            }
        }
        if (!isFirstSample)
        {
            result.NeedAnimation = true;
        }

        RebuildGeometry();
        return result;
    }

    public void Reset()
    {
        _samples.Clear();
        RebuildGeometry();
    }

    public void SetRatio(float ratio) => _ratio *= ratio;

    public float GetRatio() => _ratio;

    public float Height => (float)_graphSize.Height;

    public Point GetLastPoint() => _samples.Count == 0
        ? new Point(0, 0)
        : ToPixel(_samples[^1]);

    public void NewSize(Size size)
    {
        _graphSize = size;
        RebuildGeometry();
    }
    
    private void RebuildGeometry()
    {
        var areaFigure = new PathFigure { IsClosed = true, IsFilled = true };
        var lineFigure = new PathFigure { IsClosed = false, IsFilled = false };

        if (_samples.Count > 0)
        {
            double width = _graphSize.Width;
            double height = _graphSize.Height;

            Point[] pixels = new Point[_samples.Count];
            for (int i = 0; i < _samples.Count; i++)
            {
                pixels[i] = ToPixel(_samples[i]);
            }

            if (pixels.Length == 1)
            {
                Point p = pixels[0];
                double x0 = Math.Max(0, p.X - 1.5);
                double x1 = p.X + 1.5;

                lineFigure.StartPoint = p;

                areaFigure.StartPoint = new Point(x0, height);
                areaFigure.Segments.Add(new LineSegment { Point = new Point(x0, p.Y) });
                areaFigure.Segments.Add(new LineSegment { Point = new Point(x1, p.Y) });
                areaFigure.Segments.Add(new LineSegment { Point = new Point(x1, height) });
            }
            else
            {
                lineFigure.StartPoint = pixels[0];
                AddSmoothSegments(lineFigure.Segments, pixels, width, height);

                areaFigure.StartPoint = new Point(pixels[0].X, height);
                areaFigure.Segments.Add(new LineSegment { Point = pixels[0] });
                AddSmoothSegments(areaFigure.Segments, pixels, width, height);
                areaFigure.Segments.Add(new LineSegment { Point = new Point(pixels[^1].X, height) });
            }
        }

        var areaGeometry = new PathGeometry();
        areaGeometry.Figures.Add(areaFigure);
        _shapePath.Data = areaGeometry;

        var lineGeometry = new PathGeometry();
        lineGeometry.Figures.Add(lineFigure);
        _speedLinePath.Data = lineGeometry;
    }
    
    private static void AddSmoothSegments(PathSegmentCollection segments, IReadOnlyList<Point> points, double maxX, double maxY)
    {
        if (points.Count == 2)
        {
            segments.Add(new LineSegment { Point = points[1] });
            return;
        }

        for (int i = 0; i < points.Count - 1; i++)
        {
            Point p0 = points[Math.Max(i - 1, 0)];
            Point p1 = points[i];
            Point p2 = points[i + 1];
            Point p3 = points[Math.Min(i + 2, points.Count - 1)];

            Point c1 = new(
                Math.Clamp(p1.X + (p2.X - p0.X) / 6.0, 0, maxX),
                Math.Clamp(p1.Y + (p2.Y - p0.Y) / 6.0, 0, maxY));
            Point c2 = new(
                Math.Clamp(p2.X - (p3.X - p1.X) / 6.0, 0, maxX),
                Math.Clamp(p2.Y - (p3.Y - p1.Y) / 6.0, 0, maxY));

            segments.Add(new BezierSegment { Point1 = c1, Point2 = c2, Point3 = p2 });
        }
    }

    private Point ToPixel(Point sample) => new(sample.X / 100.0 * _graphSize.Width, sample.Y * _graphSize.Height);
}
