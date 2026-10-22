using FundTrack.core.Entities.Funds;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FundTrack.Infrastructure.Mapping;

public class LocalFundCountDocumentConfiguration : IEntityTypeConfiguration<LocalFundCountDocument>
{
    public void Configure(EntityTypeBuilder<LocalFundCountDocument> entity)
    {
        entity.ToTable("local_fund_count_document");

        entity.HasKey(e => e.LocalFundCountDocumentId);
        entity.Property(e => e.LocalFundCountDocumentId)
              .HasColumnName("local_fund_count_document_id")
              .ValueGeneratedOnAdd();

        entity.Property(e => e.LocalFundCountId)
              .IsRequired()
              .HasColumnName("local_fund_count_id");

        entity.Property(e => e.DocumentDate)
              .IsRequired()
              .HasColumnName("document_date")
              .HasColumnType("date");

        entity.Property(e => e.DocumentNumber)
              .HasMaxLength(50)
              .HasColumnName("document_number");

        entity.Property(e => e.Detail)
              .IsRequired()
              .HasMaxLength(500)
              .HasColumnName("detail");

        entity.Property(e => e.Amount)
              .HasColumnName("amount")
              .HasPrecision(14, 2);
    }
}
