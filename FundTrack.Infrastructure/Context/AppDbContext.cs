using FundTrack.core.Entities.Audit;
using FundTrack.core.Entities.Funds;
using FundTrack.core.Entities.Organization;
using FundTrack.core.Entities.Security;
using FundTrack.Infrastructure.Mapping;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FundTrack.Infrastructure.Context;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Role> Roles => Set<Role>();
    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<AppUserRole> AppUserRoles => Set<AppUserRole>();
    public DbSet<Menu> Menus => Set<Menu>();
    public DbSet<MenuRole> MenuRoles => Set<MenuRole>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PasswordReset> PasswordResets => Set<PasswordReset>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Region> Regions => Set<Region>();
    public DbSet<District> Districts => Set<District>();
    public DbSet<Pastor> Pastors => Set<Pastor>();
    public DbSet<DistrictPastor> DistrictPastors => Set<DistrictPastor>();
    public DbSet<Church> Churches => Set<Church>();
    public DbSet<ChurchHistory> ChurchHistories => Set<ChurchHistory>();
    public DbSet<Period> Periods => Set<Period>();
    public DbSet<LocalFundCount> LocalFundCounts => Set<LocalFundCount>();
    public DbSet<LocalFundCountDocument> LocalFundCountDocuments => Set<LocalFundCountDocument>();
    public DbSet<LocalFundCountAttachment> LocalFundCountAttachments => Set<LocalFundCountAttachment>();
    public DbSet<LocalFundCountCorrection> LocalFundCountCorrections => Set<LocalFundCountCorrection>();
    public DbSet<DifferenceFollowUp> DifferenceFollowUps => Set<DifferenceFollowUp>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RoleConfiguration).Assembly);

        // All "timestamp" columns in the schema are without time zone, but the app always works in UTC.
        // This converter strips the Kind on write and restores it to Utc on read, so every DateTime
        // property across every entity behaves consistently without repeating this per property.
        var utcConverter = new ValueConverter<DateTime, DateTime>(
            v => DateTime.SpecifyKind(v, DateTimeKind.Unspecified),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        var nullableUtcConverter = new ValueConverter<DateTime?, DateTime?>(
            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Unspecified) : v,
            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime))
                    property.SetValueConverter(utcConverter);
                else if (property.ClrType == typeof(DateTime?))
                    property.SetValueConverter(nullableUtcConverter);
            }
        }

        base.OnModelCreating(modelBuilder);
    }
}
