/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Diagnostics;
using FufuLauncher.Data.Entities;
using FufuLauncher.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FufuLauncher.Data.Repositories;

public class AchievementRepository
{
    private string? _overridePath;
    private string DbPath => _overridePath ?? Path.Combine(Helpers.AppPaths.DataDir, "achievements.db");

    public AchievementRepository() { }

    public void ChangeDatabase(string? newDbPath)
    {
        _overridePath = newDbPath;
    }
    
    public void InvalidateMigrationCache(string dbPath)
    {
        _migratedPaths.TryRemove(dbPath, out _);
    }
    
    public static void ClearConnectionPool()
    {
        SqliteConnection.ClearAllPools();
    }

    private static readonly object _migrateLock = new();
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, bool> _migratedPaths
        = new(StringComparer.OrdinalIgnoreCase);

    private AchievementDbContext CreateContext()
    {
        var dbPath = DbPath;
        if (!_migratedPaths.ContainsKey(dbPath))
        {
            lock (_migrateLock)
            {
                if (!_migratedPaths.ContainsKey(dbPath))
                {
                    PerformMigration(dbPath);
                    _migratedPaths[dbPath] = true;
                }
            }
        }
        return new AchievementDbContext(dbPath);
    }

    private void PerformMigration(string dbPath)
    {
        try
        {
            var dir = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            bool tableExists = false;
            try
            {
                using var checkConn = new SqliteConnection(SqlitePaths.BuildConnectionString(dbPath));
                checkConn.Open();
                using var checkCmd = checkConn.CreateCommand();
                checkCmd.CommandText =
                    "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Categories';";
                tableExists = (long)checkCmd.ExecuteScalar()! > 0;
            }
            catch { }

            if (tableExists)
            {
                using var context = new AchievementDbContext(dbPath);
                context.Database.ExecuteSqlRaw(
                    "CREATE TABLE IF NOT EXISTS __EFMigrationsHistory (MigrationId TEXT PRIMARY KEY, ProductVersion TEXT);");
                context.Database.ExecuteSqlRaw(
                    "INSERT OR IGNORE INTO __EFMigrationsHistory VALUES ('20240716000000_InitialCreate', '8.0.28');");
                Debug.WriteLine("AchievementRepository: 检测到现有数据库，已跳过迁移");
            }
            else
            {
                using var context = new AchievementDbContext(dbPath);
                context.Database.EnsureCreated();
                Debug.WriteLine("AchievementRepository: 已创建新数据库");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"AchievementRepository: 数据库迁移处理异常 - {ex.Message}");

            try
            {
                using var context = new AchievementDbContext(dbPath);
                context.Database.ExecuteSqlRaw(
                    "CREATE TABLE IF NOT EXISTS __EFMigrationsHistory (MigrationId TEXT PRIMARY KEY, ProductVersion TEXT);");
                context.Database.ExecuteSqlRaw(
                    "INSERT OR IGNORE INTO __EFMigrationsHistory VALUES ('20240716000000_InitialCreate', '8.0.28');");
            }
            catch (Exception ex2)
            {
                Debug.WriteLine($"AchievementRepository: 迁移历史回退创建失败 - {ex2.Message}");
            }
        }
    }

    // ---- Categories ----

    public List<AchievementCategoryEntity> GetAllCategories()
    {
        try
        {
            using var context = CreateContext();
            return context.Categories.ToList();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AchievementRepo] 加载分类失败: {ex.Message}");
            return new List<AchievementCategoryEntity>();
        }
    }

    public void InsertOrIgnoreCategory(string name, string? iconUrl)
    {
        using var context = CreateContext();
        if (!context.Categories.Any(c => c.Name == name))
        {
            context.Categories.Add(new AchievementCategoryEntity { Name = name, IconUrl = iconUrl });
            context.SaveChanges();
        }
    }

    public int InsertOrIgnoreCategories(IEnumerable<(string Name, string? IconUrl)> categories)
    {
        using var context = CreateContext();
        var existingNames = context.Categories.Select(c => c.Name).ToHashSet();
        int count = 0;
        foreach (var (name, iconUrl) in categories)
        {
            if (!existingNames.Contains(name))
            {
                context.Categories.Add(new AchievementCategoryEntity { Name = name, IconUrl = iconUrl });
                existingNames.Add(name);
                count++;
            }
        }
        context.SaveChanges();
        return count;
    }

    // ---- Achievements ----

    public List<AchievementEntity> GetAllAchievements()
    {
        try
        {
            using var context = CreateContext();
            return context.Achievements.ToList();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AchievementRepo] 加载成就失败: {ex.Message}");
            return new List<AchievementEntity>();
        }
    }

    public HashSet<int> GetExistingAchievementIds()
    {
        using var context = CreateContext();
        return context.Achievements.Select(a => a.Id).ToHashSet();
    }

    public void InsertAchievement(AchievementEntity achievement)
    {
        using var context = CreateContext();
        context.Achievements.Add(achievement);
        context.SaveChanges();
    }

    public void InsertAchievements(List<AchievementEntity> achievements)
    {
        using var context = CreateContext();
        context.Achievements.AddRange(achievements);
        context.SaveChanges();
    }

    public void UpdateAchievement(int uid, bool isCompleted, int currentProgress, int maxProgress, long completionTimestamp)
    {
        using var context = CreateContext();
        var entity = context.Achievements.Find(uid);
        if (entity != null)
        {
            entity.IsCompleted = isCompleted ? 1 : 0;
            entity.CurrentProgress = currentProgress;
            entity.MaxProgress = maxProgress;
            entity.CompletionTimestamp = completionTimestamp;
            context.SaveChanges();
        }
    }

    public void UpdateAchievementsBatch(Dictionary<int, (bool IsCompleted, int CurrentProgress, int MaxProgress, long CompletionTimestamp)> updates)
    {
        using var context = CreateContext();
        foreach (var (uid, (isCompleted, currentProgress, maxProgress, completionTimestamp)) in updates)
        {
            var entity = context.Achievements.Find(uid);
            if (entity != null)
            {
                entity.IsCompleted = isCompleted ? 1 : 0;
                entity.CurrentProgress = currentProgress;
                entity.MaxProgress = maxProgress;
                entity.CompletionTimestamp = completionTimestamp;
            }
        }
        context.SaveChanges();
    }
}
