/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Globalization;
using FufuLauncher.Models.DataCenter;

namespace FufuLauncher.ViewModels;

public sealed partial class DataViewModel
{
    #region Abyss

    public DcAbyssSection Spiral { get; }
    public DcAbyssSection Stygian { get; }

    public void SetAbyssSubView(DcAbyssSection? section, int subView) => section?.SetSubView(subView);

    public void SetAbyssRankSort(DcAbyssSection? section, string? sort)
    {
        if (section == null) return;
        section.RankSort = string.IsNullOrEmpty(sort) ? "use" : sort;

        var bundle = section.IsStygian ? _stygianView : _spiralView;
        if (bundle != null) BuildRanks(section, bundle);
    }

    public void ShowMoreTeams(DcAbyssSection? section)
    {
        if (section == null) return;
        section.TeamShown = Math.Min(section.TeamShown + TeamPageSize, section.AllTeams.Count);
        PushTeamPage(section);
    }

    private void PushTeamPage(DcAbyssSection section)
    {
        section.Teams.Clear();
        for (var i = 0; i < section.TeamShown && i < section.AllTeams.Count; i++)
        {
            section.Teams.Add(section.AllTeams[i]);
        }

        var shown = Math.Min(section.TeamShown, section.AllTeams.Count);
        section.TeamCountText = LF("DataPage_ShownCount", shown, section.AllTeams.Count);
        section.HasMoreTeams = shown < section.AllTeams.Count;
        section.TeamMoreText = LF("DataPage_ShowMore",
            Math.Min(TeamPageSize, Math.Max(0, section.AllTeams.Count - shown)));
    }

    private void BuildAbyssSection(DcAbyssSection section, AbyssStatsBundle bundle)
    {
        var response = bundle.Response;

        section.Kpis.Clear();
        section.Kpis.Add(new DcKpiTile
        {
            Glyph = GlyphChart, Title = L("DataPage_KpiSample"), Value = Compact(response.SampleCount),
            Caption = response.UpdateInfo ?? string.Empty, ColorTag = "accent"
        });
        section.Kpis.Add(new DcKpiTile
        {
            Glyph = GlyphStarFill, Title = L("DataPage_KpiFullStar"), Value = response.FullStarRate ?? Dash,
            Caption = L("DataPage_KpiOnceFullStar") + " " + (response.FullStarOnceRate ?? Dash), ColorTag = "up"
        });
        section.Kpis.Add(new DcKpiTile
        {
            Glyph = GlyphSync, Title = L("DataPage_KpiRestart"), Value = Fmt(response.RestartTimesAvg, 1),
            Caption = L("DataPage_UnitTimesShort"), ColorTag = "accent"
        });
        section.Kpis.Add(new DcKpiTile
        {
            Glyph = GlyphPeople, Title = L("DataPage_KpiCharacterCount"),
            Value = (response.HasList?.Count ?? 0).ToString(CultureInfo.InvariantCulture),
            Caption = CleanVersion(response.Version), ColorTag = "accent"
        });

        section.RestartDistribution.Clear();
        foreach (var item in response.RestartInfo ?? new List<AbyssRestartEntry>())
        {
            section.RestartDistribution.Add(new DcBar
            {
                Label = item.Intro ?? string.Empty,
                Value = item.Rate ?? 0,
                ValueText = PctText(item.Rate),
                ColorTag = "accent"
            });
        }

        if (section.RestartDistribution.All(bar => bar.Value <= 0)) section.RestartDistribution.Clear();
        section.ShowRestartDistribution = section.RestartDistribution.Count > 0;

        BuildTiers(section, bundle);
        BuildRanks(section, bundle);
        BuildTeams(section, bundle);
        BuildMovers(section, bundle);

        if (section.Versions.Count == 0)
        {
            foreach (var option in response.HistoryList ?? new List<AbyssOption>())
            {
                section.Versions.Add(new DcOption { Title = option.Title ?? string.Empty, Value = option.Value });
            }

            if (section.Versions.Count > 0)
            {
                section.LoadedVersion = section.Versions[0].Value;
                section.SelectedVersionIndex = 0;
            }
        }

        if (section.TeamFilters.Count == 0)
        {
            foreach (var option in response.SelectList ?? new List<AbyssOption>())
            {
                section.TeamFilters.Add(new DcOption { Title = option.Title ?? string.Empty, Value = option.Value });
            }

            if (section.TeamFilters.Count > 0)
            {
                section.LoadedTeamFilter = section.TeamFilters[0].Value;
                section.SelectedTeamFilterIndex = 0;
            }
        }

        section.Headline = response.Title ?? string.Empty;
        section.Tips = string.Join(" ",
            new[] { response.Tips, response.Tips2 }.Where(text => !string.IsNullOrEmpty(text)));
    }

    private static void BuildTiers(DcAbyssSection section, AbyssStatsBundle bundle)
    {
        section.Tiers.Clear();

        foreach (var group in bundle.Tiers)
        {
            var members = new List<DcTierMember>();

            foreach (var item in (group.List ?? new List<AbyssTierEntry>()).OrderByDescending(x => x.UseRate ?? 0))
            {
                var parts = new List<string> { L("DataPage_OwnRate") + " " + PctText(item.OwnRate) };
                if (item.C0Rate.HasValue) parts.Add("C0 " + PctText(item.C0Rate));
                if (item.ClearTime is > 0) parts.Add(Fmt(item.ClearTime, 0) + "s");

                members.Add(new DcTierMember
                {
                    Name = item.Name ?? string.Empty,
                    Avatar = item.Avatar,
                    Star = item.Star ?? 5,
                    UseRateText = PctText(item.UseRate),
                    DetailText = string.Join(" · ", parts)
                });
            }

            if (members.Count == 0) continue;

            section.Tiers.Add(new DcTierGroup
            {
                RankName = group.RankName ?? string.Empty,
                TierTag = NormalizeTierTag(group.RankClass),
                CountText = LF("DataPage_TierCount", members.Count),
                Description = TierDescription(group.RankClass),
                Members = members
            });
        }
    }

