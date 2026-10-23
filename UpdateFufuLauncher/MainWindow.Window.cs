/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Updater;

public partial class MainWindow
{
    #region Window Management

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
    private const int DWMWA_MICA_EFFECT = 1029;

    private const int DWMSBT_MAINWINDOW = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttributeData
    {
        public int Attribute;
        public IntPtr Data;
        public int SizeOfData;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public int AccentState;
        public int AccentFlags;
        public int GradientColor;
        public int AnimationId;
    }

    private void Window_SourceInitialized(object sender, EventArgs e)
    {
        IntPtr windowHandle = new WindowInteropHelper(this).EnsureHandle();

        int useImmersiveDarkMode = 1;
        DwmSetWindowAttribute(windowHandle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useImmersiveDarkMode, Marshal.SizeOf(typeof(int)));

        var osVersion = Environment.OSVersion.Version;
        if (osVersion.Major == 10 && osVersion.Minor == 0 && osVersion.Build >= 22000)
        {
            if (osVersion.Build >= 22621)
            {
                int backdropType = DWMSBT_MAINWINDOW;
                DwmSetWindowAttribute(windowHandle, DWMWA_SYSTEMBACKDROP_TYPE, ref backdropType, Marshal.SizeOf(typeof(int)));
            }
            else
            {
                int micaValue = 1;
                DwmSetWindowAttribute(windowHandle, DWMWA_MICA_EFFECT, ref micaValue, Marshal.SizeOf(typeof(int)));
            }
        }
        else
        {
            AccentPolicy accent = new AccentPolicy
            {
                AccentState = 4, 
                GradientColor = 0x7F202020 
            };

            int accentSize = Marshal.SizeOf(accent);
            IntPtr accentPtr = Marshal.AllocHGlobal(accentSize);
            try
            {
                Marshal.StructureToPtr(accent, accentPtr, false);

                WindowCompositionAttributeData data = new WindowCompositionAttributeData
                {
                    Attribute = 19, 
                    Data = accentPtr,
                    SizeOfData = accentSize
                };

                SetWindowCompositionAttribute(windowHandle, ref data);
            }
            finally
            {
                Marshal.FreeHGlobal(accentPtr);
            }
        }
    }

    private void CloseWindow_Click(object sender, RoutedEventArgs e)
    {
        Environment.Exit(0);
    }

    #endregion
}
