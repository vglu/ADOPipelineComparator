using ADOPipelineComparator.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ADOPipelineComparator.Data;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<OrganizationEntity> Organizations => Set<OrganizationEntity>();

    public DbSet<AdoSiteEntity> AdoSites => Set<AdoSiteEntity>();

    public DbSet<PipelineCacheEntity> PipelineCache => Set<PipelineCacheEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<OrganizationEntity>(entity =>
        {
            entity.ToTable("Organizations");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Name)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.OrganizationUrl)
                .HasMaxLength(300)
                .IsRequired();

            entity.Property(x => x.Pat)
                .HasMaxLength(500)
                .IsRequired();

            entity.Property(x => x.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            entity.Property(x => x.CreatedAtUtc)
                .IsRequired();

            entity.Property(x => x.UpdatedAtUtc)
                .IsRequired();

            entity.HasMany(x => x.Sites)
                .WithOne(x => x.Organization)
                .HasForeignKey(x => x.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AdoSiteEntity>(entity =>
        {
            entity.ToTable("AdoSites");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.ProjectName)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(x => x.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            entity.Property(x => x.CreatedAtUtc)
                .IsRequired();

            entity.Property(x => x.UpdatedAtUtc)
                .IsRequired();

            entity.HasMany(x => x.Pipelines)
                .WithOne(x => x.AdoSite)
                .HasForeignKey(x => x.AdoSiteId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PipelineCacheEntity>(entity =>
        {
            entity.ToTable("PipelineCache");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.OrganizationName)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.Project)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(x => x.PipelineName)
                .HasMaxLength(300)
                .IsRequired();

            entity.Property(x => x.PipelineType)
                .HasMaxLength(30)
                .IsRequired();

            entity.Property(x => x.PipelineSubtype)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(x => x.LastRunBy)
                .HasMaxLength(150);

            entity.Property(x => x.TaskName)
                .HasMaxLength(300);

            entity.Property(x => x.PipelineUrl)
                .HasMaxLength(500)
                .IsRequired();

            entity.Property(x => x.CachedAtUtc)
                .IsRequired();

            entity.HasIndex(x => new
            {
                x.AdoSiteId,
                x.Project,
                x.PipelineId,
                x.PipelineType,
            })
            .IsUnique();
        });
    }
}
