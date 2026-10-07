using FundTrack.core.Entities.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FundTrack.Infrastructure.Mapping;

public class ChurchConfiguration : IEntityTypeConfiguration<Church>
{
    public void Configure(EntityTypeBuilder<Church> entity)
    {
        entity.ToTable("church");

        entity.HasKey(e => e.ChurchId);
        entity.Property(e => e.ChurchId)
              .HasColumnName("church_id")
              .ValueGeneratedOnAdd();

        entity.Property(e => e.Code)
              .IsRequired()
              .HasMaxLength(20)
              .HasColumnName("code");

        entity.Property(e => e.Name)
              .IsRequired()
              .HasMaxLength(150)
              .HasColumnName("name");

        entity.Property(e => e.Type)
              .IsRequired()
              .HasMaxLength(30)
              .HasColumnName("type");

        entity.Property(e => e.DistrictId)
              .IsRequired()
              .HasColumnName("district_id");

        entity.Property(e => e.Address)
              .HasMaxLength(255)
              .HasColumnName("address");

        entity.Property(e => e.Phone)
              .HasMaxLength(30)
              .HasColumnName("phone");

        entity.Property(e => e.TreasurerName)
              .HasMaxLength(150)
              .HasColumnName("treasurer_name");

        entity.Property(e => e.TreasurerPhone)
              .HasMaxLength(30)
              .HasColumnName("treasurer_phone");

        entity.Property(e => e.ElderName)
              .HasMaxLength(150)
              .HasColumnName("elder_name");

        entity.Property(e => e.ElderPhone)
              .HasMaxLength(30)
              .HasColumnName("elder_phone");

        entity.Property(e => e.Status)
              .IsRequired()
              .HasMaxLength(30)
              .HasColumnName("status");

        entity.Property(e => e.MergedIntoChurchId)
              .HasColumnName("merged_into_church_id");

        entity.Property(e => e.ClosedAt)
              .HasColumnName("closed_at")
              .HasColumnType("date");

        entity.Property(e => e.CreatedAt)
              .HasColumnName("created_at")
              .HasColumnType("timestamp without time zone");

        entity.Property(e => e.ParentChurchId)
              .HasColumnName("parent_church_id");
    }
}
