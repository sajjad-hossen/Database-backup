using DatabaseBackupAPI.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace DatabaseBackupAPI.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<DatabaseConfig> DatabaseConfigs => Set<DatabaseConfig>();
    public DbSet<BackupLog> BackupLogs => Set<BackupLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>()
            .HasMany(u => u.DatabaseConfigs)
            .WithOne(d => d.User)
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DatabaseConfig>()
            .HasMany(d => d.BackupLogs)
            .WithOne(b => b.DatabaseConfig)
            .HasForeignKey(b => b.DatabaseConfigId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
