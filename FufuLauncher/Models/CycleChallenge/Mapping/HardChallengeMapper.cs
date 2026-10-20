/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using System.Globalization;
using System.Text.Json;
using FufuLauncher.Helpers;
using FufuLauncher.Models.CycleChallenge.Dto;
using FufuLauncher.Services.CycleChallenge;

namespace FufuLauncher.Models.CycleChallenge.Mapping;

public static class HardChallengeMapper
{
    public static HardChallengeDisplay Parse(string? payloadJson, string? region = null)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return HardChallengeDisplay.CreateState(StygianDisplayState.NotParticipated);
        }

        var envelope = CycleChallengePayloadReader.Read(payloadJson);
        if (envelope.Entry is not { } entryElement)
        {
            return HardChallengeDisplay.CreateState(envelope.IsUnlock == false
                ? StygianDisplayState.NotUnlocked
                : StygianDisplayState.NotParticipated);
        }

        var entry = entryElement.Deserialize<HardChallengeEntryDto>(CycleChallengePayloadReader.SerializerOptions);
        return Map(entry, envelope.IsUnlock, region);
    }


    public static HardChallengeDisplay Map(HardChallengeEntryDto? entry, bool? isUnlock, string? region = null)
    {
        var context = CycleChallengeTime.CreateContext(region);

        if (entry is null)
        {
            return HardChallengeDisplay.CreateState(isUnlock == false
                ? StygianDisplayState.NotUnlocked
                : StygianDisplayState.NotParticipated);
        }

        if (isUnlock == false)
        {
            return HardChallengeDisplay.CreateState(StygianDisplayState.NotUnlocked);
        }

        var solo = MapMode(entry.Single);
        var coop = MapMode(entry.MultiPlayer);
        var blings = MapBlings(entry.Blings);

        if (!solo.HasData && !coop.HasData)
        {
            return HardChallengeDisplay.CreateState(StygianDisplayState.NotParticipated);
        }

        var schedule = entry.Schedule;

        return new HardChallengeDisplay
        {
            State = StygianDisplayState.Content,
            ScheduleName = schedule?.Name ?? string.Empty,
            PeriodText = FormatPeriod(schedule, context),
            Solo = solo,
            Coop = coop,
            Blings = blings,
        };
    }

    private static StygianMode MapMode(HardChallengeModeDto? mode)
    {
        if (mode is null)
        {
            return StygianMode.CreateEmpty();
        }

        var hasData = mode.HasData ?? mode.Best is not null;
        if (!hasData)
        {
            return StygianMode.CreateEmpty();
        }

        var best = mode.Best;
        var difficulty = best?.Difficulty ?? 0;


        var iconName = SplitIconName(best?.Icon);
        var iconPath = CycleChallengeIcons.DifficultyMedal(difficulty, iconName);

        var challenges = MapChallenges(mode.Challenges);
        var extremeValue = FindExtremeValue(challenges);

        return new StygianMode(
            true,
            StygianDifficulty.GetDisplayName(difficulty),
            iconPath,
            FormatSeconds(best?.Second ?? 0),
            extremeValue,
            challenges);
    }

    private static List<StygianChallenge> MapChallenges(List<HardChallengeChallengeDto>? challenges)
    {
        if (challenges is null || challenges.Count == 0)
        {
            return [];
        }

        var result = new List<StygianChallenge>(challenges.Count);
        foreach (var challenge in challenges)
        {
            if (challenge is null)
            {
                continue;
            }

            result.Add(new StygianChallenge(
                challenge.Name ?? string.Empty,
                challenge.Second ?? 0,
                FormatSeconds(challenge.Second ?? 0),
                MapTeam(challenge.Teams),
                MapBestAvatars(challenge.BestAvatars),
                MapMonster(challenge.Monster)));
        }

        return result;
    }

    private static List<StygianAvatar> MapTeam(List<HardChallengeAvatarDto>? team)
    {
        if (team is null || team.Count == 0)
        {
            return [];
        }

        var result = new List<StygianAvatar>(team.Count);
        foreach (var avatar in team)
        {
            if (avatar is null)
            {
                continue;
            }

            result.Add(new StygianAvatar(
                avatar.AvatarId ?? 0,
                avatar.Image,
                avatar.Level ?? 0,
                avatar.Rarity ?? 0,
                avatar.Name,
                avatar.Rank ?? 0));
        }

        return result;
    }

    private static List<StygianBestAvatar> MapBestAvatars(List<HardChallengeBestAvatarDto>? bestAvatars)
    {
        if (bestAvatars is null || bestAvatars.Count == 0)
        {
            return [];
        }

        var result = new List<StygianBestAvatar>(bestAvatars.Count);
        foreach (var avatar in bestAvatars)
        {
            if (avatar is null)
            {
                continue;
            }

            var type = avatar.Type ?? 0;
            result.Add(new StygianBestAvatar(
                avatar.AvatarId ?? 0,
                avatar.SideIcon,
                FormatDps(avatar.Dps),
                BestAvatarTypeText(type),
                type));
        }

        return result;
    }

    private static StygianMonster MapMonster(HardChallengeMonsterDto? monster)
    {
        if (monster is null)
        {
            return StygianMonster.Empty;
        }


        var descriptions = new List<string>();
        if (monster.Descriptions is { Count: > 0 })
        {
            foreach (var description in monster.Descriptions)
            {
                if (!string.IsNullOrWhiteSpace(description))
                {
                    descriptions.Add(description);
                }
            }
        }

        return new StygianMonster(monster.Name, monster.Icon, monster.Level ?? 0, descriptions);
    }

    private static List<StygianBling> MapBlings(List<HardChallengeBlingAvatarDto>? blings)
    {
        if (blings is null || blings.Count == 0)
        {
            return [];
        }

        var result = new List<StygianBling>(blings.Count);
        foreach (var bling in blings)
        {
            if (bling is null)
            {
                continue;
            }

            result.Add(new StygianBling(
                bling.AvatarId ?? 0,
                bling.Image,
                bling.Rarity ?? 0,
                bling.Name,
                bling.IsPlus ?? false));
        }

        return result;
    }


    private static string? SplitIconName(string? iconName)
    {
        if (string.IsNullOrWhiteSpace(iconName))
        {
            return null;
        }

        var items = iconName.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return items.Length == 0 ? null : items[^1];
    }


    private static string BestAvatarTypeText(int type) => type switch
    {
        1 => "Stygian_BestType_1".GetLocalized(),
        2 => "Stygian_BestType_2".GetLocalized(),
        _ => "Stygian_BestType_Other".GetLocalized(),
    };


    private static string FormatSeconds(int seconds)
    {
        if (seconds <= 0)
        {
            return string.Empty;
        }

        return $"{seconds.ToString(CultureInfo.InvariantCulture)} {"Stygian_Second".GetLocalized()}";
    }


    private static string FindExtremeValue(List<StygianChallenge> challenges)
    {
        long max = 0;
        foreach (var challenge in challenges)
        {
            foreach (var avatar in challenge.BestAvatars)
            {
                if (TryParseDps(avatar.ValueText, out var value) && value > max)
                {
                    max = value;
                }
            }
        }

        return max > 0 ? max.ToString("N0", CultureInfo.InvariantCulture) : string.Empty;
    }


    private static string FormatDps(string? dps)
    {
        if (string.IsNullOrWhiteSpace(dps))
        {
            return string.Empty;
        }

        var raw = dps.Trim();
        return TryParseDps(raw, out var value) && value > 0
            ? value.ToString("N0", CultureInfo.InvariantCulture)
            : raw;
    }

    private static bool TryParseDps(string? text, out long value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }


        return long.TryParse(
            text.Replace(",", string.Empty, StringComparison.Ordinal).Trim(),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out value);
    }


    private static string FormatPeriod(HardChallengeScheduleDto? schedule, CycleChallengeTimeContext context)
    {
        if (schedule is null)
        {
            return string.Empty;
        }

        var start = FormatUnixTime(schedule.StartTime, context) ?? FormatDateTime(schedule.StartDateTime, context);
        var end = FormatUnixTime(schedule.EndTime, context) ?? FormatDateTime(schedule.EndDateTime, context);

        if (start is not null && end is not null)
        {
            return $"{start} - {end}";
        }

        return start ?? end ?? string.Empty;
    }

    private static string? FormatDateTime(HardChallengeDateTimeDto? dateTime, CycleChallengeTimeContext context)
    {
        if (dateTime is null)
        {
            return null;
        }

        var year = dateTime.Year ?? 0;
        var month = dateTime.Month ?? 0;
        var day = dateTime.Day ?? 0;
        if (year <= 0 || month <= 0 || day <= 0)
        {
            return null;
        }

        var wallClock = new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Unspecified);
        return CycleChallengeTime.FormatWallClock(wallClock, "yyyy.MM.dd", context);
    }

    private static string? FormatUnixTime(string? raw, CycleChallengeTimeContext context)
    {
        if (string.IsNullOrWhiteSpace(raw)
            || !long.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var unix)
            || unix <= 0)
        {
            return null;
        }

        return CycleChallengeTime.FormatUnix(unix, "yyyy.MM.dd", context);
    }
}