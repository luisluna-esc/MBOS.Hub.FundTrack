using FundTrack.core.Entities.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FundTrack.Infrastructure.Mapping;

public class PastorConfiguration : IEntityTypeConfiguration<Pastor>
{
    public void Configure(EntityTypeBuilder<Pastor> entity)
    {
        entity.ToTable("pastor");

        entity.HasKey(e => e.PastorId);
        entity.Property(e => e.PastorId)
              .HasColumnName("pastor_id")
              .ValueGeneratedOnAdd();

        entity.Property(e => e.Ci)
              .HasMaxLength(20)
              .HasColumnName("ci");

        entity.Property(e => e.FirstNames)
              .IsRequired()
              .HasMaxLength(100)
              .HasColumnName("first_names");

        entity.Property(e => e.LastNames)
              .IsRequired()
              .HasMaxLength(100)
              .HasColumnName("last_names");

        entity.Property(e => e.Email)
              .HasMaxLength(150)
              .HasColumnName("email");

        entity.Property(e => e.IsActive)
              .HasColumnName("is_active");

        entity.Property(e => e.CreatedAt)
              .HasColumnName("created_at")
              .HasColumnType("timestamp without time zone");
    }
}
