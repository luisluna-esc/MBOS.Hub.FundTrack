using FundTrack.core.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FundTrack.Infrastructure.Mapping;

public class PasswordResetConfiguration : IEntityTypeConfiguration<PasswordReset>
{
    public void Configure(EntityTypeBuilder<PasswordReset> entity)
    {
        entity.ToTable("password_reset");

        entity.HasKey(e => e.PasswordResetId);
        entity.Property(e => e.PasswordResetId)
              .HasColumnName("password_reset_id")
              .ValueGeneratedOnAdd();

        entity.Property(e => e.AppUserId)
              .IsRequired()
              .HasColumnName("app_user_id");

        entity.Property(e => e.TokenHash)
              .IsRequired()
              .HasMaxLength(255)
              .HasColumnName("token_hash");

        entity.Property(e => e.ExpiresAt)
              .IsRequired()
              .HasColumnName("expires_at")
              .HasColumnType("timestamp without time zone");

        entity.Property(e => e.IsUsed)
              .HasColumnName("is_used");

        entity.Property(e => e.CreatedAt)
              .HasColumnName("created_at")
              .HasColumnType("timestamp without time zone");
    }
}
