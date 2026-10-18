using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sockseek.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSoulseekWishlist : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SoulseekWishlistEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false),
                    Query = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    IntervalMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SearchState = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    NextSearchAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    NextSearchAtUtcUnixMilliseconds = table.Column<long>(type: "INTEGER", nullable: true),
                    LastSearchStartedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LastSearchCompletedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ActiveWorkflowId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ActiveJobId = table.Column<Guid>(type: "TEXT", nullable: true),
                    LastResultCount = table.Column<int>(type: "INTEGER", nullable: true),
                    LastErrorCode = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SoulseekWishlistEntries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SoulseekWishlistEntries_ActiveJobId",
                table: "SoulseekWishlistEntries",
                column: "ActiveJobId");

            migrationBuilder.CreateIndex(
                name: "IX_SoulseekWishlistEntries_Enabled_SearchState_NextSearchAtUtcUnixMilliseconds",
                table: "SoulseekWishlistEntries",
                columns: new[] { "Enabled", "SearchState", "NextSearchAtUtcUnixMilliseconds" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SoulseekWishlistEntries");
        }
    }
}
