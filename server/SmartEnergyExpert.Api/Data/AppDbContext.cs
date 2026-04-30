using Microsoft.EntityFrameworkCore;
using SmartEnergyExpert.Api.Entities;

namespace SmartEnergyExpert.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Dataset> Datasets => Set<Dataset>();
    public DbSet<AcousticSample> AcousticSamples => Set<AcousticSample>();
    public DbSet<ComparisonRun> ComparisonRuns => Set<ComparisonRun>();
    public DbSet<DifferencePoint> DifferencePoints => Set<DifferencePoint>();
    public DbSet<Recommendation> Recommendations => Set<Recommendation>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Role>()
            .HasIndex(x => x.Name)
            .IsUnique();

        modelBuilder.Entity<User>()
            .HasIndex(x => x.Email)
            .IsUnique();

        modelBuilder.Entity<Dataset>()
            .HasIndex(x => x.Name)
            .IsUnique();

        modelBuilder.Entity<Dataset>()
            .HasIndex(x => new { x.Type, x.SourceSystem });

        modelBuilder.Entity<AcousticSample>()
            .HasOne(x => x.Dataset)
            .WithMany(x => x.Samples)
            .HasForeignKey(x => x.DatasetId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AcousticSample>()
            .HasIndex(x => new { x.DatasetId, x.Timestamp, x.FrequencyBand });

        modelBuilder.Entity<ComparisonRun>()
            .HasOne(x => x.SimulationDataset)
            .WithMany()
            .HasForeignKey(x => x.SimulationDatasetId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ComparisonRun>()
            .HasOne(x => x.FieldDataset)
            .WithMany()
            .HasForeignKey(x => x.FieldDatasetId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ComparisonRun>()
            .HasIndex(x => x.CreatedAt);

        modelBuilder.Entity<DifferencePoint>()
            .HasOne(x => x.ComparisonRun)
            .WithMany(x => x.Differences)
            .HasForeignKey(x => x.ComparisonRunId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DifferencePoint>()
            .HasIndex(x => new { x.ComparisonRunId, x.Severity, x.Timestamp, x.FrequencyBand });

        modelBuilder.Entity<Recommendation>()
            .HasOne(x => x.ComparisonRun)
            .WithMany(x => x.Recommendations)
            .HasForeignKey(x => x.ComparisonRunId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Recommendation>()
            .HasIndex(x => new { x.ComparisonRunId, x.Confidence });

        modelBuilder.Entity<AuditLog>()
            .HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
