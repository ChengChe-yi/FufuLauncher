using System.Collections.ObjectModel;
using FufuLauncher.ViewModels;

namespace FufuLauncher.Helpers;

// Shared by the main-plugin settings and the generic plugin configuration page.
public static class VirtualKeyCatalog
{
    public static ObservableCollection<VirtualKeyOption> Keys { get; } = new(
        Enum.GetValues<Windows.System.VirtualKey>()
            .Where(key => key != Windows.System.VirtualKey.None)
            .Select(key => new VirtualKeyOption { KeyCode = (int)key, KeyName = key.ToString() })
            .GroupBy(key => key.KeyCode).Select(group => group.First()).OrderBy(key => key.KeyCode));
}
