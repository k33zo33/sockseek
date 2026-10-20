using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sockseek.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSoulseekShareRoots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SoulseekShareRoots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false),
                    Path = table.Column<string>(type: "TEXT", nullable: false),
                    VirtualName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false, collation: "NOCASE"),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SoulseekShareRoots", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SoulseekShareRoots_Path",
                table: "SoulseekShareRoots",
                column: "Path",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SoulseekShareRoots_VirtualName",
                table: "SoulseekShareRoots",
                column: "VirtualName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SoulseekShareRoots");
        }
    }
}
