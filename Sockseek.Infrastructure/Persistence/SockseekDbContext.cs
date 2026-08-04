using Microsoft.EntityFrameworkCore;
using Sockseek.Infrastructure.Persistence.Abstractions;
using Sockseek.Infrastructure.Persistence.Entities;

namespace Sockseek.Infrastructure.Persistence;

public sealed class SockseekDbContext(DbContextOptions<SockseekDbContext> options) : DbContext(options)
{
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampConcurrencyTokens();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampConcurrencyTokens();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    public DbSet<AppProfileEntity> AppProfiles => Set<AppProfileEntity>();
    public DbSet<ExternalAccountEntity> ExternalAccounts => Set<ExternalAccountEntity>();
    public DbSet<ExternalPlaylistEntity> ExternalPlaylists => Set<ExternalPlaylistEntity>();
    public DbSet<PlaylistEntity> Playlists => Set<PlaylistEntity>();
    public DbSet<PlaylistItemEntity> PlaylistItems => Set<PlaylistItemEntity>();
    public DbSet<CanonicalTrackEntity> CanonicalTracks => Set<CanonicalTrackEntity>();
    public DbSet<TrackSourceEntity> TrackSources => Set<TrackSourceEntity>();
    public DbSet<LocalMediaFileEntity> LocalMediaFiles => Set<LocalMediaFileEntity>();
    public DbSet<ResolutionAttemptEntity> ResolutionAttempts => Set<ResolutionAttemptEntity>();
    public DbSet<DownloadWorkflowEntity> DownloadWorkflows => Set<DownloadWorkflowEntity>();
    public DbSet<ProviderSyncStateEntity> ProviderSyncStates => Set<ProviderSyncStateEntity>();
    public DbSet<AppSettingEntity> AppSettings => Set<AppSettingEntity>();
    public DbSet<SchemaInfoEntity> SchemaInfos => Set<SchemaInfoEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureConcurrencyTokens(modelBuilder);

        modelBuilder.Entity<AppProfileEntity>(entity =>
        {
            entity.ToTable("AppProfiles");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).IsRequired();
        });

        modelBuilder.Entity<ExternalAccountEntity>(entity =>
        {
            entity.ToTable("ExternalAccounts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ExternalUserId).IsRequired();
            entity.Property(x => x.DisplayName).IsRequired();
            entity.Property(x => x.SecretReference).IsRequired();
            entity.HasIndex(x => new { x.Provider, x.ExternalUserId }).IsUnique();
        });

        modelBuilder.Entity<ExternalPlaylistEntity>(entity =>
        {
            entity.ToTable("ExternalPlaylists");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ExternalId).IsRequired();
            entity.Property(x => x.Name).IsRequired();
            entity.HasIndex(x => new { x.Provider, x.ExternalId, x.AccountId }).IsUnique();
            entity.HasOne(x => x.Account)
                .WithMany(x => x.Playlists)
                .HasForeignKey(x => x.AccountId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PlaylistEntity>(entity =>
        {
            entity.ToTable("Playlists");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).IsRequired();
            entity.HasOne(x => x.ExternalPlaylist)
                .WithMany(x => x.Playlists)
                .HasForeignKey(x => x.ExternalPlaylistId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<PlaylistItemEntity>(entity =>
        {
            entity.ToTable("PlaylistItems");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ProviderItemId).IsRequired();
            entity.Property(x => x.SnapshotJson).IsRequired();
            entity.HasIndex(x => new { x.PlaylistId, x.ProviderItemId }).IsUnique();
            entity.HasOne(x => x.Playlist)
                .WithMany(x => x.Items)
                .HasForeignKey(x => x.PlaylistId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.CanonicalTrack)
                .WithMany(x => x.PlaylistItems)
                .HasForeignKey(x => x.CanonicalTrackId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<CanonicalTrackEntity>(entity =>
        {
            entity.ToTable("CanonicalTracks");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Artist).IsRequired();
            entity.Property(x => x.Title).IsRequired();
            entity.Property(x => x.NormalizedArtist).IsRequired();
            entity.Property(x => x.NormalizedTitle).IsRequired();
        });

        modelBuilder.Entity<TrackSourceEntity>(entity =>
        {
            entity.ToTable("TrackSources");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ExternalId).IsRequired();
            entity.HasIndex(x => new { x.Provider, x.ExternalId }).IsUnique();
            entity.HasOne(x => x.CanonicalTrack)
                .WithMany(x => x.Sources)
                .HasForeignKey(x => x.CanonicalTrackId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LocalMediaFileEntity>(entity =>
        {
            entity.ToTable("LocalMediaFiles");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Path).IsRequired();
            entity.HasIndex(x => x.Path).IsUnique();
            entity.HasOne(x => x.CanonicalTrack)
                .WithMany(x => x.LocalMediaFiles)
                .HasForeignKey(x => x.CanonicalTrackId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ResolutionAttemptEntity>(entity =>
        {
            entity.ToTable("ResolutionAttempts");
            entity.HasKey(x => x.Id);
            entity.HasOne(x => x.PlaylistItem)
                .WithMany(x => x.ResolutionAttempts)
                .HasForeignKey(x => x.PlaylistItemId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.CandidateTrack)
                .WithMany(x => x.ResolutionAttempts)
                .HasForeignKey(x => x.CandidateTrackId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<DownloadWorkflowEntity>(entity =>
        {
            entity.ToTable("DownloadWorkflows");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.WorkflowId).IsUnique();
            entity.HasOne(x => x.PlaylistItem)
                .WithMany(x => x.DownloadWorkflows)
                .HasForeignKey(x => x.PlaylistItemId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ProviderSyncStateEntity>(entity =>
        {
            entity.ToTable("ProviderSyncStates");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ResourceId).IsRequired();
            entity.HasIndex(x => new { x.Provider, x.AccountId, x.ResourceId }).IsUnique();
        });

        modelBuilder.Entity<AppSettingEntity>(entity =>
        {
            entity.ToTable("AppSettings");
            entity.HasKey(x => x.Key);
            entity.Property(x => x.JsonValue).IsRequired();
        });

        modelBuilder.Entity<SchemaInfoEntity>(entity =>
        {
            entity.ToTable("SchemaInfo");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ApplicationVersion).IsRequired();
            entity.Property(x => x.MigrationVersion).IsRequired();
        });
    }

    private void StampConcurrencyTokens()
    {
        foreach (var entry in ChangeTracker.Entries<IHasConcurrencyToken>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified)
                entry.Entity.ConcurrencyToken = Guid.NewGuid();
        }
    }

    private static void ConfigureConcurrencyTokens(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(IHasConcurrencyToken).IsAssignableFrom(entityType.ClrType))
            {
                modelBuilder.Entity(entityType.ClrType)
                    .Property(nameof(IHasConcurrencyToken.ConcurrencyToken))
                    .IsConcurrencyToken();
            }
        }
    }
}
