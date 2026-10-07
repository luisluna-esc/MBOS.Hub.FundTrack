using FundTrack.core.Entities.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FundTrack.Infrastructure.Mapping;

public class ChurchHistoryConfiguration : IEntityTypeConfiguration<ChurchHistory>
{
    public void Configure(EntityTypeBuilder<ChurchHistory> entity)
    {
        entity.ToTable("church_history");

        entity.HasKey(e => e.ChurchHistoryId);
        entity.Property(e => e.ChurchHistoryId)
              .HasColumnName("church_history_id")
              .ValueGeneratedOnAdd();

        entity.Property(e => e.ChurchId)
              .IsRequired()
              .HasColumnName("church_id");

        entity.Property(e => e.ChangeType)
              .IsRequired()
              .HasMaxLength(50)
              .HasColumnName("change_type");

        entity.Property(e => e.OldValue)
              .HasMaxLength(500)
              .HasColumnName("old_value");

        entity.Property(e => e.NewValue)
              .HasMaxLength(500)
              .HasColumnName("new_value");

        entity.Property(e => e.ChangedAt)
              .IsRequired()
              .HasColumnName("changed_at")
              .HasColumnType("date");

        entity.Property(e => e.Reason)
              .HasMaxLength(500)
              .HasColumnName("reason");

        entity.Property(e => e.AppUserId)
              .HasColumnName("app_user_id");

        entity.Property(e => e.RegisteredAt)
              .HasColumnName("registered_at")
              .HasColumnType("timestamp without time zone");
    }
}
