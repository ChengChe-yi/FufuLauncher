/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using Windows.Graphics;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using FufuLauncher.Contracts.Services;
using FufuLauncher.Services;
using FufuLauncher.Services.MiHoYo;

namespace FufuLauncher.Views
{
    public sealed partial class BBSWindow : Window
    {
        #region 构造函数与窗口初始化
        private AppWindow m_AppWindow;

        private readonly IDeviceFingerprintService _fingerprintService;
        private static readonly DeviceProfileService _deviceProfileService = new();
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

            _fingerprintService = App.GetService<IDeviceFingerprintService>();
            
            var accountManager = App.GetService<AccountManager>();
            var activeId = accountManager.ActiveAccountId;
            if (!string.IsNullOrEmpty(activeId))
            {
                _deviceId = DeviceProfileService.GetDeviceIdForAccount(activeId);
                var profile = _deviceProfileService.SelectProfile(activeId);
                _deviceName = profile.DeviceName;
                _sysVersion = profile.SysVersion;
                _deviceUserAgent = profile.UserAgent;
            }
            else
            {
                
                _deviceId = Guid.NewGuid().ToString();
                _deviceName = "Xiaomi%2024031PN0DC";
                _sysVersion = "12";
                _deviceUserAgent = $"Mozilla/5.0 (Linux; Android 12; 24031PN0DC Build/V417IR; wv) AppleWebKit/537.36 (KHTML, like Gecko) Version/4.0 Chrome/110.0.5481.154 Safari/537.36 miHoYoBBS/{CNVersion}";
            }

          
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

        public class AppConfig { public AccountConfig Account { get; set; }
 }
        public class AccountConfig { public string Cookie { get; set; } }
    }
}
