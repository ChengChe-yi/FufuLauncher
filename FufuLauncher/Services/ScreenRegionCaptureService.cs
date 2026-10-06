/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Forms = System.Windows.Forms;

namespace FufuLauncher.Services;

internal static class ScreenRegionCaptureService
{
    [DllImport("user32.dll")]
    private static extern nint SetThreadDpiAwarenessContext(nint context);

    internal static Task<ScreenRegionSelection?> CaptureAsync(string instruction, CancellationToken cancellationToken,
        Color? accentColor = null)
    {
        var completion = new TaskCompletionSource<ScreenRegionSelection?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            nint oldContext = SetThreadDpiAwarenessContext(new nint(-4)); // physical pixels, PerMonitorV2
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var bounds = Forms.SystemInformation.VirtualScreen;
                using var desktop = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
                using (var graphics = Graphics.FromImage(desktop))
                    graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size, CopyPixelOperation.SourceCopy);
                using var overlay = new SelectionOverlay(desktop, bounds, instruction, cancellationToken,
                    accentColor ?? Color.DeepSkyBlue);
                using var registration = cancellationToken.Register(overlay.Cancel);
                overlay.ShowDialog();
                ScreenRegionSelection? crop = overlay.TakeSelection();
                if (cancellationToken.IsCancellationRequested)
                {
                    crop?.Dispose();
                    completion.TrySetCanceled(cancellationToken);
                }
                else if (!completion.TrySetResult(crop)) crop?.Dispose();
            }
            catch (OperationCanceledException) { completion.TrySetCanceled(cancellationToken); }
            catch (Exception ex) { completion.TrySetException(ex); }
            finally { if (oldContext != 0) SetThreadDpiAwarenessContext(oldContext); }
        }) { IsBackground = true, Name = "MiyousheQrScreenSelection" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }

    private sealed class SelectionOverlay : Forms.Form
    {
        private readonly Bitmap _desktop;
        private readonly string _instruction;
        private readonly CancellationToken _cancellationToken;
        private Point? _start;
        private Rectangle _selection;
        private ScreenRegionSelection? _crop;
        private readonly Rectangle _desktopBounds;
        private readonly Color _accentColor;

        internal SelectionOverlay(Bitmap desktop, Rectangle bounds, string instruction, CancellationToken cancellationToken,
            Color accentColor)
        {
            _desktop = desktop;
            _desktopBounds = bounds;
            _instruction = instruction;
            _cancellationToken = cancellationToken;
            _accentColor = accentColor;
            AutoScaleMode = Forms.AutoScaleMode.None;
            FormBorderStyle = Forms.FormBorderStyle.None;
            StartPosition = Forms.FormStartPosition.Manual;
            Bounds = bounds;
            TopMost = true;
            ShowInTaskbar = false;
            Cursor = Forms.Cursors.Cross;
            KeyPreview = true;
            DoubleBuffered = true;
            Shown += (_, _) => { if (_cancellationToken.IsCancellationRequested) Close(); else Activate(); };
        }

        internal ScreenRegionSelection? TakeSelection() { var crop = _crop; _crop = null; return crop; }

        internal void Cancel()
        {
            try { if (IsHandleCreated && !IsDisposed) BeginInvoke(new Action(Close)); }
            catch (InvalidOperationException) { }
        }

        protected override void OnPaint(Forms.PaintEventArgs e)
        {
            e.Graphics.DrawImageUnscaled(_desktop, 0, 0);
            using var shade = new SolidBrush(Color.FromArgb(140, 0, 0, 0));
            using var region = new System.Drawing.Region(ClientRectangle);
            if (!_selection.IsEmpty) region.Exclude(_selection);
            e.Graphics.FillRegion(shade, region);
            if (!_selection.IsEmpty)
            {
                using var pen = new Pen(_accentColor, 2);
                e.Graphics.DrawRectangle(pen, _selection);
            }
            // Keep instructions visible on the monitor containing the cursor, including negative origins.
            Rectangle monitor = Forms.Screen.FromPoint(Forms.Cursor.Position).Bounds;
            Point monitorOrigin = PointToClient(monitor.Location);
            using var font = new Font("Microsoft YaHei UI", 12);
            e.Graphics.DrawString(_instruction, font, Brushes.White, monitorOrigin.X + 24, monitorOrigin.Y + 24);
        }

        protected override void OnMouseDown(Forms.MouseEventArgs e)
        {
            if (e.Button == Forms.MouseButtons.Right) { Close(); return; }
            if (e.Button != Forms.MouseButtons.Left) return;
            _start = e.Location;
            Capture = true;
        }

        protected override void OnMouseMove(Forms.MouseEventArgs e)
        {
            if (_start.HasValue)
                _selection = SelectionRectangle(_start.Value, e.Location, ClientRectangle);
            Invalidate();
        }

        protected override void OnMouseUp(Forms.MouseEventArgs e)
        {
            if (e.Button != Forms.MouseButtons.Left || !_start.HasValue) return;
            _selection = SelectionRectangle(_start.Value, e.Location, ClientRectangle);
            _start = null;
            Capture = false;
            if (_selection.Width < 16 || _selection.Height < 16) { _selection = Rectangle.Empty; Invalidate(); return; }
            _crop = new ScreenRegionSelection(_desktop.Clone(_selection, PixelFormat.Format32bppArgb),
                new Rectangle(_desktopBounds.X + _selection.X, _desktopBounds.Y + _selection.Y,
                    _selection.Width, _selection.Height));
            Close();
        }

        protected override void OnKeyDown(Forms.KeyEventArgs e)
        {
            if (e.KeyCode == Forms.Keys.Escape) { e.Handled = true; Close(); }
            else base.OnKeyDown(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _crop?.Dispose();
            base.Dispose(disposing);
        }
    }

    internal static Rectangle SelectionRectangle(Point start, Point end, Rectangle bounds)
        => Rectangle.Intersect(Rectangle.FromLTRB(Math.Min(start.X, end.X), Math.Min(start.Y, end.Y),
            Math.Max(start.X, end.X), Math.Max(start.Y, end.Y)), bounds);

    internal static Bitmap CaptureRegion(Rectangle bounds)
    {
        nint oldContext = SetThreadDpiAwarenessContext(new nint(-4));
        Bitmap? frame = null;
        try
        {
            if (bounds.Width < 16 || bounds.Height < 16 || !Forms.SystemInformation.VirtualScreen.Contains(bounds))
                throw new MiyousheQrException("MiyousheQr_RegionUnavailable");
            frame = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
            using var graphics = Graphics.FromImage(frame);
            graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size, CopyPixelOperation.SourceCopy);
            return frame;
        }
        catch { frame?.Dispose(); throw; }
        finally { if (oldContext != 0) SetThreadDpiAwarenessContext(oldContext); }
    }
}

internal sealed class ScreenRegionSelection(Bitmap image, Rectangle bounds) : IDisposable
{
    internal Bitmap Image { get; } = image;
    internal Rectangle Bounds { get; } = bounds;
    public void Dispose() => Image.Dispose();
}
