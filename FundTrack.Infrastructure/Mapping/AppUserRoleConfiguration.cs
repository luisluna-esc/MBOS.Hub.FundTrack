using FundTrack.core.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FundTrack.Infrastructure.Mapping;

public class AppUserRoleConfiguration : IEntityTypeConfiguration<AppUserRole>
{
    public void Configure(EntityTypeBuilder<AppUserRole> entity)
    {
        entity.ToTable("app_user_role");

        entity.HasKey(e => new { e.AppUserId, e.RoleId });

        entity.Property(e => e.AppUserId)
              .HasColumnName("app_user_id");

        entity.Property(e => e.RoleId)
              .HasColumnName("role_id");

        entity.Property(e => e.AssignedById)
              .HasColumnName("assigned_by");

        entity.Property(e => e.AssignedAt)
              .HasColumnName("assigned_at")
              .HasColumnType("timestamp without time zone");
    }
}
