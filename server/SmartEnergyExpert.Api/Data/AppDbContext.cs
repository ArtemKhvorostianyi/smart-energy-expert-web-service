using Microsoft.EntityFrameworkCore;
using SmartEnergyExpert.Api.Entities;

namespace SmartEnergyExpert.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Experiment> Experiments => Set<Experiment>();
    public DbSet<ExperimentParameter> ExperimentParameters => Set<ExperimentParameter>();
    public DbSet<Evaluation> Evaluations => Set<Evaluation>();
    public DbSet<Recommendation> Recommendations => Set<Recommendation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Role>()
            .HasIndex(x => x.Name)
            .IsUnique();

        modelBuilder.Entity<User>()
            .HasIndex(x => x.Email)
            .IsUnique();

        modelBuilder.Entity<Recommendation>()
            .HasOne(x => x.Evaluation)
            .WithOne(x => x.Recommendation)
            .HasForeignKey<Recommendation>(x => x.EvaluationId);
    }
}
