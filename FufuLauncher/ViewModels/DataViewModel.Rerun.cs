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
    #region Rerun

    public ObservableCollection<DcRerunCard> RerunCards { get; } = new();
    public List<DcOption> RerunGroupOptions { get; }
    public List<DcOption> RerunSortOptions { get; }

    private int _rerunGroup;
    private string _rerunSort = "urgency";
    private string _rerunSearch = string.Empty;

    public string RerunCountText { get; private set; } = string.Empty;
    public bool RerunHasNone { get; private set; }

    public void SetRerunGroup(int group)
    {
        _rerunGroup = group;
        ApplyRerunFilter();
    }

    public void SetRerunSort(string? sort)
    {
        _rerunSort = string.IsNullOrEmpty(sort) ? "urgency" : sort;
        ApplyRerunFilter();
    }

    public void SearchRerun(string? keyword)
    {
        _rerunSearch = keyword?.Trim() ?? string.Empty;
        ApplyRerunFilter();
    }

    private void ApplyRerunFilter()
    {
        RerunCards.Clear();

        if (_rerunGroup >= 0 && _rerunGroup < _rerunGroups.Count)
        {
            IEnumerable<DcRerunCard> query = _rerunGroups[_rerunGroup];

            if (!string.IsNullOrEmpty(_rerunSearch))
            {
                query = query.Where(c => c.Name.Contains(_rerunSearch, StringComparison.OrdinalIgnoreCase));
            }

            query = _rerunSort switch
            {
                "days" => query.OrderByDescending(c => c.SortDays).ThenByDescending(c => c.SortUrgency),
                "interval" => query.OrderBy(c => c.SortInterval).ThenByDescending(c => c.SortUrgency),
                _ => query.OrderByDescending(c => c.SortUrgency).ThenByDescending(c => c.SortDays)
            };

            foreach (var card in query) RerunCards.Add(card);
        }

        RerunHasNone = RerunCards.Count == 0;
        RerunCountText = LF("DataPage_UnitEntries", RerunCards.Count);
        OnPropertyChanged(nameof(RerunHasNone));
        OnPropertyChanged(nameof(RerunCountText));
    }

    private void BuildRerun()
    {
        _rerunGroups.Clear();

        foreach (var group in _rerun?.Result ?? new List<List<RerunEntry>>())
        {
            var cards = new List<DcRerunCard>();

            foreach (var entry in group)
            {
                if (string.IsNullOrEmpty(entry.Name)) continue;

                var days = entry.Days ?? 0;
                var avg = entry.AvgDays ?? 0;
                var ratio = avg > 0 ? days / avg : 0;

                var (urgencyText, urgencyTag) = ratio switch
                {
                    >= 1.5 => (L("DataPage_RerunOverdue"), "overdue"),
                    >= 1.0 => (L("DataPage_RerunDue"), "due"),
                    >= 0.7 => (L("DataPage_RerunSoon"), "soon"),
                    > 0 => (L("DataPage_RerunFresh"), "fresh"),
                    _ => (L("DataPage_RerunNever"), "flat")
                };

                var forecast = avg > 0
                    ? days >= avg
                        ? LF("DataPage_RerunForecastOver", (days - avg).ToString("0", CultureInfo.InvariantCulture))
                        : LF("DataPage_RerunForecastLeft", (avg - days).ToString("0", CultureInfo.InvariantCulture))
                    : string.Empty;

                cards.Add(new DcRerunCard
                {
                    Name = entry.Name!,
                    Avatar = entry.Avatar,
                    Star = entry.Star ?? 5,
                    DaysText = Fmt(entry.Days, 0),
                    DaysCaption = L("DataPage_RerunDays"),
                    AvgDaysText = LF("DataPage_RerunDaysValue", Fmt(entry.AvgDays, 0)),
                    UpTimesText = LF("DataPage_RerunUpTimes", entry.UpTimes ?? 0),
                    MaxGapText = LF("DataPage_RerunMaxGap", Fmt(entry.MaxGapDays, 0), entry.MaxGapPool ?? Dash),
                    MinGapText = LF("DataPage_RerunMinGap", Fmt(entry.MinGapDays, 0), entry.MinGapPool ?? Dash),
                    UrgencyText = urgencyText,
                    UrgencyTag = urgencyTag,
                    ForecastText = forecast,
                    ProgressValue = Math.Clamp(ratio * 100, 0, 100),
                    SortUrgency = ratio,
                    SortDays = days,
                    SortInterval = avg > 0 ? avg : double.MaxValue,
                    History = (entry.History ?? new List<string>()).Where(h => !string.IsNullOrWhiteSpace(h)).ToList()
                });
            }

            _rerunGroups.Add(cards);
        }

        ApplyRerunFilter();
    }

    #endregion
}
