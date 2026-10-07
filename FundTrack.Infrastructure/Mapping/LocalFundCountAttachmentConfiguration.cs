using FundTrack.core.Entities.Funds;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FundTrack.Infrastructure.Mapping;

public class LocalFundCountAttachmentConfiguration : IEntityTypeConfiguration<LocalFundCountAttachment>
{
    public void Configure(EntityTypeBuilder<LocalFundCountAttachment> entity)
    {
        entity.ToTable("local_fund_count_attachment");

        entity.HasKey(e => e.LocalFundCountAttachmentId);
        entity.Property(e => e.LocalFundCountAttachmentId)
              .HasColumnName("local_fund_count_attachment_id")
              .ValueGeneratedOnAdd();

        entity.Property(e => e.LocalFundCountId)
              .IsRequired()
              .HasColumnName("local_fund_count_id");

        entity.Property(e => e.FileName)
              .IsRequired()
              .HasMaxLength(255)
              .HasColumnName("file_name");

        entity.Property(e => e.FilePath)
              .IsRequired()
              .HasMaxLength(500)
              .HasColumnName("file_path");

        entity.Property(e => e.MimeType)
              .IsRequired()
              .HasMaxLength(100)
              .HasColumnName("mime_type");

        entity.Property(e => e.SizeBytes)
              .IsRequired()
              .HasColumnName("size_bytes");

        entity.Property(e => e.AppUserId)
              .HasColumnName("app_user_id");

        entity.Property(e => e.RegisteredAt)
              .HasColumnName("registered_at")
              .HasColumnType("timestamp without time zone");
    }
}
