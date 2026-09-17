using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sockseek.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLocalLibrarySearchIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_LocalMediaFiles_Availability_CanonicalTrackId",
                table: "LocalMediaFiles",
                columns: new[] { "Availability", "CanonicalTrackId" });

            migrationBuilder.CreateIndex(
                name: "IX_CanonicalTracks_NormalizedArtist_NormalizedTitle",
                table: "CanonicalTracks",
                columns: new[] { "NormalizedArtist", "NormalizedTitle" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LocalMediaFiles_Availability_CanonicalTrackId",
                table: "LocalMediaFiles");

            migrationBuilder.DropIndex(
                name: "IX_CanonicalTracks_NormalizedArtist_NormalizedTitle",
                table: "CanonicalTracks");
        }
    }
}
