/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Collections.ObjectModel;
using System.Globalization;
using FufuLauncher.Models.DataCenter;

namespace FufuLauncher.ViewModels;

public sealed partial class DataViewModel
{
    #region Overview

    public ObservableCollection<DcKpiTile> OverviewKpis { get; } = new();
    public ObservableCollection<DcInsight> OverviewInsights { get; } = new();
    public ObservableCollection<DcMoverRow> OverviewRisers { get; } = new();
    public ObservableCollection<DcMoverRow> OverviewFallers { get; } = new();
    public ObservableCollection<DcRankRow> OverviewTopTier { get; } = new();
    public ObservableCollection<DcWishBanner> OverviewBanners { get; } = new();
    public ObservableCollection<DcCountRow> OverviewValuePicks { get; } = new();
    public ObservableCollection<DcRerunCard> OverviewOverdue { get; } = new();

    public bool HasOverviewBanners { get; private set; }
    public bool HasOverviewMovers { get; private set; }

    private void BuildOverview()
    {
        OverviewKpis.Clear();
        OverviewInsights.Clear();
        OverviewRisers.Clear();
        OverviewFallers.Clear();
        OverviewTopTier.Clear();
        OverviewBanners.Clear();
        OverviewValuePicks.Clear();
        OverviewOverdue.Clear();

        var spiral = _spiralLatest?.Response;
        var stygian = _stygianLatest?.Response;

        OverviewKpis.Add(new DcKpiTile
        {
            Glyph = GlyphInfo, Title = L("DataPage_KpiVersion"),
            Value = CleanVersion(spiral?.Version ?? _roleAvg?.Version),
            Caption = spiral?.UpdateInfo ?? string.Empty, ColorTag = "accent"
        });
        OverviewKpis.Add(new DcKpiTile
        {
            Glyph = GlyphChart, Title = L("DataPage_KpiSample"), Value = Compact(spiral?.SampleCount),
            Caption = L("DataPage_KpiStygianSample") + " " + Compact(stygian?.SampleCount), ColorTag = "accent"
        });
        OverviewKpis.Add(new DcKpiTile
        {
            Glyph = GlyphStarFill, Title = L("DataPage_KpiFullStar"), Value = spiral?.FullStarRate ?? Dash,
            Caption = L("DataPage_KpiOnceFullStar") + " " + (spiral?.FullStarOnceRate ?? Dash), ColorTag = "up"
        });
        OverviewKpis.Add(new DcKpiTile
        {
            Glyph = GlyphSync, Title = L("DataPage_KpiRestart"), Value = Fmt(spiral?.RestartTimesAvg, 1),
            Caption = L("DataPage_UnitTimesShort"), ColorTag = "accent"
        });
        OverviewKpis.Add(new DcKpiTile
        {
            Glyph = GlyphPeople, Title = L("DataPage_KpiCharacterCount"),
            Value = (_roleAvg?.Result?.Count ?? 0).ToString(CultureInfo.InvariantCulture),
            Caption = _roleAvg?.LastUpdate ?? string.Empty, ColorTag = "accent"
        });
        OverviewKpis.Add(new DcKpiTile
        {
            Glyph = GlyphStar, Title = L("DataPage_KpiBannerCount"),
            Value = (_wish?.Characters?.Count ?? 0).ToString(CultureInfo.InvariantCulture),
            Caption = LF("DataPage_KpiWeaponBanner", _wish?.Weapons?.Count ?? 0), ColorTag = "accent"
        });

        foreach (var item in Spiral.Risers) OverviewRisers.Add(item);
        foreach (var item in Spiral.Fallers) OverviewFallers.Add(item);
        HasOverviewMovers = OverviewRisers.Count > 0 || OverviewFallers.Count > 0;
        OnPropertyChanged(nameof(HasOverviewMovers));

        var topNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var group in (_spiralLatest?.Tiers ?? new List<AbyssTierGroup>())
                 .Concat(_stygianLatest?.Tiers ?? new List<AbyssTierGroup>()))
        {
            if (NormalizeTierTag(group.RankClass) != "s1") continue;
            foreach (var member in group.List ?? new List<AbyssTierEntry>())
            {
                if (!string.IsNullOrEmpty(member.Name)) topNames.Add(member.Name!);
            }
        }

        var spiralRanks = IndexRanks(_spiralLatest);
        var position = 0;
        foreach (var card in _allCharacters.Where(c => topNames.Contains(c.Name))
                     .OrderByDescending(c => c.SortHeat).Take(12))
        {
            position++;
            spiralRanks.TryGetValue(card.Name, out var rank);
            var change = rank?.UseRateChange;
            var hasChange = change.HasValue && rank?.UseRateOld != null && Math.Abs(change.Value) > 0.05;

            OverviewTopTier.Add(new DcRankRow
            {
                Position = position,
                Name = card.Name,
                Avatar = card.Avatar,
                Star = card.Star,
                UseRate = card.SortAbyss,
                UseRateText = card.AbyssRateText,
                OwnRateText = card.OwnRateText,
                FieldShareText = PctText(card.SortAbyss * card.SortOwn / 100d),
                ConstellationText = card.ConstellationText,
                ClearTimeText = Dash,
                HasClearTime = false,
                HasChange = hasChange,
                ChangeText = hasChange ? SignedPct(change) : Dash,
                ChangeGlyph = hasChange ? change > 0 ? GlyphUp : GlyphDown : string.Empty,
                ChangeTag = !hasChange ? "flat" : change > 0 ? "up" : "down",
                TierText = card.TierText,
                TierTag = card.TierTag
            });
        }

