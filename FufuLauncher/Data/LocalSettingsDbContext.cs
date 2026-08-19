/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using FufuLauncher.Data.Entities;
using FufuLauncher.Helpers;
using Microsoft.EntityFrameworkCore;

namespace FufuLauncher.Data;

public class LocalSettingsDbContext : DbContext
{
    private readonly string _dbPath;

    public DbSet<SettingEntity> Settings => Set<SettingEntity>();

    public LocalSettingsDbContext(string dbPath)
    {
        _dbPath = dbPath;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite(SqlitePaths.BuildConnectionString(_dbPath));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SettingEntity>(entity =>
        {
            entity.HasKey(e => e.Key);
        });
    }
}
