/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using ZXing;
using ZXing.Common;
using ZXing.QrCode.Internal;

namespace FufuLauncher.Services;

internal static class ScreenQrDecoder
{
    internal static string[] Decode(Bitmap bitmap)
        => DecodeRegions(bitmap).Select(code => code.Text).Distinct(StringComparer.Ordinal).ToArray();

    internal static ScreenQrDetection[] DecodeRegions(Bitmap bitmap)
    {
        using var converted = new Bitmap(bitmap.Width, bitmap.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(converted)) graphics.DrawImageUnscaled(bitmap, 0, 0);
        var data = converted.LockBits(new Rectangle(0, 0, converted.Width, converted.Height),
            ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        byte[] pixels = new byte[checked(converted.Width * converted.Height * 4)];
        try
        {
            int rowBytes = converted.Width * 4;
            for (int row = 0; row < converted.Height; row++)
                Marshal.Copy(data.Scan0 + row * data.Stride, pixels, row * rowBytes, rowBytes);
        }
        finally { converted.UnlockBits(data); }
        var reader = new BarcodeReaderGeneric
        {
            // QR finder patterns identify orientation themselves. Keep coordinates in the original frame.
            AutoRotate = false,
            Options = new DecodingOptions { PossibleFormats = new[] { BarcodeFormat.QR_CODE }, TryHarder = true }
        };
        var source = new RGBLuminanceSource(pixels, converted.Width, converted.Height, RGBLuminanceSource.BitmapFormat.BGRA32);
        // QRCodeMultiReader does not apply MultiFormatReader's inversion fallback.
        // Check both polarities so a mixed selection cannot silently hide a second code.
        var detections = new List<ScreenQrDetection>();
        foreach (var result in (reader.DecodeMultiple(source) ?? []).Concat(reader.DecodeMultiple(source.invert()) ?? []))
        {
            if (string.IsNullOrWhiteSpace(result.Text)) continue;
            var code = new ScreenQrDetection(result.Text, GetCorners(result, bitmap.Size));
            // Merge polarity duplicates at the same position, but keep two physical copies of the same QR.
            if (!detections.Any(existing => existing.Text == code.Text && existing.IsSamePosition(code)))
                detections.Add(code);
        }
        return detections.ToArray();
    }

    private static PointF[] GetCorners(Result result, Size imageSize)
    {
        if (result.ResultPoints is not { Length: >= 3 } points) return [];
        var bottomLeft = points[0];
        var topLeft = points[1];
        var topRight = points[2];
        float horizontal = ResultPoint.distance(topLeft, topRight);
        float vertical = ResultPoint.distance(topLeft, bottomLeft);
        if (horizontal <= 0 || vertical <= 0) return [];
        float module = points.Take(3).OfType<FinderPattern>().Select(point => point.EstimatedModuleSize)
            .DefaultIfEmpty(Math.Min(horizontal, vertical) / 21).Average();
        // Finder centers sit 3.5 modules inside the QR edge; include a small margin for clicking.
        float padding = module * 3.5f + 2;
        var x = new PointF((topRight.X - topLeft.X) / horizontal * padding,
            (topRight.Y - topLeft.Y) / horizontal * padding);
        var y = new PointF((bottomLeft.X - topLeft.X) / vertical * padding,
            (bottomLeft.Y - topLeft.Y) / vertical * padding);
        return new[]
        {
            new PointF(topLeft.X - x.X - y.X, topLeft.Y - x.Y - y.Y),
            new PointF(topRight.X + x.X - y.X, topRight.Y + x.Y - y.Y),
            new PointF(topRight.X + bottomLeft.X - topLeft.X + x.X + y.X,
                topRight.Y + bottomLeft.Y - topLeft.Y + x.Y + y.Y),
            new PointF(bottomLeft.X - x.X + y.X, bottomLeft.Y - x.Y + y.Y)
        }.Select(point => new PointF(Math.Clamp(point.X, 0, imageSize.Width - 1),
            Math.Clamp(point.Y, 0, imageSize.Height - 1))).ToArray();
    }
}

internal sealed record ScreenQrDetection(string Text, PointF[] Corners)
{
    internal bool Contains(PointF point)
    {
        if (Corners.Length < 3) return false;
        using var path = new System.Drawing.Drawing2D.GraphicsPath();
        path.AddPolygon(Corners);
        return path.IsVisible(point);
    }

    internal bool IsSamePosition(ScreenQrDetection other)
        => Corners.Length == other.Corners.Length && Corners.Zip(other.Corners)
            .All(pair => Math.Abs(pair.First.X - pair.Second.X) < 4 && Math.Abs(pair.First.Y - pair.Second.Y) < 4);
}
