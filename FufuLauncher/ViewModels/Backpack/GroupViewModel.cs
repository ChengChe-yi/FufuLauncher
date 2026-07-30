/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
namespace FufuLauncher.ViewModels;

public abstract class GroupViewModel
{
    public string Key { get; }
    public string Header { get; }

    protected GroupViewModel(string key, string header)
    {
        Key = key;
        Header = header;
    }
}

public sealed class GroupViewModel<TItem> : GroupViewModel
{
    public IReadOnlyList<TItem> Items { get; }

    public GroupViewModel(string key, string header, IReadOnlyList<TItem> items)
        : base(key, header) => Items = items;
}
