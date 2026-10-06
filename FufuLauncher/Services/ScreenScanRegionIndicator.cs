/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using System.Drawing;
using System.Runtime.InteropServices;
using Forms = System.Windows.Forms;

namespace FufuLauncher.Services;

// A hollow, non-activating window: the captured pixels remain untouched and mouse clicks pass through.
internal sealed class ScreenScanRegionIndicator : IAsyncDisposable
{
    internal const int BorderWidth = 3;
    private readonly TaskCompletionSource _closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private volatile IndicatorForm? _form;

    [DllImport("user32.dll")]
    private static extern nint SetThreadDpiAwarenessContext(nint context);

    internal static async Task<ScreenScanRegionIndicator> ShowAsync(Rectangle captureBounds, CancellationToken token,
        Color? accentColor = null)
    {
        var indicator = new ScreenScanRegionIndicator();
        var ready = new TaskCompletionSource<ScreenScanRegionIndicator>(TaskCreationOptions
            .RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            nint oldContext = SetThreadDpiAwarenessContext(new nint(-4));
            try
            {
                token.ThrowIfCancellationRequested();
                using var form = new IndicatorForm(captureBounds, accentColor ?? Color.DeepSkyBlue);
                indicator._form = form;
                using var registration = token.Register(indicator.Close);
                form.Shown += (_, _) =>
                {
                    if (token.IsCancellationRequested)
                    {
                        ready.TrySetCanceled(token);
                        form.Close();
                    }
                    else ready.TrySetResult(indicator);
                };
                Forms.Application.Run(form);
                ready.TrySetCanceled(token);
            }
            catch (OperationCanceledException)
            {
                ready.TrySetCanceled(token);
            }
            catch (Exception ex)
            {
                ready.TrySetException(ex);
            }
            finally
            {
                indicator._form = null;
                if (oldContext != 0) SetThreadDpiAwarenessContext(oldContext);
                indicator._closed.TrySetResult();
            }
        }) { IsBackground = true, Name = "MiyousheQrScanRegionIndicator" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        try
        {
            return await ready.Task;
        }
        catch
        {
            // A cancelled startup must also finish removing its window before the caller restores the launcher.
            await indicator._closed.Task;
            throw;
        }
    }

    private void Close()
    {
        var form = _form;
        try
        {
            if (form?.IsHandleCreated == true && !form.IsDisposed) form.BeginInvoke(new Action(form.Close));
        }
        catch (InvalidOperationException)
        {
        }
    }

    internal void SetAccentColor(Color color)
    {
        var form = _form;
        try
        {
            if (form?.IsHandleCreated == true && !form.IsDisposed)
                form.BeginInvoke(new Action(() =>
                {
                    if (!form.IsDisposed) form.BackColor = color;
                }));
        }
        catch (InvalidOperationException)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        Close();
        await _closed.Task;
    }

    internal static Rectangle OutlineBounds(Rectangle captureBounds)
    {
        captureBounds.Inflate(BorderWidth, BorderWidth);
        return captureBounds;
    }

    internal static System.Drawing.Region OutlineRegion(Size size)
    {
        var region = new System.Drawing.Region(new Rectangle(Point.Empty, size));
        region.Exclude(new Rectangle(BorderWidth, BorderWidth,
            size.Width - BorderWidth * 2, size.Height - BorderWidth * 2));
        return region;
    }

    private sealed class IndicatorForm : Forms.Form
    {
        internal IndicatorForm(Rectangle captureBounds, Color accentColor)
        {
            AutoScaleMode = Forms.AutoScaleMode.None;
            FormBorderStyle = Forms.FormBorderStyle.None;
            StartPosition = Forms.FormStartPosition.Manual;
            Bounds = OutlineBounds(captureBounds);
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = accentColor;
            Opacity = 0.95; // WinForms enables WS_EX_LAYERED and sets the alpha value.
            Region = OutlineRegion(Bounds.Size);
        }

        protected override bool ShowWithoutActivation => true;

        protected override Forms.CreateParams CreateParams
        {
            get
            {
                var parameters = base.CreateParams;
                // WS_EX_TRANSPARENT + WS_EX_LAYERED pass input to underlying applications.
                // See Microsoft Learn, Window Features / Layered Windows.
                parameters.ExStyle |= 0x20 | 0x80 | 0x08000000;
                return parameters;
            }
        }
    }
}