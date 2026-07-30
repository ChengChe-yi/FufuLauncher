/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using Microsoft.EntityFrameworkCore.Design;

namespace FufuLauncher.Data;

public class MetadataDbContextFactory : IDesignTimeDbContextFactory<MetadataDbContext>
{
    public MetadataDbContext CreateDbContext(string[] args)
    {
        var dbPath = Path.Combine(Path.GetTempPath(), "ef_design_metadata.db");
        return new MetadataDbContext(dbPath);
    }
}
