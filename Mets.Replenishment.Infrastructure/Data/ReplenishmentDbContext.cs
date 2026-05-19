using Microsoft.EntityFrameworkCore;
using Mets.Replenishment.Core.Entities;

namespace Mets.Replenishment.Infrastructure.Data;

public class ReplenishmentDbContext : DbContext
{
    public ReplenishmentDbContext(DbContextOptions<ReplenishmentDbContext> options) : base(options)
    {
    }

    public DbSet<ReplenishmentRequest> Requests { get; set; } = null!;
    public DbSet<ReplenishmentRequestItem> RequestItems { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ReplenishmentRequest>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Location).IsRequired().HasMaxLength(100);
            
            entity.HasMany(e => e.Items)
                  .WithOne()
                  .HasForeignKey(i => i.RequestId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ReplenishmentRequestItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ArticleNumber).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Description).IsRequired().HasMaxLength(255);
        });
    }
}
