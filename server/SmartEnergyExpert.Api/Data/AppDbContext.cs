using Microsoft.EntityFrameworkCore;
using SmartEnergyExpert.Api.Entities;

namespace SmartEnergyExpert.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Experiment> Experiments => Set<Experiment>();
    public DbSet<ExperimentParameter> ExperimentParameters => Set<ExperimentParameter>();
    public DbSet<Criterion> Criteria => Set<Criterion>();
    public DbSet<CriterionWeight> CriterionWeights => Set<CriterionWeight>();
    public DbSet<Evaluation> Evaluations => Set<Evaluation>();
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

        modelBuilder.Entity<Experiment>()
            .Property(x => x.Status)
            .HasMaxLength(24);

        modelBuilder.Entity<Experiment>()
            .HasOne(x => x.Creator)
            .WithMany()
            .HasForeignKey(x => x.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ExperimentParameter>()
            .HasOne(x => x.Experiment)
            .WithMany(x => x.Parameters)
            .HasForeignKey(x => x.ExperimentId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Criterion>()
            .HasIndex(x => x.Name)
            .IsUnique();

        modelBuilder.Entity<Criterion>()
            .Property(x => x.Name)
            .HasMaxLength(120);

        modelBuilder.Entity<Criterion>()
            .ToTable(x =>
            {
                x.HasCheckConstraint("CK_Criteria_Range", "\"MinValue\" < \"MaxValue\"");
                x.HasCheckConstraint("CK_Criteria_DefaultWeight", "\"DefaultWeight\" > 0");
            });

        modelBuilder.Entity<CriterionWeight>()
            .HasOne(x => x.Criterion)
            .WithMany(x => x.CriterionWeights)
            .HasForeignKey(x => x.CriterionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CriterionWeight>()
            .HasIndex(x => new { x.CriterionId, x.ExperimentType })
            .IsUnique();

        modelBuilder.Entity<CriterionWeight>()
            .ToTable(x => x.HasCheckConstraint("CK_CriterionWeights_Weight", "\"Weight\" > 0"));

        modelBuilder.Entity<Evaluation>()
            .HasOne(x => x.Experiment)
            .WithMany()
            .HasForeignKey(x => x.ExperimentId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Evaluation>()
            .HasOne(x => x.Expert)
            .WithMany()
            .HasForeignKey(x => x.ExpertId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Recommendation>()
            .HasOne(x => x.Evaluation)
            .WithOne(x => x.Recommendation)
            .HasForeignKey<Recommendation>(x => x.EvaluationId);

        modelBuilder.Entity<Recommendation>()
            .HasIndex(x => x.EvaluationId)
            .IsUnique();

        modelBuilder.Entity<Evaluation>()
            .Property(x => x.RiskLevel)
            .HasMaxLength(24);

        modelBuilder.Entity<AuditLog>()
            .HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
