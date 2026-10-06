using FufuLauncher.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FufuLauncher.Selectors;

public sealed class PluginConfigTemplateSelector : DataTemplateSelector
{
    public DataTemplate? BooleanTemplate { get; set; }
    public DataTemplate? NumberTemplate { get; set; }
    public DataTemplate? TextTemplate { get; set; }

    protected override DataTemplate SelectTemplateCore(object item)
    {
        return GetTemplate(item) ?? base.SelectTemplateCore(item);
    }

    protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
    {
        return GetTemplate(item) ?? base.SelectTemplateCore(item, container);
    }

    private DataTemplate? GetTemplate(object item)
    {
        return item is PluginConfigOption option
            ? option.Kind switch
            {
                PluginConfigValueKind.Boolean => BooleanTemplate,
                PluginConfigValueKind.Integer or PluginConfigValueKind.Number => NumberTemplate,
                _ => TextTemplate
            }
            : null;
    }
}