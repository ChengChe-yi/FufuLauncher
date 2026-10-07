/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using System.Text.Json.Serialization;

namespace FufuLauncher.Models;

public class PluginDependency
{
    [JsonPropertyName("plugin_name")]
    public string PluginName
    {
        get;
        set;
    } = string.Empty;

    [JsonPropertyName("project_name")]
    public string ProjectName
    {
        get;
        set;
    } = string.Empty;

    [JsonPropertyName("project_version")]
    public string ProjectVersion
    {
        get;
        set;
    } = string.Empty;

    [JsonIgnore] public bool IsEmpty => PluginName == "无" && ProjectName == "无" && ProjectVersion == "无";

    public override string ToString()
    {
        if (IsEmpty) return string.Empty;
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(PluginName) && PluginName != "无")
            parts.Add(PluginName);
        if (!string.IsNullOrWhiteSpace(ProjectName) && ProjectName != "无")
            parts.Add(ProjectName);
        if (!string.IsNullOrWhiteSpace(ProjectVersion) && ProjectVersion != "无")
            parts.Add($"v{ProjectVersion}");
        return string.Join(" / ", parts);
    }
}