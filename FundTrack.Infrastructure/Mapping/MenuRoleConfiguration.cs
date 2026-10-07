using FundTrack.core.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FundTrack.Infrastructure.Mapping;

public class MenuRoleConfiguration : IEntityTypeConfiguration<MenuRole>
{
    public void Configure(EntityTypeBuilder<MenuRole> entity)
    {
        entity.ToTable("menu_role");

        entity.HasKey(e => new { e.MenuId, e.RoleId });

        entity.Property(e => e.MenuId)
              .HasColumnName("menu_id");

        entity.Property(e => e.RoleId)
              .HasColumnName("role_id");
    }
}
