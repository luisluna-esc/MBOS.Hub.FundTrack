using FundTrack.core.Entities.Funds;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FundTrack.Infrastructure.Mapping;

public class LocalFundCountConfiguration : IEntityTypeConfiguration<LocalFundCount>
{
    public void Configure(EntityTypeBuilder<LocalFundCount> entity)
    {
        entity.ToTable("local_fund_count");

        entity.HasKey(e => e.LocalFundCountId);
        entity.Property(e => e.LocalFundCountId)
              .HasColumnName("local_fund_count_id")
              .ValueGeneratedOnAdd();

        entity.Property(e => e.ChurchId)
              .IsRequired()
              .HasColumnName("church_id");

        entity.Property(e => e.PeriodId)
              .IsRequired()
              .HasColumnName("period_id");

        entity.Property(e => e.DistrictId)
              .IsRequired()
              .HasColumnName("district_id");

        entity.Property(e => e.PastorId)
              .HasColumnName("pastor_id");

        entity.Property(e => e.ChurchType)
              .IsRequired()
              .HasMaxLength(30)
              .HasColumnName("church_type");

        entity.Property(e => e.SubmissionStatus)
              .IsRequired()
              .HasMaxLength(30)
              .HasColumnName("submission_status");

        entity.Property(e => e.AcmsBalance)
              .HasColumnName("acms_balance")
              .HasPrecision(14, 2);

        entity.Property(e => e.BankStatementBalance)
              .HasColumnName("bank_statement_balance")
              .HasPrecision(14, 2);

        entity.Property(e => e.BankDeposit)
              .HasColumnName("bank_deposit")
              .HasPrecision(14, 2);

        entity.Property(e => e.LocalCash)
              .HasColumnName("local_cash")
              .HasPrecision(14, 2);

        entity.Property(e => e.UsdAmount)
              .HasColumnName("usd_amount")
              .HasPrecision(14, 2);

        entity.Property(e => e.ExchangeRate)
              .HasColumnName("exchange_rate")
              .HasPrecision(10, 4);

        entity.Property(e => e.UsdAmountBs)
              .HasColumnName("usd_amount_bs")
              .HasPrecision(14, 2);

        entity.Property(e => e.DocumentsTotal)
              .HasColumnName("documents_total")
              .HasPrecision(14, 2);

        entity.Property(e => e.TotalAmount)
              .HasColumnName("total_amount")
              .HasPrecision(14, 2);

        entity.Property(e => e.Difference)
              .HasColumnName("difference")
              .HasPrecision(14, 2);

        entity.Property(e => e.DifferenceStatus)
              .HasMaxLength(30)
              .HasColumnName("difference_status");

        entity.Property(e => e.CountDate)
              .HasColumnName("count_date")
              .HasColumnType("date");

        entity.Property(e => e.TreasurerName)
              .HasMaxLength(150)
              .HasColumnName("treasurer_name");

        entity.Property(e => e.ElderName)
              .HasMaxLength(150)
              .HasColumnName("elder_name");

        entity.Property(e => e.SignedByPastor)
              .HasColumnName("signed_by_pastor");

        entity.Property(e => e.SignedByElder)
              .HasColumnName("signed_by_elder");

        entity.Property(e => e.Observation)
              .HasMaxLength(1000)
              .HasColumnName("observation");

        entity.Property(e => e.CreatedById)
              .HasColumnName("created_by");

        entity.Property(e => e.CreatedAt)
              .HasColumnName("created_at")
              .HasColumnType("timestamp without time zone");

        entity.Property(e => e.UpdatedById)
              .HasColumnName("updated_by");

        entity.Property(e => e.UpdatedAt)
              .HasColumnName("updated_at")
              .HasColumnType("timestamp without time zone");

        entity.HasMany(e => e.Documents)
              .WithOne()
              .HasForeignKey(d => d.LocalFundCountId)
              .OnDelete(DeleteBehavior.Cascade);
    }
}
