/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using FufuLauncher.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FufuLauncher.Data;

/// <summary>
/// 周期挑战独立数据库（不与其他 DbContext 共用文件与迁移）。
/// 建表与版本迁移由 <see cref="Repositories.CycleChallengeRepository"/> 负责。
/// </summary>
public class CycleChallengeDbContext : DbContext
{
    private readonly string _dbPath;

    public CycleChallengeDbContext(string dbPath)
    {
        _dbPath = dbPath;
    }

    public DbSet<CycleChallengeRecordEntity> CycleChallengeRecords => Set<CycleChallengeRecordEntity>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite($"Data Source={_dbPath};Pooling=False");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CycleChallengeRecordEntity>(entity =>
        {
            entity.ToTable("CycleChallengeRecord");
            entity.HasKey(e => new { e.Uid, e.Mode, e.ScheduleId });
        });
    }
}
