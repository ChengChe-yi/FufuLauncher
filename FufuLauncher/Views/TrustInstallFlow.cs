/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using FufuLauncher.Helpers;
using FufuLauncher.Services.CodeSigning;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FufuLauncher.Views;

internal static class TrustInstallFlow
{
    internal static async Task<TrustOperationResult?> InstallAsync(
        CodeSigningTrustService service, XamlRoot xamlRoot, TrustStoreScope scope)
    {
        var package = await Task.Run(() => service.GetPackage(true));
        if (package == null)
        {
            await ShowAsync(xamlRoot, "安装失败", "平台证书不可用，请稍后在设置中更新信任信息。");
            return null;
        }

        var scopeText = scope == TrustStoreScope.CurrentUser ? "当前用户" : "本机";
        var confirmed = await ShowAsync(xamlRoot, "核对平台证书", BuildReviewContent(package, scopeText),
            "核对无误，安装", "取消");

        if (confirmed != ContentDialogResult.Primary) return null;

        var result = await Task.Run(() => service.InstallRoot(scope));

        if (!result.Ok && result.NeedsElevation)
        {
            result = await RunElevatedAsync(xamlRoot, "install", scope == TrustStoreScope.CurrentUser ? "user" : "machine");
        }

        return result;
    }

    internal static async Task<TrustOperationResult?> UninstallAsync(
        CodeSigningTrustService service, XamlRoot xamlRoot, TrustStoreScope scope)
    {
        var scopeText = scope == TrustStoreScope.CurrentUser ? "当前用户" : "本机";
        var confirmed = await ShowAsync(xamlRoot, "卸载平台证书",
            $"即将为「{scopeText}」卸载平台证书，卸载后将不再信任我们签发的代码签名。是否继续？",
            "卸载", "取消");

        if (confirmed != ContentDialogResult.Primary) return null;

        var result = await Task.Run(() => service.UninstallRoot(scope));

        if (!result.Ok && result.NeedsElevation)
        {
            result = await RunElevatedAsync(xamlRoot, "uninstall", scope == TrustStoreScope.CurrentUser ? "user" : "machine");
        }

        return result;
    }

    private static FrameworkElement BuildReviewContent(VerifiedTrustPackage package, string scopeText)
    {
        var panel = new StackPanel { Spacing = 10 };

        panel.Children.Add(new TextBlock
        {
            Text = $"即将为「{scopeText}」安装以下平台证书，请核对无误后继续：",
            TextWrapping = TextWrapping.Wrap
        });

        panel.Children.Add(BuildCertificateBlock("平台根证书", package.Root));
        panel.Children.Add(BuildCertificateBlock("平台中间证书", package.Issuer));

        panel.Children.Add(new TextBlock
        {
            Text = "证书用途：验证我们签发的代码签名。安装后可在设置中随时卸载。",
            FontSize = 12,
            Opacity = 0.75,
            TextWrapping = TextWrapping.Wrap
        });

        return new ScrollViewer
        {
            Content = panel,
            MaxHeight = 440,
            MinWidth = 460,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
    }

    private static FrameworkElement BuildCertificateBlock(string title, X509Certificate2 certificate)
    {
        var stack = new StackPanel { Spacing = 4 };

        stack.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold });
        stack.Children.Add(BuildCertificateField("颁发给", certificate.Subject));
        stack.Children.Add(BuildCertificateField("颁发者", certificate.Issuer));
        stack.Children.Add(BuildCertificateField("SHA-256 指纹", ManifestSignatureVerifier.Sha256Thumbprint(certificate)));
        stack.Children.Add(BuildCertificateField("有效期至", certificate.NotAfter.ToString("yyyy-MM-dd")));

        return new Border
        {
            Padding = new Thickness(12),
            CornerRadius = new CornerRadius(6),
            Background = TryGetCardBrush(),
            Child = stack
        };
    }

    private static FrameworkElement BuildCertificateField(string label, string value)
    {
        var grid = new Grid { ColumnSpacing = 8 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(88) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var labelBlock = new TextBlock
        {
            Text = label,
            FontSize = 12,
            Opacity = 0.65,
            VerticalAlignment = VerticalAlignment.Top
        };

        var valueBlock = new TextBlock
        {
            Text = value,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            IsTextSelectionEnabled = true
        };

        Grid.SetColumn(valueBlock, 1);
        grid.Children.Add(labelBlock);
        grid.Children.Add(valueBlock);

        return grid;
    }

    private static Microsoft.UI.Xaml.Media.Brush? TryGetCardBrush()
    {
        try
        {
            return Application.Current.Resources.TryGetValue("CardBackgroundFillColorDefaultBrush", out var brush)
                ? brush as Microsoft.UI.Xaml.Media.Brush
                : null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TrustInstallFlow] 读取主题画刷失败: {ex.Message}");
            return null;
        }
    }

    private static async Task<ContentDialogResult> ShowAsync(
        XamlRoot xamlRoot, string title, object content, string primaryText = "确定", string closeText = "关闭")
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = content,
            PrimaryButtonText = primaryText,
            CloseButtonText = closeText,
            DefaultButton = primaryText == "确定" ? ContentDialogButton.Close : ContentDialogButton.Primary,
            XamlRoot = xamlRoot,
            Resources = { ["ContentDialogMaxWidth"] = 640.0 }
        };

        try
        {
            return await dialog.ShowAsync();
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            await Task.Delay(300);

            try
            {
                return await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TrustInstallFlow] 对话框显示失败: {ex.Message}");
                return ContentDialogResult.None;
            }
        }
    }

    private static async Task<TrustOperationResult> RunElevatedAsync(XamlRoot xamlRoot, string action, string scope)
    {
        var resultFile = Path.Combine(Path.GetTempPath(), $"fufu-trust-{Guid.NewGuid():N}.json");

        try
        {
            var executable = Environment.ProcessPath;
            if (string.IsNullOrEmpty(executable))
            {
                return new TrustOperationResult { Ok = false, Message = "无法定位当前程序路径" };
            }

            var process = Process.Start(new ProcessStartInfo(executable)
            {
                Arguments = $"--trust-cert {action} {scope} --result-file \"{resultFile}\"",
                Verb = "runas",
                UseShellExecute = true
            });

            if (process == null)
            {
                return new TrustOperationResult { Ok = false, Message = "提权进程启动失败" };
            }

            await process.WaitForExitAsync();

            if (File.Exists(resultFile))
            {
                using var document = JsonDocument.Parse(await File.ReadAllTextAsync(resultFile));
                var root = document.RootElement;

                return new TrustOperationResult
                {
                    Ok = root.TryGetProperty("ok", out var ok) && ok.GetBoolean(),
                    Message = root.TryGetProperty("message", out var message)
                        ? message.GetString() ?? string.Empty
                        : string.Empty
                };
            }

            return new TrustOperationResult
            {
                Ok = process.ExitCode == 0,
                Message = process.ExitCode == 0 ? "操作已完成" : $"提权操作退出码 {process.ExitCode}"
            };
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return new TrustOperationResult { Ok = false, Message = "已取消管理员授权，操作未执行" };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TrustInstallFlow] 提权证书操作失败: {ex}");
            return new TrustOperationResult { Ok = false, Message = $"操作失败：{ex.Message}" };
        }
        finally
        {
            try
            {
                if (File.Exists(resultFile)) File.Delete(resultFile);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TrustInstallFlow] 清理结果文件失败: {ex.Message}");
            }
        }
    }
}
