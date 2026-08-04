using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sockseek.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSqlite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Active = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppSettings",
                columns: table => new
                {
                    Key = table.Column<string>(type: "TEXT", nullable: false),
                    JsonValue = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSettings", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "CanonicalTracks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Artist = table.Column<string>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    DurationMs = table.Column<int>(type: "INTEGER", nullable: true),
                    Isrc = table.Column<string>(type: "TEXT", nullable: true),
                    MusicBrainzRecordingId = table.Column<string>(type: "TEXT", nullable: true),
                    NormalizedArtist = table.Column<string>(type: "TEXT", nullable: false),
                    NormalizedTitle = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CanonicalTracks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExternalAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Provider = table.Column<int>(type: "INTEGER", nullable: false),
                    ExternalUserId = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: false),
                    SecretReference = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    LastAuthorizedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProviderSyncStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Provider = table.Column<int>(type: "INTEGER", nullable: false),
                    AccountId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ResourceId = table.Column<string>(type: "TEXT", nullable: false),
                    Cursor = table.Column<string>(type: "TEXT", nullable: true),
                    ETag = table.Column<string>(type: "TEXT", nullable: true),
                    LastSuccessUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LastError = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderSyncStates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SchemaInfo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ApplicationVersion = table.Column<string>(type: "TEXT", nullable: false),
                    MigrationVersion = table.Column<string>(type: "TEXT", nullable: false),
                    LastBackupUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchemaInfo", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LocalMediaFiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CanonicalTrackId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Path = table.Column<string>(type: "TEXT", nullable: false),
                    Size = table.Column<long>(type: "INTEGER", nullable: false),
                    LastWriteUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    DurationMs = table.Column<int>(type: "INTEGER", nullable: true),
                    Codec = table.Column<string>(type: "TEXT", nullable: true),
                    Bitrate = table.Column<int>(type: "INTEGER", nullable: true),
                    SampleRate = table.Column<int>(type: "INTEGER", nullable: true),
                    BitDepth = table.Column<int>(type: "INTEGER", nullable: true),
                    Availability = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocalMediaFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LocalMediaFiles_CanonicalTracks_CanonicalTrackId",
                        column: x => x.CanonicalTrackId,
                        principalTable: "CanonicalTracks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "TrackSources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CanonicalTrackId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Provider = table.Column<int>(type: "INTEGER", nullable: false),
                    ExternalId = table.Column<string>(type: "TEXT", nullable: false),
                    ExternalUrl = table.Column<string>(type: "TEXT", nullable: true),
                    RawMetadataJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrackSources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrackSources_CanonicalTracks_CanonicalTrackId",
                        column: x => x.CanonicalTrackId,
                        principalTable: "CanonicalTracks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExternalPlaylists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AccountId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Provider = table.Column<int>(type: "INTEGER", nullable: false),
                    ExternalId = table.Column<string>(type: "TEXT", nullable: false),
                    Url = table.Column<string>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    SnapshotVersion = table.Column<long>(type: "INTEGER", nullable: false),
                    LastSyncedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalPlaylists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalPlaylists_ExternalAccounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "ExternalAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Playlists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    ImportMode = table.Column<int>(type: "INTEGER", nullable: false),
                    ExternalPlaylistId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Playlists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Playlists_ExternalPlaylists_ExternalPlaylistId",
                        column: x => x.ExternalPlaylistId,
                        principalTable: "ExternalPlaylists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "PlaylistItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PlaylistId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    ProviderItemId = table.Column<string>(type: "TEXT", nullable: false),
                    CanonicalTrackId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    SnapshotJson = table.Column<string>(type: "TEXT", nullable: false),
                    RemovedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlaylistItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlaylistItems_CanonicalTracks_CanonicalTrackId",
                        column: x => x.CanonicalTrackId,
                        principalTable: "CanonicalTracks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PlaylistItems_Playlists_PlaylistId",
                        column: x => x.PlaylistId,
                        principalTable: "Playlists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DownloadWorkflows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EngineJobId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PlaylistItemId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    OutputPath = table.Column<string>(type: "TEXT", nullable: true),
                    CandidateJson = table.Column<string>(type: "TEXT", nullable: true),
                    ErrorCode = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DownloadWorkflows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DownloadWorkflows_PlaylistItems_PlaylistItemId",
                        column: x => x.PlaylistItemId,
                        principalTable: "PlaylistItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ResolutionAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PlaylistItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CandidateTrackId = table.Column<Guid>(type: "TEXT", nullable: true),
                    EngineJobId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Method = table.Column<int>(type: "INTEGER", nullable: false),
                    Score = table.Column<double>(type: "REAL", nullable: false),
                    Decision = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResolutionAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResolutionAttempts_CanonicalTracks_CandidateTrackId",
                        column: x => x.CandidateTrackId,
                        principalTable: "CanonicalTracks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ResolutionAttempts_PlaylistItems_PlaylistItemId",
                        column: x => x.PlaylistItemId,
                        principalTable: "PlaylistItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DownloadWorkflows_PlaylistItemId",
                table: "DownloadWorkflows",
                column: "PlaylistItemId");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadWorkflows_WorkflowId",
                table: "DownloadWorkflows",
                column: "WorkflowId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalAccounts_Provider_ExternalUserId",
                table: "ExternalAccounts",
                columns: new[] { "Provider", "ExternalUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalPlaylists_AccountId",
                table: "ExternalPlaylists",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalPlaylists_Provider_ExternalId_AccountId",
                table: "ExternalPlaylists",
                columns: new[] { "Provider", "ExternalId", "AccountId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LocalMediaFiles_CanonicalTrackId",
                table: "LocalMediaFiles",
                column: "CanonicalTrackId");

            migrationBuilder.CreateIndex(
                name: "IX_LocalMediaFiles_Path",
                table: "LocalMediaFiles",
                column: "Path",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlaylistItems_CanonicalTrackId",
                table: "PlaylistItems",
                column: "CanonicalTrackId");

            migrationBuilder.CreateIndex(
                name: "IX_PlaylistItems_PlaylistId_ProviderItemId",
                table: "PlaylistItems",
                columns: new[] { "PlaylistId", "ProviderItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Playlists_ExternalPlaylistId",
                table: "Playlists",
                column: "ExternalPlaylistId");

            migrationBuilder.CreateIndex(
                name: "IX_ProviderSyncStates_Provider_AccountId_ResourceId",
                table: "ProviderSyncStates",
                columns: new[] { "Provider", "AccountId", "ResourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ResolutionAttempts_CandidateTrackId",
                table: "ResolutionAttempts",
                column: "CandidateTrackId");

            migrationBuilder.CreateIndex(
                name: "IX_ResolutionAttempts_PlaylistItemId",
                table: "ResolutionAttempts",
                column: "PlaylistItemId");

            migrationBuilder.CreateIndex(
                name: "IX_TrackSources_CanonicalTrackId",
                table: "TrackSources",
                column: "CanonicalTrackId");

            migrationBuilder.CreateIndex(
                name: "IX_TrackSources_Provider_ExternalId",
                table: "TrackSources",
                columns: new[] { "Provider", "ExternalId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppProfiles");

            migrationBuilder.DropTable(
                name: "AppSettings");

            migrationBuilder.DropTable(
                name: "DownloadWorkflows");

            migrationBuilder.DropTable(
                name: "LocalMediaFiles");

            migrationBuilder.DropTable(
                name: "ProviderSyncStates");

            migrationBuilder.DropTable(
                name: "ResolutionAttempts");

            migrationBuilder.DropTable(
                name: "SchemaInfo");

            migrationBuilder.DropTable(
                name: "TrackSources");

            migrationBuilder.DropTable(
                name: "PlaylistItems");

            migrationBuilder.DropTable(
                name: "CanonicalTracks");

            migrationBuilder.DropTable(
                name: "Playlists");

            migrationBuilder.DropTable(
                name: "ExternalPlaylists");

            migrationBuilder.DropTable(
                name: "ExternalAccounts");
        }
    }
}
