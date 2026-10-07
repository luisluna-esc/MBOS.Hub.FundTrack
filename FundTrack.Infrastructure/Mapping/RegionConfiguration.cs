using FundTrack.core.Entities.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FundTrack.Infrastructure.Mapping;

public class RegionConfiguration : IEntityTypeConfiguration<Region>
{
    public void Configure(EntityTypeBuilder<Region> entity)
    {
        entity.ToTable("region");

        entity.HasKey(e => e.RegionId);
        entity.Property(e => e.RegionId)
              .HasColumnName("region_id")
              .ValueGeneratedOnAdd();

        entity.Property(e => e.Name)
              .IsRequired()
              .HasMaxLength(100)
              .HasColumnName("name");

        entity.Property(e => e.IsActive)
              .HasColumnName("is_active");

        entity.Property(e => e.CreatedAt)
              .HasColumnName("created_at")
              .HasColumnType("timestamp without time zone");

        entity.Property(e => e.PendingBalance)
              .HasColumnName("pending_balance")
              .HasPrecision(14, 2);
    }
}
