/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using Microsoft.EntityFrameworkCore.Design;

namespace FufuLauncher.Data;

public class AchievementDbContextFactory : IDesignTimeDbContextFactory<AchievementDbContext>
{
    public AchievementDbContext CreateDbContext(string[] args)
    {
        var dbPath = Path.Combine(Path.GetTempPath(), "ef_design_achievements.db");
        return new AchievementDbContext(dbPath);
    }
}
