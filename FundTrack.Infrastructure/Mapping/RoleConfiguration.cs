using FundTrack.core.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FundTrack.Infrastructure.Mapping;

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> entity)
    {
        entity.ToTable("role");

        entity.HasKey(e => e.RoleId);
        entity.Property(e => e.RoleId)
              .HasColumnName("role_id")
              .ValueGeneratedOnAdd();

        entity.Property(e => e.Name)
              .IsRequired()
              .HasMaxLength(50)
              .HasColumnName("name");

        entity.Property(e => e.Description)
              .HasMaxLength(255)
              .HasColumnName("description");

        entity.Property(e => e.IsActive)
              .HasColumnName("is_active");

        entity.Property(e => e.CreatedAt)
              .HasColumnName("created_at")
              .HasColumnType("timestamp without time zone");
    }
}
