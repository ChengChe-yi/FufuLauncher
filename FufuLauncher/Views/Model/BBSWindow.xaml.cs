/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using Windows.Graphics;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using FufuLauncher.Services;
using FufuLauncher.Services.Device;
using FufuLauncher.Services.MiHoYo.Fingerprint;

namespace FufuLauncher.Views
{
    public sealed partial class BBSWindow : Window
    {
        #region 构造函数与窗口初始化

        private AppWindow m_AppWindow;

        private readonly DeviceFpService _deviceFpService;
        private string _deviceId = "";
        private string _deviceName = "";
        private string _sysVersion = "";
        private string _deviceUserAgent = "";

        public BBSWindow() : this(true)
        {
        }

        private BBSWindow(bool autoInitialize)
        {
            InitializeComponent();

            _deviceFpService = App.GetService<DeviceFpService>();

            // 设备身份是 App 级：不随活跃账号变化，直接取固定档案。
            var profile = App.GetService<MobileDeviceService>().Device;
            _deviceName = Uri.EscapeDataString(profile.ResolvedDisplayName);
            _sysVersion = profile.OsVersion;
            _deviceUserAgent = string.Format(
                FufuLauncher.Constants.MiHoYo.UserAgents.AndroidBbsTemplate,
                profile.OsVersion,
                profile.Model,
                profile.BuildId,
                CNVersion);

            foreach (var config in _clientConfigs.Values)
            {
                config.UserAgent = _deviceUserAgent;
            }

            _currentConfig = _clientConfigs["2"];

            InitializeWindowStyle();
            UrlTextBox.Text = DefaultUrl;

            if (autoInitialize)
            {
                _ = InitializeWebViewAsync();
            }
        }

        private void InitializeWindowStyle()
        {
            m_AppWindow = AppWindow;
            var displayArea = DisplayArea.GetFromWindowId(m_AppWindow.Id, DisplayAreaFallback.Primary);
            if (displayArea != null)
            {
                var targetHeight = (int)(displayArea.WorkArea.Height * 0.8);
                var targetWidth = (int)(targetHeight * 9.0 / 16.0);

                m_AppWindow.Resize(new SizeInt32(targetWidth, targetHeight));
                m_AppWindow.Move(new PointInt32(
                    (displayArea.WorkArea.Width - targetWidth) / 2 + displayArea.WorkArea.X,
                    (displayArea.WorkArea.Height - targetHeight) / 2 + displayArea.WorkArea.Y
                ));
            }

            if (AppTitleBar != null)
            {
                m_AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;
                m_AppWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
                m_AppWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
                SetTitleBar(AppTitleBar);
            }
        }

        #endregion

        public class AppConfig
        {
            public AccountConfig Account
            {
                get;
                set;
            }
        }

        public class AccountConfig
        {
            public string Cookie
            {
                get;
                set;
            }
        }
    }
}