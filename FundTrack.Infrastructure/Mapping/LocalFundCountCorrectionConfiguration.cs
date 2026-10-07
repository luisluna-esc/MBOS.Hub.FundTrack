using FundTrack.core.Entities.Funds;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FundTrack.Infrastructure.Mapping;

public class LocalFundCountCorrectionConfiguration : IEntityTypeConfiguration<LocalFundCountCorrection>
{
    public void Configure(EntityTypeBuilder<LocalFundCountCorrection> entity)
    {
        entity.ToTable("local_fund_count_correction");

        entity.HasKey(e => e.LocalFundCountCorrectionId);
        entity.Property(e => e.LocalFundCountCorrectionId)
              .HasColumnName("local_fund_count_correction_id")
              .ValueGeneratedOnAdd();

        entity.Property(e => e.LocalFundCountId)
              .IsRequired()
              .HasColumnName("local_fund_count_id");

        entity.Property(e => e.AppliedPeriodId)
              .IsRequired()
              .HasColumnName("applied_period_id");

        entity.Property(e => e.FieldName)
              .IsRequired()
              .HasMaxLength(50)
              .HasColumnName("field_name");

        entity.Property(e => e.PreviousAmount)
              .HasColumnName("previous_amount")
              .HasPrecision(14, 2);

        entity.Property(e => e.CorrectedAmount)
              .HasColumnName("corrected_amount")
              .HasPrecision(14, 2);

        entity.Property(e => e.Reason)
              .IsRequired()
              .HasMaxLength(500)
              .HasColumnName("reason");

        entity.Property(e => e.AppUserId)
              .HasColumnName("app_user_id");

        entity.Property(e => e.RegisteredAt)
              .HasColumnName("registered_at")
              .HasColumnType("timestamp without time zone");
    }
}
