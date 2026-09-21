using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sockseek.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlaybackQueuePersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlaybackQueues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    CurrentIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    RepeatMode = table.Column<int>(type: "INTEGER", nullable: false),
                    ShuffleEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    ShuffleSeed = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlaybackQueues", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlaybackQueueItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false),
                    QueueId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    CanonicalTrackId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LocalMediaFileId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DownloadWorkflowId = table.Column<Guid>(type: "TEXT", nullable: true),
                    State = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlaybackQueueItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlaybackQueueItems_CanonicalTracks_CanonicalTrackId",
                        column: x => x.CanonicalTrackId,
                        principalTable: "CanonicalTracks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlaybackQueueItems_DownloadWorkflows_DownloadWorkflowId",
                        column: x => x.DownloadWorkflowId,
                        principalTable: "DownloadWorkflows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PlaybackQueueItems_LocalMediaFiles_LocalMediaFileId",
                        column: x => x.LocalMediaFileId,
                        principalTable: "LocalMediaFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PlaybackQueueItems_PlaybackQueues_QueueId",
                        column: x => x.QueueId,
                        principalTable: "PlaybackQueues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlaybackQueueItems_CanonicalTrackId",
                table: "PlaybackQueueItems",
                column: "CanonicalTrackId");

            migrationBuilder.CreateIndex(
                name: "IX_PlaybackQueueItems_DownloadWorkflowId",
                table: "PlaybackQueueItems",
                column: "DownloadWorkflowId");

            migrationBuilder.CreateIndex(
                name: "IX_PlaybackQueueItems_LocalMediaFileId",
                table: "PlaybackQueueItems",
                column: "LocalMediaFileId");

            migrationBuilder.CreateIndex(
                name: "IX_PlaybackQueueItems_QueueId_Position",
                table: "PlaybackQueueItems",
                columns: new[] { "QueueId", "Position" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlaybackQueueItems");

            migrationBuilder.DropTable(
                name: "PlaybackQueues");
        }
    }
}
