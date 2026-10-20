/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using System.Globalization;
using FufuLauncher.Helpers;

namespace FufuLauncher.Models.CycleChallenge;

public enum TheaterDisplayState
{
    Content = 0,


    NotUnlocked,


    NotParticipated,


    NoDetail,


    Error,
}

public static class TheaterDifficulty
{
    public static string GetDisplayName(int difficultyId) => difficultyId switch
    {
        1 => "Theater_Difficulty_1".GetLocalized(),
        2 => "Theater_Difficulty_2".GetLocalized(),
        3 => "Theater_Difficulty_3".GetLocalized(),
        4 => "Theater_Difficulty_4".GetLocalized(),
        5 => "Theater_Difficulty_5".GetLocalized(),
        _ => "Theater_Difficulty_Unknown".GetLocalized(),
    };
}

public sealed record TheaterAvatar(int AvatarId, string? IconUrl, int Level, int Rarity, string? Name, int AvatarType)
{
    public bool IsTrial => AvatarType == 2;


    public bool IsSupport => AvatarType == 3;


    public string TypeText => AvatarType switch
    {
        2 => "Theater_AvatarTrial".GetLocalized(),
        3 => "Theater_AvatarSupport".GetLocalized(),
        _ => string.Empty,
    };


    public bool HasTypeText => TypeText.Length > 0;


    public bool HasIcon => !string.IsNullOrWhiteSpace(IconUrl);


    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? "?" : Name!;
}

public sealed record TheaterEnemy(string? Name, string? IconUrl, int Level)
{
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? "?" : Name!;


    public string LevelText => Level > 0 ? $"Lv.{Level}" : string.Empty;


    public bool HasLevel => Level > 0;
}

public sealed record TheaterRound(
    int RoundId,
    bool IsGetMedal,
    bool IsTarot,
    string Title,
    string MedalIconPath,
    string FinishTimeText,
    string DurationText,
    string TarotIconPath,
    IReadOnlyList<TheaterAvatar> Avatars,
    IReadOnlyList<TheaterEnemy> Enemies,
    IReadOnlyList<TheaterBuff> ChoiceCards,
    string SplendourSummary,
    IReadOnlyList<TheaterSplendourBuff> SplendourBuffs)
{
    public bool HasMedal => IsGetMedal;


    public bool HasNoMedal => !IsGetMedal;


    public bool HasMedalIcon => !string.IsNullOrWhiteSpace(MedalIconPath);


    public bool HasTarotIcon => !string.IsNullOrWhiteSpace(TarotIconPath);


    public bool HasAvatars => Avatars.Count > 0;


    public bool HasEnemies => Enemies.Count > 0;


    public bool HasFinishTime => FinishTimeText.Length > 0;


    public bool HasDuration => DurationText.Length > 0;


    public bool HasChoiceCards => ChoiceCards.Count > 0;


    public bool HasSplendour => HasSplendourSummary || SplendourBuffs.Count > 0;


    public bool HasSplendourSummary => !string.IsNullOrWhiteSpace(SplendourSummary);
}

public sealed record TheaterBuff(string? IconUrl, string Name, string Description, bool IsEnhanced)
{
    public bool HasIcon => !string.IsNullOrWhiteSpace(IconUrl);


    public bool HasName => !string.IsNullOrWhiteSpace(Name);


    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);
}

public sealed record TheaterSplendourBuff(
    string? IconUrl,
    string Name,
    int Level,
    IReadOnlyList<string> Effects)
{
    public bool HasIcon => !string.IsNullOrWhiteSpace(IconUrl);


    public bool HasName => !string.IsNullOrWhiteSpace(Name);


    public bool HasLevel => Level > 0;


    public string LevelText => Level > 0 ? $"Lv.{Level}" : string.Empty;


    public bool HasEffects => Effects.Count > 0;
}

public sealed record TheaterAvatarDamage(int AvatarId, string? IconUrl, int Rarity, long Value)
{
    public string ValueText => Value > 0
        ? Value.ToString("N0", CultureInfo.InvariantCulture)
        : "--";
}

public sealed record TheaterBattleStats(
    string TotalTimeText,
    TheaterAvatarDamage? MaxDefeat,
    TheaterAvatarDamage? MaxDamage,
    TheaterAvatarDamage? MaxTakeDamage,
    IReadOnlyList<TheaterAvatarDamage> ShortestClear)
{
    public bool HasTotalTime => TotalTimeText.Length > 0;


    public bool HasMaxDefeat => MaxDefeat is not null;


    public bool HasMaxDamage => MaxDamage is not null;


    public bool HasMaxTakeDamage => MaxTakeDamage is not null;


    public bool HasShortestClear => ShortestClear.Count > 0;
}

