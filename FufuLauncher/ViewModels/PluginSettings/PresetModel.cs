/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using CommunityToolkit.Mvvm.ComponentModel;

namespace FufuLauncher.ViewModels;

public class PresetModel : ObservableObject
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string DllHash { get; set; }
    public Dictionary<string, Dictionary<string, string>> ConfigData { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    
    [System.Text.Json.Serialization.JsonIgnore]
    public string FilePath { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsLocked { get; set; }

    private bool _isActive;
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (SetProperty(ref _isActive, value))
            {
                OnPropertyChanged(nameof(IsNotActive));
            }
        }
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsNotActive => !IsActive;
}
