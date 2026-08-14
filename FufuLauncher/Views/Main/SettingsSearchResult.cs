/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using Microsoft.UI.Xaml;

namespace FufuLauncher.Views;

public sealed class SettingsSearchResult
{
    public string Title
    {
        get; init;
    } = string.Empty;

    public string Section
    {
        get; init;
    } = string.Empty;

    public string SectionTag
    {
        get; init;
    } = string.Empty;

    public FrameworkElement? Element
    {
        get; init;
    }

    public override string ToString() => Title;
}
