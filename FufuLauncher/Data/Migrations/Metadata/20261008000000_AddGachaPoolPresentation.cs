/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FufuLauncher.Data.Migrations.Metadata;

public partial class AddGachaPoolPresentation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "BannerImageUrl",
            table: "GachaPoolMetadata",
            type: "TEXT",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "PoolName",
            table: "GachaPoolMetadata",
            type: "TEXT",
            nullable: false,
            defaultValue: "");
    }


    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "BannerImageUrl",
            table: "GachaPoolMetadata");

        migrationBuilder.DropColumn(
            name: "PoolName",
            table: "GachaPoolMetadata");
    }
}