        foreach (var banner in _allCharacterBanners.Where(b => b.StatusTag is "up" or "accent").Take(4))
        {
            OverviewBanners.Add(banner);
        }

        foreach (var banner in _allWeaponBanners.Where(b => b.StatusTag == "up").Take(2))
        {
            OverviewBanners.Add(banner);
        }

        HasOverviewBanners = OverviewBanners.Count > 0;
        OnPropertyChanged(nameof(HasOverviewBanners));

        var picks = _allCharacters
            .Where(c => c.Star == 5 && c.SortAbyss >= 30 && c.SortValue > 0)
            .OrderByDescending(c => c.SortValue)
            .Take(8)
            .ToList();

        var maxValue = picks.Select(c => c.SortValue).DefaultIfEmpty(1).Max();
        position = 0;
        foreach (var card in picks)
        {
            position++;
            OverviewValuePicks.Add(new DcCountRow
            {
                Position = position,
                Name = card.Name,
                Icon = card.Avatar,
                CountText = card.AbyssRateText,
                DetailText = L("DataPage_OwnRate") + " " + card.OwnRateText,
                Ratio = maxValue > 0 ? card.SortValue * 100d / maxValue : 0
            });
        }

        if (_rerunGroups.Count > 0)
        {
            foreach (var card in _rerunGroups[0].OrderByDescending(c => c.SortUrgency).Take(6))
            {
                OverviewOverdue.Add(card);
            }
        }

        BuildOverviewInsights();
    }

    private void BuildOverviewInsights()
    {
        var spiral = _spiralLatest?.Response;

        var leaders = OverviewTopTier.Take(3).Select(r => r.Name).ToList();
        if (spiral != null && leaders.Count > 0)
        {
            OverviewInsights.Add(new DcInsight
            {
                Glyph = GlyphChart, ColorTag = "accent", Title = L("DataPage_InsightMetaTitle"),
                Body = LF("DataPage_InsightMetaBody", CleanVersion(spiral.Version), spiral.FullStarRate ?? Dash,
                    Fmt(spiral.RestartTimesAvg, 1), string.Join(ListSep, leaders))
            });
        }

        if (OverviewRisers.FirstOrDefault() is { } riser)
        {
            OverviewInsights.Add(new DcInsight
            {
                Glyph = GlyphUp, ColorTag = "up", Title = L("DataPage_InsightRiseTitle"),
                Body = LF("DataPage_InsightRiseBody", riser.Name, riser.PreviousText, riser.CurrentText)
            });
        }

        if (OverviewFallers.FirstOrDefault() is { } faller)
        {
            OverviewInsights.Add(new DcInsight
            {
                Glyph = GlyphDown, ColorTag = "down", Title = L("DataPage_InsightFallTitle"),
                Body = LF("DataPage_InsightFallBody", faller.Name, faller.PreviousText, faller.CurrentText)
            });
        }

        if (OverviewValuePicks.FirstOrDefault() is { } pick &&
            _allCharacters.FirstOrDefault(c => c.Name == pick.Name) is { } pickCard)
        {
            OverviewInsights.Add(new DcInsight
            {
                Glyph = GlyphStarFill, ColorTag = "up", Title = L("DataPage_InsightValueTitle"),
                Body = LF("DataPage_InsightValueBody", pickCard.Name, pickCard.AbyssRateText, pickCard.OwnRateText)
            });
        }

        var ongoing = _allCharacterBanners.Where(b => b.StatusTag == "up").ToList();
        if (ongoing.Count > 0)
        {
            var names = ongoing.SelectMany(b => b.Star5).Select(s => s.Name).Distinct(StringComparer.Ordinal).ToList();
            var advice = new List<string>();

            foreach (var name in names.Take(4))
            {
                var card = _allCharacters.FirstOrDefault(c => c.Name == name);
                advice.Add(card == null
                    ? name
                    : LF("DataPage_BannerAdviceItem", name, card.TierText, card.AbyssRateText));
            }

            OverviewInsights.Add(new DcInsight
            {
                Glyph = GlyphStar, ColorTag = "accent", Title = L("DataPage_InsightBannerTitle"),
                Body = LF("DataPage_InsightBannerBody", string.Join(ListSep, names), string.Join(ClauseSep, advice))
            });
        }
        else
        {
            OverviewInsights.Add(new DcInsight
            {
                Glyph = GlyphStar, ColorTag = "flat", Title = L("DataPage_InsightBannerTitle"),
                Body = L("DataPage_InsightBannerNone")
            });
        }

        if (Spiral.AllTeams.FirstOrDefault() is { TeamNames.Length: > 0 } team)
        {
            OverviewInsights.Add(new DcInsight
            {
                Glyph = GlyphPeople, ColorTag = "accent", Title = L("DataPage_InsightTeamTitle"),
                Body = LF("DataPage_InsightTeamBody", team.TeamNames, team.UseRateText)
            });
        }

        if (OverviewOverdue.FirstOrDefault() is { } overdue)
        {
            OverviewInsights.Add(new DcInsight
            {
                Glyph = GlyphHistory, ColorTag = "up", Title = L("DataPage_InsightOverdueTitle"),
                Body = LF("DataPage_InsightOverdueBody", overdue.Name,
                    overdue.SortDays.ToString("0", CultureInfo.InvariantCulture),
                    overdue.SortInterval is > 0 and < double.MaxValue
                        ? overdue.SortInterval.ToString("0", CultureInfo.InvariantCulture)
                        : Dash)
            });
        }
    }

    #endregion
}
