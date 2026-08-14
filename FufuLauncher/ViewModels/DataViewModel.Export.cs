/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Diagnostics;
using FufuLauncher.Helpers;
using FufuLauncher.Messages;
using FufuLauncher.Models.DataCenter;
using FufuLauncher.Services;

namespace FufuLauncher.ViewModels;

public sealed partial class DataViewModel
{
    #region PDF Export

    public async Task ExportPdfAsync(Microsoft.UI.Xaml.Window? owner)
    {
        if (!CanExportPdf) return;

        var path = await FilePickerService.PickSaveFileAsync(
            owner,
            new[] { (L("DataPage_ExportPdfFileType"), new[] { ".pdf" }) },
            $"FufuLauncher_DataCenter_{DateTime.Now:yyyyMMdd}",
            Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary,
            error => StatusMessage = LF("DataPage_ExportFailed", error));
        if (string.IsNullOrEmpty(path)) return;

        var previousStatus = StatusMessage;
        IsExporting = true;
        StatusMessage = L("DataPage_Exporting");

        try
        {
            var snapshot = CreateReportSnapshot(previousStatus);
            await _pdfReport.GenerateAsync(snapshot, path);
            StatusMessage = LF("DataPage_Exported", Path.GetFileName(path));
            _notificationService.Show(
                L("DataPage_ExportSuccessTitle"),
                StatusMessage,
                NotificationType.Success,
                4000);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DataViewModel] PDF 导出失败: {ex}");
            StatusMessage = LF("DataPage_ExportFailed", ex.Message);
            _notificationService.Show(
                L("DataPage_ExportFailedTitle"),
                StatusMessage,
                NotificationType.Error,
                6000);
        }
        finally
        {
            IsExporting = false;
            if (StatusMessage == L("DataPage_Exporting")) StatusMessage = previousStatus;
        }
    }

    private DataCenterReportSnapshot CreateReportSnapshot(string status)
    {
        return new DataCenterReportSnapshot(
            DateTimeOffset.Now,
            System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? Dash,
            DataSourceText,
            status,
            OverviewKpis.ToList(),
            OverviewInsights.ToList(),
            OverviewRisers.ToList(),
            OverviewFallers.ToList(),
            OverviewTopTier.ToList(),
            OverviewValuePicks.ToList(),
            OverviewBanners.ToList(),
            OverviewOverdue.ToList(),
            _allCharacters.ToList(),
            CreateAbyssSnapshot(Spiral),
            CreateAbyssSnapshot(Stygian),
            _allCharacterBanners.ToList(),
            _allWeaponBanners.ToList(),
            WishTopReruns.ToList(),
            WishTopCompanions.ToList(),
            _rerunGroups.Select(group => (IReadOnlyList<DcRerunCard>)group.ToList()).ToList());
    }

    private static DataCenterAbyssSnapshot CreateAbyssSnapshot(DcAbyssSection section) => new(
        section.Headline,
        section.LoadedVersion ?? Dash,
        section.Tips,
        section.ShowClearTime,
        section.Kpis.ToList(),
        section.Tiers.ToList(),
        section.Ranks.ToList(),
        section.AllTeams.ToList(),
        section.Risers.ToList(),
        section.Fallers.ToList(),
        section.RestartDistribution.ToList());

    #endregion
}
