/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using Forms = System.Windows.Forms;

namespace FufuLauncher.Services;

internal static class ScreenQrChoiceService
{
    [DllImport("user32.dll")]
    private static extern nint SetThreadDpiAwarenessContext(nint context);

    // Reading remains paused while the user chooses from this frozen frame.
    internal static Task<ScreenQrDetection?> ChooseAsync(Bitmap frame, Rectangle screenBounds,
        IReadOnlyList<ScreenQrDetection> codes, CancellationToken token)
    {
        var completion =
            new TaskCompletionSource<ScreenQrDetection?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            nint oldContext = SetThreadDpiAwarenessContext(new nint(-4));
            try
            {
                token.ThrowIfCancellationRequested();
                ScreenQrDetection? selected;
                using (var overlay = new ChoiceOverlay(frame, screenBounds, codes, token))
                {
                    using var registration = token.Register(overlay.Cancel);
                    overlay.ShowDialog();
                    token.ThrowIfCancellationRequested();
                    selected = overlay.Selected;
                }

                completion.TrySetResult(selected);
            }
            catch (OperationCanceledException)
            {
                completion.TrySetCanceled(token);
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
            finally
            {
                if (oldContext != 0) SetThreadDpiAwarenessContext(oldContext);
            }
        }) { IsBackground = true, Name = "MiyousheQrChoice" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }

    internal static ScreenQrDetection? HitTest(IReadOnlyList<ScreenQrDetection> codes, PointF point)
        => codes.FirstOrDefault(code => code.Contains(point));

    internal static void DrawChoices(Graphics graphics, Bitmap frame,
        IReadOnlyList<ScreenQrDetection> codes, ScreenQrDetection? hovered = null)
    {
        graphics.DrawImageUnscaled(frame, 0, 0);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        foreach (var code in codes)
        {
            if (code.Corners.Length < 3) continue;
            bool active = ReferenceEquals(code, hovered);
            using var fill = new SolidBrush(Color.FromArgb(active ? 110 : 65, 144, 238, 144));
            using var outline = new Pen(Color.FromArgb(0, 180, 80), active ? 3 : 2);
            graphics.FillPolygon(fill, code.Corners);
            graphics.DrawPolygon(outline, code.Corners);
        }
    }

    private sealed class ChoiceOverlay : Forms.Form
    {
        private readonly Bitmap _frame;
        private readonly IReadOnlyList<ScreenQrDetection> _codes;
        private ScreenQrDetection? _hovered;

        internal ScreenQrDetection? Selected
        {
            get;
            private set;
        }

        internal ChoiceOverlay(Bitmap frame, Rectangle bounds, IReadOnlyList<ScreenQrDetection> codes,
            CancellationToken token)
        {
            _frame = frame;
            _codes = codes;
            AutoScaleMode = Forms.AutoScaleMode.None;
            FormBorderStyle = Forms.FormBorderStyle.None;
            StartPosition = Forms.FormStartPosition.Manual;
            Bounds = bounds;
            TopMost = true;
            ShowInTaskbar = false;
            KeyPreview = true;
            DoubleBuffered = true;
            Shown += (_, _) =>
            {
                if (token.IsCancellationRequested) Close();
                else Activate();
            };
        }

        internal void Cancel()
        {
            try
            {
                if (IsHandleCreated && !IsDisposed) BeginInvoke(new Action(Close));
            }
            catch (InvalidOperationException)
            {
            }
        }

        protected override void OnPaint(Forms.PaintEventArgs e) => DrawChoices(e.Graphics, _frame, _codes, _hovered);

        protected override void OnMouseMove(Forms.MouseEventArgs e)
        {
            _hovered = HitTest(_codes, e.Location);
            Cursor = _hovered == null ? Forms.Cursors.Default : Forms.Cursors.Hand;
            Invalidate();
        }

        protected override void OnMouseDown(Forms.MouseEventArgs e)
        {
            if (e.Button == Forms.MouseButtons.Right)
            {
                Close();
                return;
            }

            if (e.Button != Forms.MouseButtons.Left) return;
            Selected = HitTest(_codes, e.Location);
            if (Selected != null) Close();
        }

        protected override void OnKeyDown(Forms.KeyEventArgs e)
        {
            if (e.KeyCode == Forms.Keys.Escape)
            {
                e.Handled = true;
                Close();
            }
            else base.OnKeyDown(e);
        }
    }
}