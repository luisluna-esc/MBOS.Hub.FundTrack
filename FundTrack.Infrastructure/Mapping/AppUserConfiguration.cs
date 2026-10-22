using FundTrack.core.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FundTrack.Infrastructure.Mapping;

public class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> entity)
    {
        entity.ToTable("app_user");

        entity.HasKey(e => e.AppUserId);
        entity.Property(e => e.AppUserId)
              .HasColumnName("app_user_id")
              .ValueGeneratedOnAdd();

        entity.Property(e => e.Ci)
              .HasMaxLength(20)
              .HasColumnName("ci");

        entity.Property(e => e.PasswordHash)
              .IsRequired()
              .HasMaxLength(255)
              .HasColumnName("password_hash");

        entity.Property(e => e.FullName)
              .IsRequired()
              .HasMaxLength(200)
              .HasColumnName("full_name");

        entity.Property(e => e.Username)
              .IsRequired()
              .HasMaxLength(100)
              .HasColumnName("username");

        entity.Property(e => e.Phone)
              .HasMaxLength(30)
              .HasColumnName("phone");

        entity.Property(e => e.PastorId)
              .HasColumnName("pastor_id");

        entity.Property(e => e.PasswordChangedAt)
              .HasColumnName("password_changed_at")
              .HasColumnType("timestamp without time zone");

        entity.Property(e => e.FailedLoginAttempts)
              .HasColumnName("failed_login_attempts");

        entity.Property(e => e.LockedUntil)
              .HasColumnName("locked_until")
              .HasColumnType("timestamp without time zone");

        entity.Property(e => e.IsActive)
              .HasColumnName("is_active");

        entity.Property(e => e.LastLogin)
              .HasColumnName("last_login")
              .HasColumnType("timestamp without time zone");

        entity.Property(e => e.CreatedById)
              .HasColumnName("created_by");

        entity.Property(e => e.CreatedAt)
              .HasColumnName("created_at")
              .HasColumnType("timestamp without time zone");
    }
}
