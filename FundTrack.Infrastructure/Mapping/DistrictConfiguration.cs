using FundTrack.core.Entities.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FundTrack.Infrastructure.Mapping;

public class DistrictConfiguration : IEntityTypeConfiguration<District>
{
    public void Configure(EntityTypeBuilder<District> entity)
    {
        entity.ToTable("district");

        entity.HasKey(e => e.DistrictId);
        entity.Property(e => e.DistrictId)
              .HasColumnName("district_id")
              .ValueGeneratedOnAdd();

        entity.Property(e => e.RegionId)
              .IsRequired()
              .HasColumnName("region_id");

        entity.Property(e => e.Name)
              .IsRequired()
              .HasMaxLength(100)
              .HasColumnName("name");

        entity.Property(e => e.IsMissionary)
              .HasColumnName("is_missionary");

        entity.Property(e => e.IsActive)
              .HasColumnName("is_active");

        entity.Property(e => e.CreatedAt)
              .HasColumnName("created_at")
              .HasColumnType("timestamp without time zone");
    }
}
