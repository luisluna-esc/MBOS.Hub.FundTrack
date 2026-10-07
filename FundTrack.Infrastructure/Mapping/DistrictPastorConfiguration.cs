using FundTrack.core.Entities.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FundTrack.Infrastructure.Mapping;

public class DistrictPastorConfiguration : IEntityTypeConfiguration<DistrictPastor>
{
    public void Configure(EntityTypeBuilder<DistrictPastor> entity)
    {
        entity.ToTable("district_pastor");

        entity.HasKey(e => e.DistrictPastorId);
        entity.Property(e => e.DistrictPastorId)
              .HasColumnName("district_pastor_id")
              .ValueGeneratedOnAdd();

        entity.Property(e => e.DistrictId)
              .IsRequired()
              .HasColumnName("district_id");

        entity.Property(e => e.PastorId)
              .IsRequired()
              .HasColumnName("pastor_id");

        entity.Property(e => e.StartDate)
              .IsRequired()
              .HasColumnName("start_date")
              .HasColumnType("date");

        entity.Property(e => e.EndDate)
              .HasColumnName("end_date")
              .HasColumnType("date");
    }
}
