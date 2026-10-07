using FundTrack.core.Entities.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FundTrack.Infrastructure.Mapping;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> entity)
    {
        entity.ToTable("audit_log");

        entity.HasKey(e => e.AuditLogId);
        entity.Property(e => e.AuditLogId)
              .HasColumnName("audit_log_id")
              .ValueGeneratedOnAdd();

        entity.Property(e => e.AppUserId)
              .HasColumnName("app_user_id");

        entity.Property(e => e.Action)
              .IsRequired()
              .HasMaxLength(20)
              .HasColumnName("action");

        entity.Property(e => e.Module)
              .HasMaxLength(100)
              .HasColumnName("module");

        entity.Property(e => e.TableName)
              .IsRequired()
              .HasMaxLength(100)
              .HasColumnName("table_name");

        entity.Property(e => e.RecordId)
              .HasColumnName("record_id");

        entity.Property(e => e.Description)
              .HasMaxLength(500)
              .HasColumnName("description");

        entity.Property(e => e.OldValue)
              .HasColumnName("old_value")
              .HasColumnType("jsonb");

        entity.Property(e => e.NewValue)
              .HasColumnName("new_value")
              .HasColumnType("jsonb");

        entity.Property(e => e.Ip)
              .HasMaxLength(50)
              .HasColumnName("ip");

        entity.Property(e => e.UserAgent)
              .HasMaxLength(500)
              .HasColumnName("user_agent");

        entity.Property(e => e.CreatedAt)
              .IsRequired()
              .HasColumnName("created_at")
              .HasColumnType("timestamp without time zone");
    }
}
