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
    #region Wish

    private List<DcWishBanner> _filteredBanners = new();

    public ObservableCollection<DcWishBanner> WishBanners { get; } = new();
    public ObservableCollection<DcCountRow> WishTopReruns { get; } = new();
    public ObservableCollection<DcCountRow> WishTopCompanions { get; } = new();

    private bool _wishShowWeapons;
    private string _wishSearch = string.Empty;
    private int _wishShown;

    public bool WishShowWeapons => _wishShowWeapons;
    public bool WishShowCharacters => !_wishShowWeapons;
    public string WishCountText { get; private set; } = string.Empty;
    public bool WishHasMore { get; private set; }
    public bool WishHasNone { get; private set; }
    public string WishMoreText { get; private set; } = string.Empty;

    public void SetWishCategory(bool weapons)
    {
        if (_wishShowWeapons == weapons) return;
        _wishShowWeapons = weapons;
        OnPropertyChanged(nameof(WishShowWeapons));
        OnPropertyChanged(nameof(WishShowCharacters));
        ApplyWishFilter();
    }

    public void SearchWish(string? keyword)
    {
        _wishSearch = keyword?.Trim() ?? string.Empty;
        ApplyWishFilter();
    }

    public void ShowMoreWish()
    {
        _wishShown = Math.Min(_wishShown + WishPageSize, _filteredBanners.Count);
        PushWishPage();
    }

    private void ApplyWishFilter()
    {
        IEnumerable<DcWishBanner> query = _wishShowWeapons ? _allWeaponBanners : _allCharacterBanners;

        if (!string.IsNullOrEmpty(_wishSearch))
        {
            query = query.Where(b => b.SearchKey.Contains(_wishSearch, StringComparison.OrdinalIgnoreCase));
        }

        _filteredBanners = query.ToList();
        _wishShown = Math.Min(WishPageSize, _filteredBanners.Count);
        PushWishPage();
    }

    private void PushWishPage()
    {
        WishBanners.Clear();
        for (var i = 0; i < _wishShown; i++) WishBanners.Add(_filteredBanners[i]);

        WishHasMore = _wishShown < _filteredBanners.Count;
        WishHasNone = _filteredBanners.Count == 0;
        WishCountText = LF("DataPage_ShownCount", _wishShown, _filteredBanners.Count);
        WishMoreText = LF("DataPage_ShowMore",
            Math.Min(WishPageSize, Math.Max(0, _filteredBanners.Count - _wishShown)));

        OnPropertyChanged(nameof(WishHasMore));
        OnPropertyChanged(nameof(WishHasNone));
        OnPropertyChanged(nameof(WishCountText));
        OnPropertyChanged(nameof(WishMoreText));
    }

    private sealed class WishCharacterStat
    {
        public int Count { get; set; }
        public string LatestVersion { get; set; } = Dash;
        public DateTime? LatestDate { get; set; }
        public double? DaysSince { get; set; }
        public double? AverageGap { get; set; }
        public List<string> Banners { get; } = new();
        public List<DateTime> Dates { get; } = new();
    }

    private Dictionary<string, WishCharacterStat> BuildWishStats()
    {
        var result = new Dictionary<string, WishCharacterStat>(StringComparer.Ordinal);
        if (_wish?.Characters == null) return result;

        foreach (var banner in _wish.Characters.AsEnumerable().Reverse())
        {
            var (start, _) = ParseRange(banner.Time);

            foreach (var name in banner.Star5 ?? new List<string>())
            {
                if (string.IsNullOrWhiteSpace(name)) continue;

                if (!result.TryGetValue(name, out var stat))
                {
                    stat = new WishCharacterStat();
                    result[name] = stat;
                }

                stat.Count++;
                stat.Banners.Insert(0, banner.Version ?? string.Empty);
                if (start.HasValue)
                {
                    stat.Dates.Add(start.Value);
                    stat.LatestDate = start;
                }

                stat.LatestVersion = banner.Version ?? stat.LatestVersion;
            }
        }

        var today = DateTime.Today;
        foreach (var stat in result.Values)
        {
            if (stat.LatestDate.HasValue) stat.DaysSince = (today - stat.LatestDate.Value).TotalDays;

            if (stat.Dates.Count >= 2)
            {
                var ordered = stat.Dates.OrderBy(d => d).ToList();
                var gaps = new List<double>();
                for (var i = 1; i < ordered.Count; i++) gaps.Add((ordered[i] - ordered[i - 1]).TotalDays);
                stat.AverageGap = gaps.Average();
            }
        }

        return result;
    }

    private void BuildWish()
    {
        _allCharacterBanners.Clear();
        _allWeaponBanners.Clear();
        WishTopReruns.Clear();
        WishTopCompanions.Clear();

        if (_wish == null)
        {
            ApplyWishFilter();
            return;
        }

        var icons = _wish.AvatarList ?? new Dictionary<string, string>();

        foreach (var banner in _wish.Characters ?? new List<WishBannerEntry>())
        {
            _allCharacterBanners.Add(ToBanner(banner, icons));
        }

        foreach (var banner in _wish.Weapons ?? new List<WishBannerEntry>())
        {
            _allWeaponBanners.Add(ToBanner(banner, icons));
        }

        var maxCount = _wishStats.Values.Select(s => s.Count).DefaultIfEmpty(1).Max();
        var position = 0;
        foreach (var pair in _wishStats.OrderByDescending(p => p.Value.Count).ThenBy(p => p.Key, StringComparer.Ordinal)
                     .Take(10))
        {
            position++;
            WishTopReruns.Add(new DcCountRow
            {
                Position = position,
                Name = pair.Key,
                Icon = icons.GetValueOrDefault(pair.Key),
                CountText = LF("DataPage_WishUpTimes", pair.Value.Count),
                DetailText = pair.Value.DaysSince.HasValue
                    ? LF("DataPage_WishDaysAgo", pair.Value.DaysSince.Value.ToString("0", CultureInfo.InvariantCulture))
                    : pair.Value.LatestVersion,
                Ratio = maxCount > 0 ? pair.Value.Count * 100d / maxCount : 0
            });
        }

        var companions = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var banner in _wish.Characters ?? new List<WishBannerEntry>())
        {
            foreach (var name in banner.Star4 ?? new List<string>())
            {
                if (string.IsNullOrWhiteSpace(name)) continue;
                companions[name] = companions.GetValueOrDefault(name) + 1;
            }
        }

        var maxCompanion = companions.Values.DefaultIfEmpty(1).Max();
        position = 0;
        foreach (var pair in companions.OrderByDescending(p => p.Value).ThenBy(p => p.Key, StringComparer.Ordinal)
                     .Take(10))
        {
            position++;
            WishTopCompanions.Add(new DcCountRow
            {
                Position = position,
                Name = pair.Key,
                Icon = icons.GetValueOrDefault(pair.Key),
                CountText = LF("DataPage_WishUpTimes", pair.Value),
                Ratio = maxCompanion > 0 ? pair.Value * 100d / maxCompanion : 0
            });
        }

        ApplyWishFilter();
    }

    private static DcWishBanner ToBanner(WishBannerEntry banner, Dictionary<string, string> icons)
    {
        var (start, end) = ParseRange(banner.Time);
        var today = DateTime.Today;

        var status = string.Empty;
        var statusTag = "flat";
        var relative = string.Empty;

        if (start.HasValue && end.HasValue)
        {
            if (today >= start.Value && today <= end.Value)
            {
                status = L("DataPage_WishOngoing");
                statusTag = "up";
                relative = LF("DataPage_WishDaysLeft",
                    Math.Max(0, (end.Value - today).TotalDays).ToString("0", CultureInfo.InvariantCulture));
            }
            else if (start.Value > today)
            {
                status = L("DataPage_WishUpcoming");
                statusTag = "accent";
                relative = LF("DataPage_WishDaysUntil",
                    (start.Value - today).TotalDays.ToString("0", CultureInfo.InvariantCulture));
            }
            else
            {
                relative = LF("DataPage_WishDaysAgo",
                    (today - start.Value).TotalDays.ToString("0", CultureInfo.InvariantCulture));
            }
        }

        var star5 = (banner.Star5 ?? new List<string>())
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => new DcNamedIcon { Name = n, Icon = icons.GetValueOrDefault(n), Star = 5 })
            .ToList();

        var star4 = (banner.Star4 ?? new List<string>())
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => new DcNamedIcon { Name = n, Icon = icons.GetValueOrDefault(n), Star = 4 })
            .ToList();

        return new DcWishBanner
        {
            Avatar = banner.Avatar,
            Version = banner.Version ?? string.Empty,
            TimeText = banner.Time ?? string.Empty,
            StatusText = status,
            StatusTag = statusTag,
            RelativeText = relative,
            Star5 = star5,
            Star4 = star4,
            SearchKey = string.Join(' ',
                new[] { banner.Version ?? string.Empty }
                    .Concat(star5.Select(s => s.Name))
                    .Concat(star4.Select(s => s.Name)))
        };
    }

    #endregion
}