    private static void BuildRanks(DcAbyssSection section, AbyssStatsBundle bundle)
    {
        section.Ranks.Clear();

        IEnumerable<AbyssRankEntry> query = bundle.Ranks;

        query = section.RankSort switch
        {
            "own" => query.OrderByDescending(x => x.OwnRate ?? 0),
            "change" => query.OrderByDescending(x => x.UseRateChange ?? double.MinValue),
            "time" => query.Where(x => x.ClearTime is > 0).OrderBy(x => x.ClearTime),
            _ => query.OrderByDescending(x => x.UseRate ?? 0)
        };

        var position = 0;
        foreach (var item in query)
        {
            position++;
            var change = item.UseRateChange;
            var hasChange = change.HasValue && item.UseRateOld.HasValue && Math.Abs(change.Value) > 0.05;

            section.Ranks.Add(new DcRankRow
            {
                Position = position,
                Name = item.Name ?? string.Empty,
                Avatar = item.Avatar,
                Star = item.Star ?? 5,
                UseRate = item.UseRate ?? 0,
                UseRateText = PctText(item.UseRate),
                OwnRateText = PctText(item.OwnRate),
                FieldShareText = PctText((item.UseRate ?? 0) * (item.OwnRate ?? 0) / 100d),
                ConstellationText = item.AvgConstellation.HasValue ? "C" + Fmt(item.AvgConstellation, 1) : Dash,
                ClearTimeText = item.ClearTime is > 0 ? Fmt(item.ClearTime, 1) + "s" : Dash,
                HasClearTime = section.ShowClearTime,
                HasChange = hasChange,
                ChangeText = hasChange ? SignedPct(change) : Dash,
                ChangeGlyph = hasChange ? change > 0 ? GlyphUp : GlyphDown : string.Empty,
                ChangeTag = !hasChange ? "flat" : change > 0 ? "up" : "down",
                TierText = RankClassText(item.RankClass),
                TierTag = NormalizeTierTag(item.RankClass)
            });
        }
    }

    private void BuildTeams(DcAbyssSection section, AbyssStatsBundle bundle)
    {
        section.AllTeams.Clear();

        var position = 0;
        foreach (var team in bundle.Teams.OrderByDescending(t => t.UseRate ?? 0))
        {
            var members = new List<DcTeamMember>();
            foreach (var member in team.Members ?? new List<AbyssTeamMember>())
            {
                members.Add(new DcTeamMember
                {
                    Avatar = member.Avatar,
                    Star = member.Star ?? 5,
                    Name = bundle.ResolveName(member.Avatar) ?? string.Empty
                });
            }

            if (members.Count == 0) continue;
            position++;

            var halves = new List<DcBar>();
            AddHalf(halves, "DataPage_HalfFirst", team.FirstHalfRate);
            AddHalf(halves, "DataPage_HalfMid", team.MidHalfRate);
            AddHalf(halves, "DataPage_HalfSecond", team.SecondHalfRate);

            section.AllTeams.Add(new DcTeamCard
            {
                Position = position,
                Members = members,
                TeamNames = string.Join(" · ", members.Select(m => m.Name).Where(n => !string.IsNullOrEmpty(n))),
                UseRate = team.UseRate ?? 0,
                UseRateText = PctText(team.UseRate),
                HasRateText = PctText(team.HasRate),
                AttendRateText = PctText(team.AttendRate),
                UseCountText = LF("DataPage_TeamCount", Compact(team.UseCount)),
                ClearTimeText = team.ClearTime is > 0 ? Fmt(team.ClearTime, 1) + "s" : Dash,
                HasClearTime = team.ClearTime is > 0,
                HalfSplit = halves
            });
        }

        section.TeamShown = Math.Min(TeamPageSize, section.AllTeams.Count);
        PushTeamPage(section);
    }

    private static void AddHalf(List<DcBar> target, string key, double? rate)
    {
        if (rate is not > 0) return;
        target.Add(new DcBar { Label = L(key), Value = rate.Value, ValueText = PctText(rate), ColorTag = "accent" });
    }

    private static void BuildMovers(DcAbyssSection section, AbyssStatsBundle bundle)
    {
        section.Risers.Clear();
        section.Fallers.Clear();

        var withChange = bundle.Ranks
            .Where(r => r.UseRateChange.HasValue && r.UseRateOld.HasValue && Math.Abs(r.UseRateChange!.Value) > 0.05)
            .ToList();

        foreach (var item in withChange.OrderByDescending(r => r.UseRateChange).Take(5))
        {
            section.Risers.Add(ToMover(item));
        }

        foreach (var item in withChange.OrderBy(r => r.UseRateChange).Take(5))
        {
            section.Fallers.Add(ToMover(item));
        }

        section.HasMovers = section.Risers.Count > 0 || section.Fallers.Count > 0;
    }

    private static DcMoverRow ToMover(AbyssRankEntry item)
    {
        var change = item.UseRateChange ?? 0;
        return new DcMoverRow
        {
            Name = item.Name ?? string.Empty,
            Avatar = item.Avatar,
            Star = item.Star ?? 5,
            CurrentText = PctText(item.UseRate),
            PreviousText = PctText(item.UseRateOld),
            ChangeText = SignedPct(change),
            ChangeGlyph = change > 0 ? GlyphUp : GlyphDown,
            ChangeTag = change > 0 ? "up" : "down"
        };
    }

    #endregion
}
