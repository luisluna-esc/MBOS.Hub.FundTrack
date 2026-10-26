using FundTrack.Application.Dtos.Funds;
using FundTrack.Application.Dtos.Organization;
using FundTrack.Application.Dtos.Security;
using FundTrack.Application.Interfaces.Audit;
using FundTrack.Application.Interfaces.Charts;
using FundTrack.Application.Interfaces.Dashboard;
using FundTrack.Application.Interfaces.Funds;
using FundTrack.Application.Interfaces.Imports;
using FundTrack.Application.Interfaces.Organization;
using FundTrack.Application.Interfaces.Reports;
using FundTrack.Application.Interfaces.Security;
using FundTrack.Application.Services;
using FundTrack.Application.Validators.Funds;
using FundTrack.Application.Validators.Organization;
using FundTrack.Application.Validators.Security;
using FundTrack.core.Configuration;
using FundTrack.core.Interfaces;
using FundTrack.Infrastructure.Context;
using FundTrack.Infrastructure.Context.Core;
using FundTrack.Infrastructure.Interceptors;
using FundTrack.Infrastructure.Security;
using FundTrack.Infrastructure.Storage;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FundTrack.Infrastructure.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Missing connection string 'DefaultConnection' in appsettings.");

        services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<AuditSaveChangesInterceptor>();

        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
            options.UseNpgsql(connectionString)
                   .AddInterceptors(serviceProvider.GetRequiredService<AuditSaveChangesInterceptor>()));

        services.Configure<PaginationOptions>(configuration.GetSection("Pagination"));
        services.AddScoped(sp => sp.GetRequiredService<IOptions<PaginationOptions>>().Value);

        services.Configure<JwtOptions>(configuration.GetSection("Jwt"));
        services.AddScoped(sp => sp.GetRequiredService<IOptions<JwtOptions>>().Value);

        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IValidator<RoleDto>, RoleValidator>();

        services.AddScoped<IAppUserService, AppUserService>();
        services.AddScoped<IValidator<AppUserDto>, AppUserValidator>();

        services.AddScoped<IAppUserRoleService, AppUserRoleService>();

        services.AddScoped<IMenuService, MenuService>();
        services.AddScoped<IValidator<MenuDto>, MenuValidator>();
        services.AddScoped<IRoleMenuService, RoleMenuService>();

        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IAuthService, AuthService>();

        services.AddScoped<IPasswordService, PasswordService>();
        services.AddScoped<IValidator<ChangePasswordRequestDto>, ChangePasswordRequestValidator>();

        services.Configure<AuditRetentionOptions>(configuration.GetSection("AuditRetention"));
        services.AddScoped(sp => sp.GetRequiredService<IOptions<AuditRetentionOptions>>().Value);
        services.AddScoped<IAuditLogService, AuditLogService>();

        services.AddScoped<IDataScopeService, DataScopeService>();

        services.AddScoped<IRegionService, RegionService>();
        services.AddScoped<IValidator<RegionDto>, RegionValidator>();

        services.AddScoped<IDistrictService, DistrictService>();
        services.AddScoped<IValidator<DistrictRequestDto>, DistrictRequestValidator>();
        services.AddScoped<IValidator<DistrictPastorAssignRequestDto>, DistrictPastorAssignRequestValidator>();

        services.AddScoped<IPastorService, PastorService>();
        services.AddScoped<IValidator<PastorRequestDto>, PastorRequestValidator>();

        services.AddScoped<IChurchService, ChurchService>();
        services.AddScoped<IValidator<ChurchRequestDto>, ChurchRequestValidator>();
        services.AddScoped<IValidator<ChurchCloseRequestDto>, ChurchCloseRequestValidator>();

        services.AddScoped<IPeriodService, PeriodService>();
        services.AddScoped<IValidator<PeriodRequestDto>, PeriodRequestValidator>();

        services.Configure<AttachmentOptions>(configuration.GetSection("Attachments"));
        services.AddScoped(sp => sp.GetRequiredService<IOptions<AttachmentOptions>>().Value);
        services.AddScoped<IFileStorage, LocalFileStorage>();

        services.AddScoped<ILocalFundCountService, LocalFundCountService>();
        services.AddScoped<IValidator<LocalFundCountRequestDto>, LocalFundCountRequestValidator>();

        services.AddScoped<IDifferenceFollowUpService, DifferenceFollowUpService>();
        services.AddScoped<IValidator<DifferenceFollowUpRequestDto>, DifferenceFollowUpRequestValidator>();

        services.AddScoped<ILocalFundCountCorrectionService, LocalFundCountCorrectionService>();
        services.AddScoped<IValidator<LocalFundCountCorrectionRequestDto>, LocalFundCountCorrectionRequestValidator>();

        services.AddScoped<ILocalFundCountAttachmentService, LocalFundCountAttachmentService>();

        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IChartService, ChartService>();

        services.AddScoped<IChurchImportService, ChurchImportService>();

        services.AddScoped<IReportService, ReportService>();

        return services;
    }
}
