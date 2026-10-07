using FundTrack.core.Entities.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FundTrack.Infrastructure.Mapping;

public class PeriodConfiguration : IEntityTypeConfiguration<Period>
{
    public void Configure(EntityTypeBuilder<Period> entity)
    {
        entity.ToTable("period");

        entity.HasKey(e => e.PeriodId);
        entity.Property(e => e.PeriodId)
              .HasColumnName("period_id")
              .ValueGeneratedOnAdd();

        entity.Property(e => e.Year)
              .IsRequired()
              .HasColumnName("year");

        entity.Property(e => e.Month)
              .IsRequired()
              .HasColumnName("month");

        entity.Property(e => e.DueDate)
              .IsRequired()
              .HasColumnName("due_date")
              .HasColumnType("date");

        entity.Property(e => e.Status)
              .IsRequired()
              .HasMaxLength(30)
              .HasColumnName("status");

        entity.Property(e => e.ClosedAt)
              .HasColumnName("closed_at")
              .HasColumnType("timestamp without time zone");

        entity.Property(e => e.ClosedById)
              .HasColumnName("closed_by");
    }
}
