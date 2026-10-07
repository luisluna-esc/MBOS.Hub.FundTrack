using FundTrack.core.Entities.Funds;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FundTrack.Infrastructure.Mapping;

public class DifferenceFollowUpConfiguration : IEntityTypeConfiguration<DifferenceFollowUp>
{
    public void Configure(EntityTypeBuilder<DifferenceFollowUp> entity)
    {
        entity.ToTable("difference_follow_up");

        entity.HasKey(e => e.DifferenceFollowUpId);
        entity.Property(e => e.DifferenceFollowUpId)
              .HasColumnName("difference_follow_up_id")
              .ValueGeneratedOnAdd();

        entity.Property(e => e.LocalFundCountId)
              .IsRequired()
              .HasColumnName("local_fund_count_id");

        entity.Property(e => e.PreviousStatus)
              .HasMaxLength(30)
              .HasColumnName("previous_status");

        entity.Property(e => e.NewStatus)
              .IsRequired()
              .HasMaxLength(30)
              .HasColumnName("new_status");

        entity.Property(e => e.Justification)
              .IsRequired()
              .HasMaxLength(1000)
              .HasColumnName("justification");

        entity.Property(e => e.AppUserId)
              .HasColumnName("app_user_id");

        entity.Property(e => e.FollowedUpAt)
              .HasColumnName("followed_up_at")
              .HasColumnType("timestamp without time zone");
    }
}