public sealed record TheaterMedalIcon(string IconPath)
{
    public bool HasIcon => !string.IsNullOrWhiteSpace(IconPath);
}

public sealed record RoleCombatDisplay
{
    public TheaterDisplayState State
    {
        get;
        init;
    } = TheaterDisplayState.NotParticipated;


    public string ErrorMessage
    {
        get;
        init;
    } = string.Empty;


    public string PeriodText
    {
        get;
        init;
    } = string.Empty;


    public string DifficultyName
    {
        get;
        init;
    } = string.Empty;


    public string HeraldryIconPath
    {
        get;
        init;
    } = string.Empty;


    public string MaxRoundText
    {
        get;
        init;
    } = "--";


    public IReadOnlyList<TheaterMedalIcon> MedalIcons
    {
        get;
        init;
    } = [];


    public string CoinText
    {
        get;
        init;
    } = "--";


    public string AvatarBonusText
    {
        get;
        init;
    } = "--";


    public string RentText
    {
        get;
        init;
    } = "--";


    public IReadOnlyList<TheaterRound> Rounds
    {
        get;
        init;
    } = [];


    public IReadOnlyList<TheaterAvatar> BackupAvatars
    {
        get;
        init;
    } = [];


    public TheaterBattleStats? BattleStats
    {
        get;
        init;
    }


    public bool ShowContent => State == TheaterDisplayState.Content;


    public bool HasState => State != TheaterDisplayState.Content;


    public string StateTitle => State switch
    {
        TheaterDisplayState.NotUnlocked => "CycleChallenge_NotUnlocked".GetLocalized(),
        TheaterDisplayState.NotParticipated => "CycleChallenge_NotParticipated".GetLocalized(),
        TheaterDisplayState.NoDetail => "Theater_NoDetail".GetLocalized(),
        TheaterDisplayState.Error => "CycleChallenge_LoadFailed".GetLocalized(),
        _ => string.Empty,
    };


    public string StateDetail => State == TheaterDisplayState.Error ? ErrorMessage : string.Empty;


    public bool HasStateDetail => StateDetail.Length > 0;


    public bool HasPeriod => PeriodText.Length > 0;


    public bool HasHeraldryIcon => HeraldryIconPath.Length > 0;


    public bool HasRounds => Rounds.Count > 0;


    public bool HasNoRounds => Rounds.Count == 0;


    public bool HasMedalIcons => MedalIcons.Count > 0;


    public bool HasBackupAvatars => BackupAvatars.Count > 0;


    public bool ShowBattleStats => BattleStats is not null;


    public string BattleTotalTimeText => BattleStats?.TotalTimeText ?? string.Empty;


    public bool HasBattleTotalTime => BattleTotalTimeText.Length > 0;


    public TheaterAvatarDamage? MaxDefeat => BattleStats?.MaxDefeat;


    public string MaxDefeatValueText => MaxDefeat?.ValueText ?? "--";


    public string? MaxDefeatIconUrl => MaxDefeat?.IconUrl;


    public int MaxDefeatAvatarId => MaxDefeat?.AvatarId ?? 0;


    public int MaxDefeatRarity => MaxDefeat?.Rarity ?? 0;


    public bool HasMaxDefeat => MaxDefeat is not null;


    public TheaterAvatarDamage? MaxDamage => BattleStats?.MaxDamage;


    public string MaxDamageValueText => MaxDamage?.ValueText ?? "--";


    public string? MaxDamageIconUrl => MaxDamage?.IconUrl;


    public int MaxDamageAvatarId => MaxDamage?.AvatarId ?? 0;


    public int MaxDamageRarity => MaxDamage?.Rarity ?? 0;


    public bool HasMaxDamage => MaxDamage is not null;


    public TheaterAvatarDamage? MaxTakeDamage => BattleStats?.MaxTakeDamage;


    public string MaxTakeDamageValueText => MaxTakeDamage?.ValueText ?? "--";


    public string? MaxTakeDamageIconUrl => MaxTakeDamage?.IconUrl;


    public int MaxTakeDamageAvatarId => MaxTakeDamage?.AvatarId ?? 0;


    public int MaxTakeDamageRarity => MaxTakeDamage?.Rarity ?? 0;


    public bool HasMaxTakeDamage => MaxTakeDamage is not null;


    public IReadOnlyList<TheaterAvatarDamage> ShortestClear => BattleStats?.ShortestClear ?? [];


    public bool HasShortestClear => ShortestClear.Count > 0;


    public static RoleCombatDisplay CreateState(TheaterDisplayState state, string? detail = null) => new()
    {
        State = state,
        ErrorMessage = detail ?? string.Empty,
    };


    public static RoleCombatDisplay CreateError(string message) =>
        CreateState(TheaterDisplayState.Error, message);
}