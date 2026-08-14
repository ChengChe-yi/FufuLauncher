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
    #region Characters

    private List<DcCharacterCard> _filteredCharacters = new();

    public ObservableCollection<DcCharacterCard> Characters { get; } = new();

    public List<DcOption> CharacterSortOptions { get; }
    public List<DcOption> StarFilterOptions { get; }

    private string _characterSearch = string.Empty;
    private int _characterStarFilter;
    private string _characterSort = "heat";
    private int _characterShown;

    public string CharacterCountText { get; private set; } = string.Empty;
    public bool HasMoreCharacters { get; private set; }
    public bool HasNoCharacters { get; private set; }
    public string CharacterMoreText { get; private set; } = string.Empty;

    public void SearchCharacters(string? keyword)
    {
        _characterSearch = keyword?.Trim() ?? string.Empty;
        ApplyCharacterFilter();
    }

    public void SetCharacterStarFilter(int star)
    {
        _characterStarFilter = star;
        ApplyCharacterFilter();
    }

    public void SetCharacterSort(string? sort)
    {
        _characterSort = string.IsNullOrEmpty(sort) ? "heat" : sort;
        ApplyCharacterFilter();
    }

    public void ShowMoreCharacters()
    {
        _characterShown = Math.Min(_characterShown + CharacterPageSize, _filteredCharacters.Count);
        PushCharacterPage();
    }

    private void ApplyCharacterFilter()
    {
        IEnumerable<DcCharacterCard> query = _allCharacters;

        if (_characterStarFilter is 4 or 5)
        {
            query = query.Where(c => c.Star == _characterStarFilter);
        }

        if (!string.IsNullOrEmpty(_characterSearch))
        {
            query = query.Where(c => c.SearchKey.Contains(_characterSearch, StringComparison.OrdinalIgnoreCase));
        }

        query = _characterSort switch
        {
            "abyss" => query.OrderByDescending(c => c.SortAbyss).ThenByDescending(c => c.SortHeat),
            "value" => query.OrderByDescending(c => c.SortValue).ThenByDescending(c => c.SortAbyss),
            "own" => query.OrderByDescending(c => c.SortOwn).ThenByDescending(c => c.SortHeat),
            "level" => query.OrderByDescending(c => c.SortLevel).ThenByDescending(c => c.SortHeat),
            "damage" => query.OrderByDescending(c => c.SortDamage).ThenByDescending(c => c.SortHeat),
            _ => query.OrderByDescending(c => c.SortHeat).ThenByDescending(c => c.SortAbyss)
        };

        _filteredCharacters = query.ToList();
        _characterShown = Math.Min(CharacterPageSize, _filteredCharacters.Count);
        PushCharacterPage();
    }

    private void PushCharacterPage()
    {
        Characters.Clear();
        for (var i = 0; i < _characterShown; i++) Characters.Add(_filteredCharacters[i]);

        HasMoreCharacters = _characterShown < _filteredCharacters.Count;
        HasNoCharacters = _filteredCharacters.Count == 0;
        CharacterCountText = LF("DataPage_ShownCount", _characterShown, _filteredCharacters.Count);
        CharacterMoreText = LF("DataPage_ShowMore",
            Math.Min(CharacterPageSize, Math.Max(0, _filteredCharacters.Count - _characterShown)));

        OnPropertyChanged(nameof(HasMoreCharacters));
        OnPropertyChanged(nameof(HasNoCharacters));
        OnPropertyChanged(nameof(CharacterCountText));
        OnPropertyChanged(nameof(CharacterMoreText));
    }

    private DcCharacterDetail? _selectedCharacter;

    public DcCharacterDetail? SelectedCharacter
    {
        get => _selectedCharacter;
        private set
        {
            _selectedCharacter = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedCharacterTitle));
        }
    }

    public string SelectedCharacterTitle =>
        SelectedCharacter == null ? string.Empty : LF("DataPage_DetailTitle", SelectedCharacter.Name);

    public void SelectCharacter(DcCharacterCard? card) => SelectedCharacter = card?.Detail;

    private void BuildCharacters()
    {
        _allCharacters.Clear();
        if (_roleAvg?.Result == null)
        {
            ApplyCharacterFilter();
            return;
        }

        var spiralRanks = IndexRanks(_spiralLatest);
        var stygianRanks = IndexRanks(_stygianLatest);
        var spiralTiers = IndexTiers(_spiralLatest);
        var stygianTiers = IndexTiers(_stygianLatest);
        var rerunIndex = IndexRerun();

        foreach (var entry in _roleAvg.Result)
        {
            var name = entry.Role;
            if (string.IsNullOrEmpty(name)) continue;

            spiralRanks.TryGetValue(name, out var spiralRank);
            stygianRanks.TryGetValue(name, out var stygianRank);
            spiralTiers.TryGetValue(name, out var spiralTier);
            stygianTiers.TryGetValue(name, out var stygianTier);

            var abyssPick = spiralRank?.UseRate ?? 0;
            var stygianPick = stygianRank?.UseRate ?? 0;
            var ownRate = spiralRank?.OwnRate ?? stygianRank?.OwnRate ?? 0;

            var fieldShare = abyssPick * ownRate / 100d;

            var score = Math.Clamp(
                0.40 * abyssPick + 0.25 * stygianPick + 0.20 * fieldShare + 0.15 * ownRate, 0, 100);
            var (tierText, tierTag) = ScoreToTier(score);

            var valueIndex = abyssPick - ownRate;

            var star = entry.Star ?? 5;
            var (headline, headlineTag) = BuildHeadline(score, valueIndex, ownRate, spiralRank);

            var detail = BuildCharacterDetail(entry, spiralRank, stygianRank, spiralTier ?? stygianTier,
                score, tierText, tierTag, rerunIndex);

            _allCharacters.Add(new DcCharacterCard
            {
                Name = name!,
                Ename = entry.Ename ?? string.Empty,
                Avatar = entry.Avatar,
                Star = star,
                StarText = star + L("DataPage_StarUnit"),
                MetaScore = score,
                MetaScoreText = score.ToString("0", CultureInfo.InvariantCulture),
                TierText = tierText,
                TierTag = tierTag,
                LevelText = "Lv." + Fmt(entry.AvgLevel, 1),
                ConstellationText = "C" + Fmt(entry.AvgConstellation, 2),
                TalentText = $"{Fmt(entry.Ability1, 1)} / {Fmt(entry.Ability2, 1)} / {Fmt(entry.Ability3, 1)}",
                SampleText = LF("DataPage_SampleFormat", Compact(entry.RoleSum)),
                DamageText = NumText(entry.Damage),
                DamageName = entry.DamageName ?? string.Empty,
                AbyssRateText = PctText(spiralRank?.UseRate),
                StygianRateText = PctText(stygianRank?.UseRate),
                OwnRateText = PctText(ownRate > 0 ? ownRate : null),
                TopWeapons = BuildWeaponRows(entry.Weapons, 3),
                TopArtifacts = BuildArtifactRows(entry.ArtifactSets, 3),
                HeadlineText = headline,
                HeadlineTag = headlineTag,
                SortHeat = score,
                SortOwn = ownRate,
                SortAbyss = abyssPick,
                SortLevel = entry.AvgLevel ?? 0,
                SortDamage = entry.Damage ?? 0,
                SortValue = valueIndex,
                SearchKey = string.Join(' ', name, entry.Ename, entry.DamageName),
                Detail = detail
            });
        }

        ApplyCharacterFilter();
    }

    private static (string text, string tag) BuildHeadline(double score, double valueIndex, double ownRate,
        AbyssRankEntry? spiralRank)
    {
        if (score >= 78) return (L("DataPage_HeadlineTop"), "s1");
        if (valueIndex >= 20) return (L("DataPage_HeadlineValue"), "up");
        if (spiralRank?.UseRateChange is >= 15) return (L("DataPage_HeadlineRising"), "up");
        if (spiralRank?.UseRateChange is <= -15) return (L("DataPage_HeadlineFalling"), "down");
        if (ownRate >= 95) return (L("DataPage_HeadlinePopular"), "a");
        return (string.Empty, "flat");
    }

    private DcCharacterDetail BuildCharacterDetail(RoleAvgEntry entry, AbyssRankEntry? spiralRank,
        AbyssRankEntry? stygianRank, AbyssTierEntry? tierEntry, double score, string tierText, string tierTag,
        Dictionary<string, RerunEntry> rerunIndex)
    {
        var name = entry.Role ?? string.Empty;
        var star = entry.Star ?? 5;
        var ownRate = spiralRank?.OwnRate ?? stygianRank?.OwnRate;

        var detail = new DcCharacterDetail
        {
            Name = name,
            Ename = entry.Ename ?? string.Empty,
            Avatar = entry.Avatar,
            StarText = star + L("DataPage_StarUnit"),
            StarTag = star == 5 ? "star5" : "star4",
            TierText = tierText,
            TierTag = tierTag,
            MetaScoreText = score.ToString("0", CultureInfo.InvariantCulture),
            SubtitleText = string.IsNullOrEmpty(entry.DamageName)
                ? entry.Ename ?? string.Empty
                : $"{entry.Ename} · {entry.DamageName}"
        };

        detail.Metrics.Add(new DcKpiTile
        {
            Glyph = GlyphBolt, Title = L("DataPage_AbyssPick"), Value = PctText(spiralRank?.UseRate),
            Caption = RankClassText(spiralRank?.RankClass), ColorTag = "accent"
        });
        detail.Metrics.Add(new DcKpiTile
        {
            Glyph = GlyphFlag, Title = L("DataPage_StygianPick"), Value = PctText(stygianRank?.UseRate),
            Caption = RankClassText(stygianRank?.RankClass), ColorTag = "accent"
        });
        detail.Metrics.Add(new DcKpiTile
        {
            Glyph = GlyphPeople, Title = L("DataPage_OwnRate"), Value = PctText(ownRate),
            Caption = L("DataPage_FieldShare") + " " +
                      PctText((spiralRank?.UseRate ?? 0) * (ownRate ?? 0) / 100d),
            ColorTag = "accent"
        });
        detail.Metrics.Add(new DcKpiTile
        {
            Glyph = GlyphLevel, Title = L("DataPage_MetricAvgLevel"), Value = "Lv." + Fmt(entry.AvgLevel, 1),
            Caption = L("DataPage_MetricTalent") +
                      $" {Fmt(entry.Ability1, 1)}/{Fmt(entry.Ability2, 1)}/{Fmt(entry.Ability3, 1)}",
            ColorTag = "accent"
        });
        detail.Metrics.Add(new DcKpiTile
        {
            Glyph = GlyphStar, Title = L("DataPage_MetricAvgConst"), Value = "C" + Fmt(entry.AvgConstellation, 2),
            Caption = L("DataPage_ZeroConstellation") + " " + PctText(tierEntry?.C0Rate ?? entry.C0), ColorTag = "accent"
        });
        detail.Metrics.Add(new DcKpiTile
        {
            Glyph = GlyphBolt, Title = L("DataPage_CoreDamage"), Value = NumText(entry.Damage),
            Caption = entry.DamageName ?? string.Empty, ColorTag = "accent"
        });

        if (stygianRank?.ClearTime is > 0)
        {
            detail.Metrics.Add(new DcKpiTile
            {
                Glyph = GlyphStopwatch, Title = L("DataPage_ClearTime"),
                Value = Fmt(stygianRank.ClearTime, 1) + "s", Caption = L("DataPage_TabStygian"), ColorTag = "accent"
            });
        }

        detail.Metrics.Add(new DcKpiTile
        {
            Glyph = GlyphChart, Title = L("DataPage_MetricSample"), Value = Compact(entry.RoleSum),
            Caption = CleanVersion(_roleAvg?.Version), ColorTag = "accent"
        });

        detail.Weapons = BuildWeaponRows(entry.Weapons, 6);
        detail.Artifacts = BuildArtifactRows(entry.ArtifactSets, 6);

        var constellations = tierEntry != null
            ? new[]
            {
                tierEntry.C0Rate, tierEntry.C1Rate, tierEntry.C2Rate, tierEntry.C3Rate, tierEntry.C4Rate,
                tierEntry.C5Rate, tierEntry.C6Rate
            }
            : new[] { entry.C0, entry.C1, entry.C2, entry.C3, entry.C4, entry.C5, entry.C6 };

        var maxConst = constellations.Max(c => c ?? 0);
        for (var i = 0; i < constellations.Length; i++)
        {
            var value = constellations[i] ?? 0;
            detail.Constellations.Add(new DcBar
            {
                Label = "C" + i,
                Value = value,
                ValueText = PctText(value),
                ColorTag = i == 0 ? "accent" : "muted",
                IsHighlighted = value > 0 && Math.Abs(value - maxConst) < 0.001
            });
        }

        detail.Teams = FindTeamsFor(entry.Avatar, name, 4);

        if (_wishStats.TryGetValue(name, out var stat) && stat.Count > 0)
        {
            foreach (var version in stat.Banners.Take(12))
            {
                detail.BannerHistory.Add(new DcNamedIcon { Name = version, Star = star });
            }

            detail.WishSummary = LF("DataPage_WishSummaryFormat", stat.Count, stat.LatestVersion,
                stat.DaysSince?.ToString("0", CultureInfo.InvariantCulture) ?? Dash,
                stat.AverageGap?.ToString("0", CultureInfo.InvariantCulture) ?? Dash);
        }
        else
        {
            detail.WishSummary = L("DataPage_WishSummaryNever");
        }

        if (rerunIndex.TryGetValue(name, out var rerun))
        {
            detail.RerunSummary = LF("DataPage_RerunSummaryFormat", Fmt(rerun.Days, 0), Fmt(rerun.AvgDays, 0),
                Fmt(rerun.MaxGapDays, 0));
        }

        BuildCharacterInsights(detail, entry, spiralRank, ownRate, tierEntry, rerunIndex.GetValueOrDefault(name));

        return detail;
    }

    private static void BuildCharacterInsights(DcCharacterDetail detail, RoleAvgEntry entry,
        AbyssRankEntry? spiralRank, double? ownRateValue, AbyssTierEntry? tierEntry, RerunEntry? rerun)
    {
        var name = entry.Role ?? string.Empty;
        var abyssPick = spiralRank?.UseRate ?? 0;
        var ownRate = ownRateValue ?? 0;

        if (abyssPick >= 60 && ownRate > 0 && abyssPick - ownRate >= 15)
        {
            detail.Insights.Add(new DcInsight
            {
                Glyph = GlyphStarFill, ColorTag = "up", Title = L("DataPage_InsightValueTitle"),
                Body = LF("DataPage_InsightValueBody", name, PctText(abyssPick), PctText(ownRate))
            });
        }
        else if (ownRate >= 95 && abyssPick is > 0 and < 30)
        {
            detail.Insights.Add(new DcInsight
            {
                Glyph = GlyphWarning, ColorTag = "down", Title = L("DataPage_InsightBenchTitle"),
                Body = LF("DataPage_InsightBenchBody", name, PctText(ownRate), PctText(abyssPick))
            });
        }

        if (spiralRank is { UseRateChange: { } change, UseRateOld: not null } && Math.Abs(change) >= 8)
        {
            detail.Insights.Add(new DcInsight
            {
                Glyph = change > 0 ? GlyphUp : GlyphDown,
                ColorTag = change > 0 ? "up" : "down",
                Title = change > 0 ? L("DataPage_InsightRiseTitle") : L("DataPage_InsightFallTitle"),
                Body = LF(change > 0 ? "DataPage_InsightRiseBody" : "DataPage_InsightFallBody",
                    name, PctText(spiralRank.UseRateOld), PctText(spiralRank.UseRate))
            });
        }

        var zeroConst = tierEntry?.C0Rate ?? entry.C0;
        if (zeroConst is >= 70)
        {
            detail.Insights.Add(new DcInsight
            {
                Glyph = GlyphCheck, ColorTag = "up", Title = L("DataPage_InsightNoConstTitle"),
                Body = LF("DataPage_InsightNoConstBody", name, PctText(zeroConst))
            });
        }
        else if (zeroConst is > 0 and < 40)
        {
            detail.Insights.Add(new DcInsight
            {
                Glyph = GlyphWarning, ColorTag = "down", Title = L("DataPage_InsightConstTitle"),
                Body = LF("DataPage_InsightConstBody", name, PctText(zeroConst))
            });
        }

        var topWeapon = entry.Weapons?.FirstOrDefault();
        if (topWeapon is { Rate: >= 50, Name: { Length: > 0 } weaponName })
        {
            detail.Insights.Add(new DcInsight
            {
                Glyph = GlyphWeapon, ColorTag = "accent", Title = L("DataPage_InsightWeaponTitle"),
                Body = LF("DataPage_InsightWeaponBody", weaponName, PctText(topWeapon.Rate), name)
            });
        }

        var topArtifact = entry.ArtifactSets?.FirstOrDefault();
        if (topArtifact is { Rate: >= 50, Name: { Length: > 0 } artifactName })
        {
            detail.Insights.Add(new DcInsight
            {
                Glyph = GlyphArtifact, ColorTag = "accent", Title = L("DataPage_InsightArtifactTitle"),
                Body = LF("DataPage_InsightArtifactBody", artifactName, PctText(topArtifact.Rate))
            });
        }

        if (rerun is { Days: { } days, AvgDays: > 0 } && days >= rerun.AvgDays!.Value)
        {
            detail.Insights.Add(new DcInsight
            {
                Glyph = GlyphHistory, ColorTag = "up", Title = L("DataPage_InsightOverdueTitle"),
                Body = LF("DataPage_InsightOverdueBody", name, Fmt(days, 0), Fmt(rerun.AvgDays, 0))
            });
        }
    }

    private static Dictionary<string, AbyssRankEntry> IndexRanks(AbyssStatsBundle? bundle)
    {
        var map = new Dictionary<string, AbyssRankEntry>(StringComparer.Ordinal);
        foreach (var item in bundle?.Ranks ?? new List<AbyssRankEntry>())
        {
            if (!string.IsNullOrEmpty(item.Name)) map[item.Name!] = item;
        }

        return map;
    }

    private static Dictionary<string, AbyssTierEntry> IndexTiers(AbyssStatsBundle? bundle)
    {
        var map = new Dictionary<string, AbyssTierEntry>(StringComparer.Ordinal);
        foreach (var group in bundle?.Tiers ?? new List<AbyssTierGroup>())
        {
            foreach (var item in group.List ?? new List<AbyssTierEntry>())
            {
                if (!string.IsNullOrEmpty(item.Name)) map[item.Name!] = item;
            }
        }

        return map;
    }

    private Dictionary<string, RerunEntry> IndexRerun()
    {
        var map = new Dictionary<string, RerunEntry>(StringComparer.Ordinal);

        foreach (var group in (_rerun?.Result ?? new List<List<RerunEntry>>()).Take(2))
        {
            foreach (var entry in group)
            {
                if (!string.IsNullOrEmpty(entry.Name)) map[entry.Name!] = entry;
            }
        }

        return map;
    }

    private List<DcTeamCard> FindTeamsFor(string? avatar, string name, int take)
    {
        var source = Spiral.AllTeams.Count > 0 ? Spiral.AllTeams : Stygian.AllTeams;
        var result = new List<DcTeamCard>();

        foreach (var team in source)
        {
            var match = team.Members.Any(m =>
                (!string.IsNullOrEmpty(avatar) && string.Equals(m.Avatar, avatar, StringComparison.OrdinalIgnoreCase))
                || (!string.IsNullOrEmpty(name) && string.Equals(m.Name, name, StringComparison.Ordinal)));

            if (!match) continue;

            result.Add(team);
            if (result.Count >= take) break;
        }

        return result;
    }

    private static List<DcRateRow> BuildWeaponRows(List<WeaponUsageEntry>? source, int take)
    {
        var rows = new List<DcRateRow>();
        foreach (var item in (source ?? new List<WeaponUsageEntry>()).Take(take))
        {
            rows.Add(new DcRateRow
            {
                Name = item.Name ?? string.Empty,
                Icon = item.Avatar,
                Rate = item.Rate ?? 0,
                RateText = PctText(item.Rate),
                ColorTag = "accent"
            });
        }

        return rows;
    }

    private static List<DcRateRow> BuildArtifactRows(List<ArtifactUsageEntry>? source, int take)
    {
        var rows = new List<DcRateRow>();
        foreach (var item in (source ?? new List<ArtifactUsageEntry>()).Take(take))
        {
            rows.Add(new DcRateRow
            {
                Name = item.Name ?? string.Empty,
                Icon = item.Avatars is { Count: > 0 } avatars ? avatars[0] : null,
                Rate = item.Rate ?? 0,
                RateText = PctText(item.Rate),
                ColorTag = "accent"
            });
        }

        return rows;
    }

    #endregion
}
