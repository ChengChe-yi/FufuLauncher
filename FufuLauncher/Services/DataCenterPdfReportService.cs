/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Collections.Concurrent;
using FufuLauncher.ViewModels;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FufuLauncher.Services;

public interface IDataCenterPdfReportService
{
    Task GenerateAsync(DataCenterReportSnapshot snapshot, string path, CancellationToken cancellationToken = default);
}

public sealed record DataCenterReportSnapshot(
    DateTimeOffset ExportedAt,
    string AppVersion,
    string DataSource,
    string Status,
    IReadOnlyList<DcKpiTile> OverviewKpis,
    IReadOnlyList<DcInsight> Insights,
    IReadOnlyList<DcMoverRow> Risers,
    IReadOnlyList<DcMoverRow> Fallers,
    IReadOnlyList<DcRankRow> TopTier,
    IReadOnlyList<DcCountRow> ValuePicks,
    IReadOnlyList<DcWishBanner> ActiveBanners,
    IReadOnlyList<DcRerunCard> OverdueReruns,
    IReadOnlyList<DcCharacterCard> Characters,
    DataCenterAbyssSnapshot Spiral,
    DataCenterAbyssSnapshot Stygian,
    IReadOnlyList<DcWishBanner> CharacterBanners,
    IReadOnlyList<DcWishBanner> WeaponBanners,
    IReadOnlyList<DcCountRow> TopReruns,
    IReadOnlyList<DcCountRow> TopCompanions,
    IReadOnlyList<IReadOnlyList<DcRerunCard>> RerunGroups);

public sealed record DataCenterAbyssSnapshot(
    string Title,
    string Version,
    string Tips,
    bool ShowClearTime,
    IReadOnlyList<DcKpiTile> Kpis,
    IReadOnlyList<DcTierGroup> Tiers,
    IReadOnlyList<DcRankRow> Ranks,
    IReadOnlyList<DcTeamCard> Teams,
    IReadOnlyList<DcMoverRow> Risers,
    IReadOnlyList<DcMoverRow> Fallers,
    IReadOnlyList<DcBar> RestartDistribution);

public sealed class DataCenterPdfReportService : IDataCenterPdfReportService
{
    private const string Navy = "172033";
    private const string Ink = "1D2638";
    private const string Accent = "4F8CFF";
    private const string Pale = "F2F6FC";
    private const string Muted = "64748B";
    private const string Positive = "168A52";
    private const string Negative = "C53E4D";

    private static readonly HttpClient ImageClient = new()
    {
        Timeout = TimeSpan.FromSeconds(8)
    };

