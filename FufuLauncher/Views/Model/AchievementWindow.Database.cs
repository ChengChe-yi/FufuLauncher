/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using FufuLauncher.Data.Entities;
using FufuLauncher.Helpers;
using FufuLauncher.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FufuLauncher.Views;

public sealed partial class AchievementWindow
{
    private void EnsureDatabaseExists(string dbPath)
    {
        bool isNewDb = !File.Exists(dbPath);

        // EF Core EnsureCreated handles table creation when repository is first used.
        // Trigger it by accessing the repository, which calls EnsureCreated on the DbContext.
        // No need for ad-hoc PRAGMA + ALTER TABLE migrations anymore.

        if (isNewDb)
        {
            string oldJsonPath = Path.Combine(Path.GetDirectoryName(dbPath)!, "achievements.json");
            if (File.Exists(oldJsonPath))
            {
                ImportJsonToDb(oldJsonPath, dbPath);
            }
            else if (File.Exists(_assetsFilePath))
            {
                ImportJsonToDb(_assetsFilePath, dbPath);
            }
        }
    }

    private void ImportJsonToDb(string jsonPath, string dbPath)
    {
        string jsonContent;
        try
        {
            jsonContent = File.ReadAllText(jsonPath);
        }
        catch (IOException ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[AchievementWindow] 读取成就文件失败 ({jsonPath}): {ex.Message}");
            return;
        }

        var options = new JsonSerializerOptions {
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            ReadCommentHandling = JsonCommentHandling.Skip,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        List<AchievementCategory> rawCategories;
        try
        {
            rawCategories = JsonSerializer.Deserialize<List<AchievementCategory>>(jsonContent, options);
        }
        catch (JsonException ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[AchievementWindow] 成就 JSON 解析失败 ({jsonPath}): {ex.Message}");
            return;
        }
        if (rawCategories == null) return;

        var writeOptions = new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

        var categoryEntries = rawCategories.Select(cat => (GetCategoryName(cat), cat.IconUrl)).ToList();
        _achievementRepo.InsertOrIgnoreCategories(categoryEntries);

        var achievements = new List<AchievementEntity>();
        foreach (var cat in rawCategories)
        {
            string categoryName = GetCategoryName(cat);
            if (cat.Achievements == null) continue;

            foreach (var item in cat.Achievements)
            {
                achievements.Add(new AchievementEntity
                {
                    Id = item.Id,
                    Title = item.Title ?? "",
                    CategoryName = categoryName,
                    RawJson = JsonSerializer.Serialize(item, writeOptions),
                    IsCompleted = item.IsCompleted ? 1 : 0,
                    CurrentProgress = item.CurrentProgress,
                    MaxProgress = item.MaxProgress,
                    CompletionTimestamp = 0
                });
            }
        }
        _achievementRepo.InsertAchievements(achievements);
    }

    private async Task SyncWithAssetsDatabase()
    {
        if (_isBatchProcessing) return;
        _isBatchProcessing = true;
        ViewModel.IsLoading = true;
        ViewModel.StatusMessage = "正在对比数据库版本...";

        try
        {
            if (!File.Exists(_assetsFilePath))
            {
                await ShowDialogAsync("ErrorTitle".GetLocalized(), "找不到内置数据库文件");
                return;
            }

            string masterJson = await File.ReadAllTextAsync(_assetsFilePath);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, NumberHandling = JsonNumberHandling.AllowReadingFromString, ReadCommentHandling = JsonCommentHandling.Skip, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var masterCategories = JsonSerializer.Deserialize<List<AchievementCategory>>(masterJson, options);

            EnsureDatabaseExists(_workFilePath);

            _achievementRepo.ChangeDatabase(_workFilePath);

            var existingIds = _achievementRepo.GetExistingAchievementIds();

            int addedCount = 0;
            int newCategoriesCount = 0;
            var writeOptions = new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

            var newCategories = new List<(string, string?)>();
            var newAchievements = new List<AchievementEntity>();

            foreach (var masterCat in masterCategories)
            {
                string categoryName = GetCategoryName(masterCat);
                newCategories.Add((categoryName, masterCat.IconUrl));

                if (masterCat.Achievements == null) continue;

                foreach (var item in masterCat.Achievements)
                {
                    if (!existingIds.Contains(item.Id))
                    {
                        newAchievements.Add(new AchievementEntity
                        {
                            Id = item.Id,
                            Title = item.Title ?? "",
                            CategoryName = categoryName,
                            RawJson = JsonSerializer.Serialize(item, writeOptions),
                            IsCompleted = item.IsCompleted ? 1 : 0,
                            CurrentProgress = item.CurrentProgress,
                            MaxProgress = item.MaxProgress,
                            CompletionTimestamp = 0
                        });
                        addedCount++;
                        existingIds.Add(item.Id);
                    }
                }
            }

            newCategoriesCount = _achievementRepo.InsertOrIgnoreCategories(newCategories);
            if (newAchievements.Count > 0)
                _achievementRepo.InsertAchievements(newAchievements);

            if (addedCount > 0)
            {
                LoadData();
                await ShowDialogAsync("数据库更新", $"同步成功！\n新增分类: {newCategoriesCount} 个\n新增成就: {addedCount} 个");
            }
            else
            {
                ViewModel.StatusMessage = "当前已是最新数据库";
                await ShowDialogAsync("数据库更新", "您的存档已经是最新版本，无需更新。");
            }
        }
        catch (Exception ex)
        {
            await ShowDialogAsync("更新失败", $"同步过程中发生错误：\n{ex.Message}");
        }
        finally
        {
            ViewModel.IsLoading = false;
            _isBatchProcessing = false;
            if (_isDataLoaded) CalculateGlobalStats();
        }
    }

    private string GetCategoryName(AchievementCategory cat)
    {
        var props = typeof(AchievementCategory).GetProperties();
        var titleProp = props.FirstOrDefault(p => p.Name.Equals("Title", StringComparison.OrdinalIgnoreCase));
        var nameProp = props.FirstOrDefault(p => p.Name.Equals("Name", StringComparison.OrdinalIgnoreCase));

        string name = null;
        if (titleProp != null) name = titleProp.GetValue(cat) as string;
        if (string.IsNullOrEmpty(name) && nameProp != null) name = nameProp.GetValue(cat) as string;

        return string.IsNullOrEmpty(name) ? "AchievementWindow_UnknownCategory".GetLocalized() : name;
    }

    private void SetCategoryTitle(AchievementCategory cat, string title)
    {
        var props = typeof(AchievementCategory).GetProperties();
        var titleProp = props.FirstOrDefault(p => p.Name.Equals("Title", StringComparison.OrdinalIgnoreCase));
        if (titleProp != null && titleProp.CanWrite)
        {
            titleProp.SetValue(cat, title);
        }
    }

    private async void OnUpdateDbClick(object sender, RoutedEventArgs e)
    {
        var confirmDialog = new ContentDialog
        {
            Title = "AchievementWindow_UpdateDb".GetLocalized(),
            Content = "此操作将读取软件内置的最新成就列表，并将缺失的新成就添加到您当前的存档中。\n\n您的现有进度（已完成的成就）将保留不会丢失。\n\n是否继续？",
            PrimaryButtonText = "AchievementWindow_StartUpdate".GetLocalized(),
            CloseButtonText = "CancelBtn".GetLocalized(),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };

        var result = await confirmDialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            await SyncWithAssetsDatabase();
        }
    }
}
