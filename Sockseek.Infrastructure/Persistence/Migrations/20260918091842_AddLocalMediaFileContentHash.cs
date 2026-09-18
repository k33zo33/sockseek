using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sockseek.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLocalMediaFileContentHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContentHash",
                table: "LocalMediaFiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContentHashAlgorithm",
                table: "LocalMediaFiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ContentHashComputedAtUtc",
                table: "LocalMediaFiles",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContentHash",
                table: "LocalMediaFiles");

            migrationBuilder.DropColumn(
                name: "ContentHashAlgorithm",
                table: "LocalMediaFiles");

            migrationBuilder.DropColumn(
                name: "ContentHashComputedAtUtc",
                table: "LocalMediaFiles");
        }
    }
}
