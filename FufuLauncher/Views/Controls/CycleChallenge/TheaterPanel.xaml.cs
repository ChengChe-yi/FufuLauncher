/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using System.Diagnostics;
using FufuLauncher.Models.CycleChallenge;
using FufuLauncher.Models.CycleChallenge.Mapping;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using FufuLauncher.Helpers;

namespace FufuLauncher.Views.Controls.CycleChallenge;

public sealed partial class TheaterPanel : UserControl
{
    private RoleCombatDisplay _display = RoleCombatDisplay.CreateState(TheaterDisplayState.NotParticipated);

    public TheaterPanel()
    {
        InitializeComponent();
    }


    public RoleCombatDisplay Display => _display;


    public string? Region
    {
        get;
        set;
    }


    public void Show(string payloadJson)
    {
        RoleCombatDisplay display;
        try
        {
            display = RoleCombatMapper.Parse(payloadJson, Region);
        }
        catch (Exception ex)
        {
            DebugLog.Info("TheaterPanel", $"[TheaterPanel] Failed to parse payload: {ex}");
            display = RoleCombatDisplay.CreateError(ex.Message);
        }

        SetDisplay(display);
    }


    private void SetDisplay(RoleCombatDisplay display)
    {
        _display = display;

        try
        {
            Bindings.Update();
        }
        catch (Exception ex)
        {
            DebugLog.Info("TheaterPanel", $"[TheaterPanel] Refresh binding failed: {ex.Message}");
        }
    }


    private void OnSplendourSummaryLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is RichTextBlock block && block.Tag is string text && text.Length > 0)
        {
            CycleChallengeRichTextParser.Apply(block, [text]);
        }
    }


    private void OnSplendourTooltipLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is RichTextBlock block && block.Tag is TheaterSplendourBuff buff)
        {
            CycleChallengeRichTextParser.Apply(block, buff.Effects);
        }
    }


    private void OnBuffTooltipLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is RichTextBlock block && block.Tag is TheaterBuff buff && buff.HasDescription)
        {
            CycleChallengeRichTextParser.Apply(block, [buff.Description]);
        }
    }
}