    public async Task GenerateAsync(DataCenterReportSnapshot snapshot, string path, CancellationToken cancellationToken = default)
    {
        var images = await DownloadImagesAsync(snapshot, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        await Task.Run(() =>
        {
            Document.Create(document =>
            {
                document.Page(page => ComposeCover(page, snapshot, images));
                document.Page(page => ComposeOverview(page, snapshot, images));
                document.Page(page => ComposeCharacters(page, snapshot, images));
                document.Page(page => ComposeAbyss(page, snapshot.Spiral, "Spiral Abyss", images));
                document.Page(page => ComposeAbyss(page, snapshot.Stygian, "Stygian Onslaught", images));
                document.Page(page => ComposeWishes(page, snapshot, images));
                document.Page(page => ComposeMethodology(page, snapshot));
            }).GeneratePdf(path);
        }, cancellationToken);
    }

    private static async Task<ConcurrentDictionary<string, byte[]>> DownloadImagesAsync(
        DataCenterReportSnapshot snapshot, CancellationToken cancellationToken)
    {
        var urls = snapshot.Characters.OrderByDescending(x => x.MetaScore).Take(8).Select(x => x.Avatar)
            .Concat(snapshot.ActiveBanners.Take(4).Select(x => x.Avatar))
            .Concat(snapshot.CharacterBanners.Take(6).Select(x => x.Avatar))
            .Where(url => Uri.TryCreate(url, UriKind.Absolute, out _))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var result = new ConcurrentDictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

        await Parallel.ForEachAsync(urls, new ParallelOptions
        {
            MaxDegreeOfParallelism = 4,
            CancellationToken = cancellationToken
        }, async (url, token) =>
        {
            try
            {
                var bytes = await ImageClient.GetByteArrayAsync(url!, token);
                if (bytes.Length > 0) result[url!] = bytes;
            }
            catch (Exception)
            {
                // ignored
            }
        });

        return result;
    }

    private static void ComposeCover(PageDescriptor page, DataCenterReportSnapshot report,
        IReadOnlyDictionary<string, byte[]> images)
    {
        page.Size(PageSizes.A4);
        page.Margin(32);
        page.DefaultTextStyle(x => x.FontSize(9).FontColor(Ink).FontFamily("Segoe UI"));
        page.Header().ShowOnce().Height(156).Background(Navy).Padding(28).Column(column =>
        {
            column.Spacing(6);
            column.Item().Text("FufuLauncher").FontColor(Colors.White).FontSize(14).SemiBold().LetterSpacing(1.5f);
            column.Item().Text("DATA CENTER").FontColor(Colors.White).FontSize(31).Bold();
            column.Item().Text("COMPREHENSIVE ANALYSIS REPORT").FontColor("B8D4FF").FontSize(10).SemiBold().LetterSpacing(1.2f);
        });
        page.Content().PaddingTop(30).Column(column =>
        {
            column.Spacing(18);
            column.Item().Text("Server-wide statistics, translated into practical decisions.")
                .FontColor(Ink).FontSize(21).SemiBold();
            column.Item().Text("This report brings together character training, endgame performance, banner history and rerun timing into one professional, printable snapshot.")
                .FontColor(Muted).FontSize(11).LineHeight(1.45f);
            column.Item().Element(c => ComposeKpis(c, report.OverviewKpis));
            column.Item().PaddingTop(8).Border(1).BorderColor("D7E1F0").Background(Pale).Padding(16).Column(meta =>
            {
                meta.Spacing(5);
                meta.Item().Text("REPORT METADATA").FontSize(9).FontColor(Accent).SemiBold().LetterSpacing(1f);
                meta.Item().Text($"Exported: {report.ExportedAt.LocalDateTime:yyyy-MM-dd HH:mm}  ·  FufuLauncher {report.AppVersion}").FontSize(10).FontColor(Ink);
                meta.Item().Text($"Data snapshot: {EmptyAsDash(report.Status)}").FontSize(10).FontColor(Ink);
                meta.Item().Text($"Attribution: {EmptyAsDash(report.DataSource)}").FontSize(9).FontColor(Muted);
            });
            column.Item().PaddingTop(6).Text("Contents").FontSize(16).Bold().FontColor(Ink);
            column.Item().Text("01  Executive overview    02  Character intelligence    03  Spiral Abyss    04  Stygian Onslaught    05  Banners & reruns    06  Methodology")
                .FontSize(10).FontColor(Muted).LineHeight(1.45f);
        });
        page.Footer().ShowOnce().AlignCenter().Text("FufuLauncher · Data Center Analysis").FontSize(9).FontColor("8EA4C5");
    }

    private static void ComposeOverview(PageDescriptor page, DataCenterReportSnapshot report,
        IReadOnlyDictionary<string, byte[]> images)
    {
        ConfigurePage(page, report);
        page.Content().Column(column =>
        {
            SectionTitle(column, "Executive overview", "A concise view of the current environment and highest-impact decisions.");
            column.Item().Element(c => ComposeKpis(c, report.OverviewKpis));
            if (report.Insights.Count > 0)
            {
                column.Item().PaddingTop(14).Text("Analyst takeaways").FontSize(15).Bold().FontColor(Ink);
                column.Item().PaddingTop(6).Column(insights =>
                {
                    insights.Spacing(7);
                    foreach (var insight in report.Insights.Take(7))
                        insights.Item().BorderLeft(3).BorderColor(TagColor(insight.ColorTag)).PaddingLeft(10).Column(item =>
                        {
                            item.Item().Text(insight.Title).SemiBold().FontSize(10).FontColor(Ink);
                            item.Item().Text(insight.Body).FontSize(9).FontColor(Muted).LineHeight(1.25f);
                        });
                });
            }
            column.Item().PaddingTop(14).Row(row =>
            {
                row.RelativeItem().Element(c => ComposeRankTable(c, "Top tier this period", report.TopTier.Take(12).ToList(), false));
                row.ConstantItem(14);
                row.RelativeItem().Element(c => ComposeCountBars(c, "Worth pulling for", report.ValuePicks.Take(8).ToList()));
            });
            if (report.Risers.Count > 0 || report.Fallers.Count > 0)
            {
                column.Item().PaddingTop(14).Row(row =>
                {
                    row.RelativeItem().Element(c => ComposeMovers(c, "Rising usage", report.Risers));
                    row.ConstantItem(14);
                    row.RelativeItem().Element(c => ComposeMovers(c, "Falling usage", report.Fallers));
                });
            }
        });
    }

    private static void ComposeCharacters(PageDescriptor page, DataCenterReportSnapshot report,
        IReadOnlyDictionary<string, byte[]> images)
    {
        ConfigurePage(page, report);
        page.Content().Column(column =>
        {
            SectionTitle(column, "Character intelligence", $"{report.Characters.Count} tracked characters · latest-period meta score combines Abyss, Stygian, ownership and field share.");
            var highlights = report.Characters.OrderByDescending(c => c.MetaScore).Take(6).ToList();
            if (highlights.Count > 0)
            {
                column.Item().Text("High-impact profiles").FontSize(15).Bold().FontColor(Ink);
                column.Item().PaddingTop(6).Grid(grid =>
                {
                    grid.Columns(2);
                    foreach (var character in highlights)
                    {
                        grid.Item().Padding(3).Border(1).BorderColor("DCE5F2").Padding(9).Row(row =>
                        {
                            row.ConstantItem(42).Element(c => Avatar(c, character.Avatar, character.Name, images));
                            row.ConstantItem(8);
                            row.RelativeItem().Column(body =>
                            {
                                body.Item().Text($"{character.Name}  ·  {character.TierText}").SemiBold().FontSize(10).FontColor(Ink);
                                body.Item().Text($"Meta {character.MetaScoreText}  |  Abyss {character.AbyssRateText}  |  Stygian {character.StygianRateText}").FontSize(8).FontColor(Muted);
                                body.Item().Text($"Build: {JoinRates(character.TopWeapons)}").FontSize(8).FontColor(Muted);
                                if (character.HasHeadline) body.Item().Text(character.HeadlineText).FontSize(8).FontColor(TagColor(character.HeadlineTag));
                            });
                        });
                    }
                });
            }
            column.Item().PaddingTop(14).Element(c => ComposeCharacterTable(c, report.Characters.OrderByDescending(x => x.MetaScore).ToList()));
        });
    }

    private static void ComposeAbyss(PageDescriptor page, DataCenterAbyssSnapshot board, string fallbackTitle,
        IReadOnlyDictionary<string, byte[]> images)
    {
        ConfigurePage(page, null);
        page.Content().Column(column =>
        {
            var title = string.IsNullOrWhiteSpace(board.Title) ? fallbackTitle : board.Title;
            SectionTitle(column, title, $"Selected period: {EmptyAsDash(board.Version)} · {EmptyAsDash(board.Tips)}");
            column.Item().Element(c => ComposeKpis(c, board.Kpis));
            if (board.RestartDistribution.Count > 0)
            {
                column.Item().PaddingTop(12).Element(c => ComposeBars(c, "Retry count distribution", board.RestartDistribution.Take(8).ToList()));
            }
            column.Item().PaddingTop(12).Row(row =>
            {
                row.RelativeItem().Element(c => ComposeTierGroups(c, "Tier landscape", board.Tiers));
                row.ConstantItem(14);
                row.RelativeItem().Element(c => ComposeMovers(c, "Usage movement", board.Risers.Concat(board.Fallers).Take(8).ToList()));
            });
            column.Item().PaddingTop(12).Element(c => ComposeRankTable(c, "Character rankings", board.Ranks, board.ShowClearTime));
            if (board.Teams.Count > 0)
            {
                column.Item().PaddingTop(12).Element(c => ComposeTeamTable(c, "Popular team compositions", board.Teams, board.ShowClearTime));
            }
        });
    }

    private static void ComposeWishes(PageDescriptor page, DataCenterReportSnapshot report,
        IReadOnlyDictionary<string, byte[]> images)
    {
        ConfigurePage(page, report);
        page.Content().Column(column =>
        {
            SectionTitle(column, "Banner & rerun intelligence", "Live availability, historical banner appearances and interval-based rerun forecasts.");
            if (report.ActiveBanners.Count > 0)
            {
                column.Item().Text("Available now / upcoming").FontSize(15).Bold().FontColor(Ink);
                column.Item().PaddingTop(6).Row(row =>
                {
                    foreach (var banner in report.ActiveBanners.Take(4))
                    {
                        row.RelativeItem().Padding(3).Border(1).BorderColor("DCE5F2").Padding(7).Column(card =>
                        {
                            card.Item().Height(48).Element(c => BannerImage(c, banner.Avatar, images));
                            card.Item().PaddingTop(5).Text(banner.Version).SemiBold().FontSize(9).FontColor(Ink);
                            card.Item().Text($"{banner.StatusText} · {banner.RelativeText}").FontSize(8).FontColor(TagColor(banner.StatusTag));
                            card.Item().Text(string.Join(" · ", banner.Star5.Select(x => x.Name))).FontSize(8).FontColor(Muted);
                        });
                    }
                });
            }
            column.Item().PaddingTop(12).Row(row =>
            {
                row.RelativeItem().Element(c => ComposeCountBars(c, "Most reruns", report.TopReruns));
                row.ConstantItem(14);
                row.RelativeItem().Element(c => ComposeCountBars(c, "Frequent 4-star features", report.TopCompanions));
            });
            column.Item().PaddingTop(12).Element(c => ComposeRerunTable(c, "Rerun watch", report.RerunGroups.SelectMany(x => x).OrderByDescending(x => x.SortUrgency).ToList()));
            column.Item().PaddingTop(12).Element(c => ComposeBannerTable(c, "Character banner history", report.CharacterBanners));
            if (report.WeaponBanners.Count > 0)
                column.Item().PaddingTop(12).Element(c => ComposeBannerTable(c, "Weapon banner history", report.WeaponBanners));
        });
    }

    private static void ComposeMethodology(PageDescriptor page, DataCenterReportSnapshot report)
    {
        ConfigurePage(page, report);
        page.Content().Column(column =>
        {
            SectionTitle(column, "Methodology & source notes", "How to read this report responsibly.");
            var items = new[]
            {
                ("Data source", EmptyAsDash(report.DataSource)),
                ("Snapshot time", report.ExportedAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm")),
                ("Coverage", "Character training averages, Spiral Abyss and Stygian performance, wish history and rerun intervals were combined where available."),
                ("Meta score", "The character meta score weighs Abyss pick rate (40%), Stygian pick rate (25%), ownership-adjusted field share (20%), and ownership (15%). It is a descriptive popularity/performance signal, not a guarantee of individual account results."),
                ("Period handling", "Character scores use the latest available endgame data. Abyss and Stygian chapters use the periods selected in the Data Center at export time."),
                ("Images", "Avatar and banner artwork are included when the public source was reachable during report creation. An unavailable image does not change the numerical analysis."),
                ("Disclaimer", "These are community-provided aggregate statistics. Game balance, banners and datasets can change without notice; treat advice as analytical context rather than official information.")
            };
            foreach (var (label, body) in items)
            {
                column.Item().PaddingBottom(10).BorderLeft(3).BorderColor(Accent).PaddingLeft(12).Column(item =>
                {
                    item.Item().Text(label).SemiBold().FontSize(11).FontColor(Ink);
                    item.Item().Text(body).FontSize(9).FontColor(Muted).LineHeight(1.35f);
                });
            }
            column.Item().PaddingTop(20).AlignCenter().Text("Prepared by FufuLauncher · Data Center").FontSize(11).SemiBold().FontColor(Navy);
        });
    }

    private static void ConfigurePage(PageDescriptor page, DataCenterReportSnapshot? report)
    {
        page.Size(PageSizes.A4);
        page.Margin(32);
        page.DefaultTextStyle(x => x.FontSize(9).FontColor(Ink).FontFamily("Segoe UI"));
        page.Header().Element(c => Header(c));
        page.Footer().Element(c => Footer(c, report));
    }

    private static void Header(IContainer container) => container.PaddingBottom(10).BorderBottom(1).BorderColor("DCE5F2").Row(row =>
    {
        row.RelativeItem().Text("FufuLauncher · DATA CENTER").FontSize(9).SemiBold().FontColor(Navy).LetterSpacing(0.8f);
        row.RelativeItem().AlignRight().Text("COMPREHENSIVE ANALYSIS REPORT").FontSize(8).FontColor(Muted).LetterSpacing(0.7f);
    });

    private static void Footer(IContainer container, DataCenterReportSnapshot? report) => container.PaddingTop(8).BorderTop(1).BorderColor("DCE5F2").AlignCenter().Text(text =>
    {
        text.Span(report == null ? "FufuLauncher" : $"FufuLauncher · {report.ExportedAt:yyyy-MM-dd} · ").FontSize(8).FontColor(Muted);
        text.Span("Page ").FontSize(8).FontColor(Muted);
        text.CurrentPageNumber().FontSize(8).FontColor(Muted);
        text.Span(" / ").FontSize(8).FontColor(Muted);
        text.TotalPages().FontSize(8).FontColor(Muted);
    });

    private static void SectionTitle(ColumnDescriptor column, string title, string subtitle)
    {
        column.Item().Text(title).FontSize(22).Bold().FontColor(Navy);
        column.Item().PaddingTop(3).Text(subtitle).FontSize(9).FontColor(Muted).LineHeight(1.3f);
        column.Item().PaddingTop(12).Height(3).Background(Accent);
        column.Item().PaddingBottom(10);
    }

    private static void ComposeKpis(IContainer container, IReadOnlyList<DcKpiTile> kpis) => container.Grid(grid =>
    {
        grid.Columns(Math.Min(Math.Max(kpis.Count, 1), 3));
        foreach (var kpi in kpis)
            grid.Item().Padding(3).Border(1).BorderColor("DCE5F2").Background(Pale).Padding(10).Column(card =>
            {
                card.Item().Text(kpi.Title).FontSize(8).FontColor(Muted);
                card.Item().Text(kpi.Value).FontSize(17).Bold().FontColor(TagColor(kpi.ColorTag));
                if (kpi.HasCaption) card.Item().Text(kpi.Caption).FontSize(7).FontColor(Muted);
            });
    });

    private static void ComposeRankTable(IContainer container, string title, IReadOnlyList<DcRankRow> ranks, bool showTime)
    {
        container.Column(column =>
        {
            TableTitle(column.Item(), title, ranks.Count);
            column.Item().Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(22); columns.RelativeColumn(2.6f); columns.RelativeColumn(); columns.RelativeColumn(); columns.RelativeColumn();
                if (showTime) columns.RelativeColumn();
                columns.RelativeColumn();
            });
            HeaderCell(table, "#"); HeaderCell(table, "Character"); HeaderCell(table, "Use"); HeaderCell(table, "Own"); HeaderCell(table, "Tier");
            if (showTime) HeaderCell(table, "Time");
            HeaderCell(table, "Δ");
            foreach (var rank in ranks)
            {
                Cell(table, rank.PositionText); Cell(table, rank.Name); Cell(table, rank.UseRateText); Cell(table, rank.OwnRateText); Cell(table, rank.TierText);
                if (showTime) Cell(table, rank.ClearTimeText);
                Cell(table, rank.ChangeText, TagColor(rank.ChangeTag));
            }
            });
        });
    }

    private static void ComposeCharacterTable(IContainer container, IReadOnlyList<DcCharacterCard> characters)
    {
        container.Column(column =>
        {
            TableTitle(column.Item(), "Complete character roster", characters.Count);
            column.Item().Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(22); columns.RelativeColumn(2.2f); columns.RelativeColumn(); columns.RelativeColumn(); columns.RelativeColumn(); columns.RelativeColumn(1.4f); columns.RelativeColumn(2.1f);
            });
            HeaderCell(table, "#"); HeaderCell(table, "Character"); HeaderCell(table, "Meta"); HeaderCell(table, "Abyss"); HeaderCell(table, "Own"); HeaderCell(table, "Tier"); HeaderCell(table, "Recommended weapon");
            var index = 0;
            foreach (var item in characters)
            {
                index++;
                Cell(table, index.ToString()); Cell(table, item.Name); Cell(table, item.MetaScoreText, TagColor(item.TierTag)); Cell(table, item.AbyssRateText); Cell(table, item.OwnRateText); Cell(table, item.TierText); Cell(table, JoinRates(item.TopWeapons));
            }
            });
        });
    }

    private static void ComposeTeamTable(IContainer container, string title, IReadOnlyList<DcTeamCard> teams, bool showTime)
    {
        container.Column(column =>
        {
            TableTitle(column.Item(), title, teams.Count);
            column.Item().Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(24); columns.RelativeColumn(3); columns.RelativeColumn(); columns.RelativeColumn(); columns.RelativeColumn();
                if (showTime) columns.RelativeColumn();
            });
            HeaderCell(table, "#"); HeaderCell(table, "Team"); HeaderCell(table, "Use"); HeaderCell(table, "Buildable"); HeaderCell(table, "Attendance");
            if (showTime) HeaderCell(table, "Time");
            foreach (var team in teams)
            {
                Cell(table, team.PositionText); Cell(table, team.TeamNames); Cell(table, team.UseRateText); Cell(table, team.HasRateText); Cell(table, team.AttendRateText);
                if (showTime) Cell(table, team.ClearTimeText);
            }
            });
        });
    }

    private static void ComposeRerunTable(IContainer container, string title, IReadOnlyList<DcRerunCard> cards)
    {
        container.Column(column =>
        {
            TableTitle(column.Item(), title, cards.Count);
            column.Item().Table(table =>
        {
            table.ColumnsDefinition(columns => { columns.RelativeColumn(2.3f); columns.RelativeColumn(); columns.RelativeColumn(); columns.RelativeColumn(1.2f); columns.RelativeColumn(2.2f); });
            HeaderCell(table, "Item"); HeaderCell(table, "Days waiting"); HeaderCell(table, "Average"); HeaderCell(table, "Status"); HeaderCell(table, "Forecast");
            foreach (var card in cards)
            {
                Cell(table, card.Name); Cell(table, card.DaysText); Cell(table, card.AvgDaysText); Cell(table, card.UrgencyText, TagColor(card.UrgencyTag)); Cell(table, card.ForecastText);
            }
            });
        });
    }

    private static void ComposeBannerTable(IContainer container, string title, IReadOnlyList<DcWishBanner> banners)
    {
        container.Column(column =>
        {
            TableTitle(column.Item(), title, banners.Count);
            column.Item().Table(table =>
        {
            table.ColumnsDefinition(columns => { columns.RelativeColumn(); columns.RelativeColumn(2); columns.RelativeColumn(2.6f); columns.RelativeColumn(2.6f); });
            HeaderCell(table, "Version"); HeaderCell(table, "Schedule"); HeaderCell(table, "5-star features"); HeaderCell(table, "4-star features");
            foreach (var banner in banners)
            {
                Cell(table, banner.Version); Cell(table, banner.TimeText); Cell(table, string.Join(" · ", banner.Star5.Select(x => x.Name))); Cell(table, string.Join(" · ", banner.Star4.Select(x => x.Name)));
            }
            });
        });
    }

    private static void ComposeMovers(IContainer container, string title, IReadOnlyList<DcMoverRow> movers)
    {
        container.Border(1).BorderColor("DCE5F2").Padding(9).Column(column =>
        {
            column.Item().Text(title).FontSize(11).SemiBold().FontColor(Ink);
            if (movers.Count == 0) column.Item().PaddingTop(5).Text("No comparable movement data available.").FontSize(8).FontColor(Muted);
            foreach (var item in movers.Take(8))
                column.Item().PaddingTop(5).Row(row =>
                {
                    row.RelativeItem().Text(item.Name).FontSize(8).FontColor(Ink);
                    row.RelativeItem().AlignRight().Text($"{item.PreviousText} → {item.CurrentText}  {item.ChangeText}").FontSize(8).FontColor(TagColor(item.ChangeTag));
                });
        });
    }

    private static void ComposeCountBars(IContainer container, string title, IReadOnlyList<DcCountRow> rows)
    {
        container.Border(1).BorderColor("DCE5F2").Padding(9).Column(column =>
        {
            column.Item().Text(title).FontSize(11).SemiBold().FontColor(Ink);
            foreach (var row in rows.Take(10))
            {
                column.Item().PaddingTop(6).Text($"{row.PositionText}. {row.Name}  ·  {row.CountText}").FontSize(8).FontColor(Ink);
                column.Item().Height(4).Background("DFE8F5").Row(bar =>
                {
                    var ratio = Math.Clamp(row.Ratio, 0, 100);
                    if (ratio > 0) bar.RelativeItem((float)ratio).Background(Accent);
                    if (ratio < 100) bar.RelativeItem((float)(100 - ratio));
                });
                if (!string.IsNullOrEmpty(row.DetailText)) column.Item().Text(row.DetailText).FontSize(7).FontColor(Muted);
            }
        });
    }

    private static void ComposeBars(IContainer container, string title, IReadOnlyList<DcBar> bars)
    {
        container.Border(1).BorderColor("DCE5F2").Padding(9).Column(column =>
        {
            column.Item().Text(title).FontSize(11).SemiBold().FontColor(Ink);
            foreach (var bar in bars)
                column.Item().PaddingTop(5).Row(row =>
                {
                    row.ConstantItem(90).Text(bar.Label).FontSize(8).FontColor(Muted);
                    row.RelativeItem().Height(9).Background("DFE8F5").Row(fill =>
                    {
                        var ratio = Math.Clamp(bar.Value, 0, 100);
                        if (ratio > 0) fill.RelativeItem((float)ratio).Background(TagColor(bar.ColorTag));
                        if (ratio < 100) fill.RelativeItem((float)(100 - ratio));
                    });
                    row.ConstantItem(36).AlignRight().Text(bar.ValueText).FontSize(8).FontColor(Ink);
                });
        });
    }

    private static void ComposeTierGroups(IContainer container, string title, IReadOnlyList<DcTierGroup> groups)
    {
        container.Border(1).BorderColor("DCE5F2").Padding(9).Column(column =>
        {
            column.Item().Text(title).FontSize(11).SemiBold().FontColor(Ink);
            foreach (var group in groups)
                column.Item().PaddingTop(6).Column(item =>
                {
                    item.Item().Text($"{group.RankName} · {group.CountText}").FontSize(9).SemiBold().FontColor(TagColor(group.TierTag));
                    item.Item().Text(string.Join(" · ", group.Members.Take(12).Select(m => $"{m.Name} ({m.UseRateText})"))).FontSize(7).FontColor(Muted).LineHeight(1.2f);
                });
        });
    }

    private static void Avatar(IContainer container, string? url, string fallback, IReadOnlyDictionary<string, byte[]> images)
    {
        if (!string.IsNullOrEmpty(url) && images.TryGetValue(url, out var bytes))
            container.Border(1).BorderColor("DCE5F2").Image(bytes).FitArea();
        else
            container.Border(1).BorderColor("DCE5F2").Background(Pale).AlignCenter().AlignMiddle().Text(fallback[..Math.Min(1, fallback.Length)]).FontSize(14).SemiBold().FontColor(Accent);
    }

    private static void BannerImage(IContainer container, string? url, IReadOnlyDictionary<string, byte[]> images)
    {
        if (!string.IsNullOrEmpty(url) && images.TryGetValue(url, out var bytes)) container.Image(bytes).FitArea();
        else container.Background(Pale).AlignCenter().AlignMiddle().Text("FufuLauncher").FontSize(7).FontColor(Muted);
    }

    private static void TableTitle(IContainer container, string title, int count) => container.PaddingBottom(5).Row(row =>
    {
        row.RelativeItem().Text(title).FontSize(15).Bold().FontColor(Ink);
        row.AutoItem().AlignRight().Text($"{count} entries").FontSize(8).FontColor(Muted);
    });

    private static void HeaderCell(TableDescriptor table, string text) => table.Cell().Background(Navy).PaddingVertical(5).PaddingHorizontal(4).Text(text).FontSize(7).SemiBold().FontColor(Colors.White);
    private static void Cell(TableDescriptor table, string text, string? color = null) => table.Cell().BorderBottom(1).BorderColor("E4EBF4").PaddingVertical(3).PaddingHorizontal(4).Text(EmptyAsDash(text)).FontSize(7).FontColor(color ?? Ink);

    private static string JoinRates(IEnumerable<DcRateRow> rows) => string.Join(" · ", rows.Take(2).Select(x => $"{x.Name} {x.RateText}"));
    private static string EmptyAsDash(string? value) => string.IsNullOrWhiteSpace(value) ? "—" : value;
    private static string TagColor(string? tag) => tag?.ToLowerInvariant() switch
    {
        "up" or "s1" or "overdue" => Positive,
        "down" => Negative,
        "due" or "s" => "C87915",
        "soon" or "a" => Accent,
        "b" => "397BC7",
        _ => Accent
    };
}
