using KorridorX.BackgroundJobs;
using KorridorX.Configuration;
using KorridorX.Data;
using KorridorX.Data.Seed;
using KorridorX.Infrastructure;
using KorridorX.HealthChecks;
using KorridorX.Middleware;
using KorridorX.Models.Enums;
using KorridorX.Models.Identity;
using KorridorX.Providers.DigitalAssets;
using KorridorX.Providers.Remittance;
using KorridorX.Providers.Remittance.Blaaiz;
using KorridorX.Providers.Screening;
using KorridorX.Services.Auth;
using KorridorX.Services.Audit;
using KorridorX.Services.AdminUsers;
using KorridorX.Services.BusinessContext;
using KorridorX.Services.BusinessBeneficiaries;
using KorridorX.Services.BusinessTransfers;
using KorridorX.Services.BusinessFunding;
using KorridorX.Services.FinancialCore;
using KorridorX.Services.Instant;
using KorridorX.Services.Marketplace;
using KorridorX.Services.Notifications;
using KorridorX.Services.Operations;
using KorridorX.Services.Compliance;
using KorridorX.Services.Customers;
using KorridorX.Services.DigitalAssets;
using KorridorX.Services.EmbeddedFinance;
using KorridorX.Services.Finance;
using KorridorX.Services.Fx;
using KorridorX.Services.Payments;
using KorridorX.Services.Recipients;
using KorridorX.Services.References;
using KorridorX.Services.Transfers;
using KorridorX.Services.Providers;
using KorridorX.Services.Reconciliation;
using KorridorX.Services.Webhooks;
using KorridorX.Services.Security;
using KorridorX.Services.Support;
using KorridorX.Services.Treasury;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Diagnostics;
using System.Net;
using System.Security.Claims;
using System.Threading.RateLimiting;

Activity.DefaultIdFormat = ActivityIdFormat.W3C;
Activity.ForceDefaultIdFormat = true;

var builder = WebApplication.CreateBuilder(args);

var hostingOptions = builder.Configuration
    .GetSection(HostingOptions.SectionName)
    .Get<HostingOptions>() ?? new HostingOptions();

builder.Logging.ClearProviders();
if (hostingOptions.JsonConsoleLogging)
{
    builder.Logging.AddJsonConsole(options =>
    {
        options.IncludeScopes = true;
        options.UseUtcTimestamp = true;
        options.TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";
    });
}
else
{
    builder.Logging.AddSimpleConsole(options =>
    {
        options.IncludeScopes = true;
        options.SingleLine = true;
        options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
        options.UseUtcTimestamp = true;
    });
    builder.Logging.AddDebug();
}

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services
    .AddOptions<IdentitySeedOptions>()
    .Bind(builder.Configuration.GetSection(IdentitySeedOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<IdentitySeedOptions>, IdentitySeedOptionsValidator>();
builder.Services
    .AddOptions<HostingOptions>()
    .Bind(builder.Configuration.GetSection(HostingOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<HostingOptions>, HostingOptionsValidator>();
builder.Services
    .AddOptions<BlaaizOptions>()
    .Bind(builder.Configuration.GetSection("Blaaiz"))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<BlaaizOptions>, BlaaizOptionsValidator>();
builder.Services
    .AddOptions<NotificationDeliveryOptions>()
    .Bind(builder.Configuration.GetSection(NotificationDeliveryOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<NotificationDeliveryOptions>, NotificationDeliveryOptionsValidator>();
builder.Services
    .AddOptions<SecurityOptions>()
    .Bind(builder.Configuration.GetSection(SecurityOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<SecurityOptions>, SecurityOptionsValidator>();
builder.Services
    .AddOptions<SupportOptions>()
    .Bind(builder.Configuration.GetSection(SupportOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<SupportOptions>, SupportOptionsValidator>();
builder.Services
    .AddOptions<ComplianceScreeningOptions>()
    .Bind(builder.Configuration.GetSection(ComplianceScreeningOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<ComplianceScreeningOptions>, ComplianceScreeningOptionsValidator>();
builder.Services
    .AddOptions<OpenSanctionsOptions>()
    .Bind(builder.Configuration.GetSection(OpenSanctionsOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<OpenSanctionsOptions>, OpenSanctionsOptionsValidator>();
builder.Services
    .AddOptions<DataRetentionOptions>()
    .Bind(builder.Configuration.GetSection(DataRetentionOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<DataRetentionOptions>, DataRetentionOptionsValidator>();
builder.Services
    .AddOptions<TreasuryOptions>()
    .Bind(builder.Configuration.GetSection(TreasuryOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<TreasuryOptions>, TreasuryOptionsValidator>();
builder.Services
    .AddOptions<AccountingOptions>()
    .Bind(builder.Configuration.GetSection(AccountingOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<AccountingOptions>, AccountingOptionsValidator>();
builder.Services.AddMemoryCache();

var dataProtection = builder.Services
    .AddDataProtection()
    .SetApplicationName(hostingOptions.ApplicationName);

if (!string.IsNullOrWhiteSpace(hostingOptions.DataProtectionKeysPath))
{
    Directory.CreateDirectory(hostingOptions.DataProtectionKeysPath);
    dataProtection.PersistKeysToFileSystem(
        new DirectoryInfo(hostingOptions.DataProtectionKeysPath));
}

builder.Services.AddHostedService<StartupConfigurationValidationService>();

builder.Services.AddControllers();
builder.Services
    .AddHealthChecks()
    .AddCheck(
        "self",
        () => HealthCheckResult.Healthy("KorridorX process is running."),
        tags: ["live"])
    .AddCheck<DatabaseReadinessHealthCheck>(
        "postgresql",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["ready"]);

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor |
                               ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = hostingOptions.ForwardLimit;

    foreach (var proxy in hostingOptions.TrustedProxies)
    {
        if (IPAddress.TryParse(proxy, out var address))
            options.KnownProxies.Add(address);
    }
});

var securityOptions = builder.Configuration
    .GetSection(SecurityOptions.SectionName)
    .Get<SecurityOptions>() ?? new SecurityOptions();

builder.Services.Configure<DataProtectionTokenProviderOptions>(options =>
{
    options.TokenLifespan = TimeSpan.FromMinutes(
        securityOptions.Accounts.TokenLifespanMinutes);
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsJsonAsync(
            ApiResponses.Fail(
                "Too many requests. Please wait before trying again.",
                "RATE_LIMIT_EXCEEDED"),
            cancellationToken);
    };

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        var rateLimits = ResolveRateLimits(httpContext);
        return RateLimitPartition.GetFixedWindowLimiter(
            ResolveRateLimitKey(httpContext),
            _ => CreateFixedWindowOptions(
                rateLimits.GlobalPermitLimit,
                rateLimits.GlobalWindowMinutes));
    });

    options.AddPolicy(SecurityRateLimitPolicies.Authentication, httpContext =>
    {
        var rateLimits = ResolveRateLimits(httpContext);
        return RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => CreateFixedWindowOptions(
                rateLimits.AuthenticationPermitLimit,
                rateLimits.AuthenticationWindowMinutes));
    });

    options.AddPolicy(SecurityRateLimitPolicies.Sensitive, httpContext =>
    {
        var rateLimits = ResolveRateLimits(httpContext);
        return RateLimitPartition.GetFixedWindowLimiter(
            ResolveRateLimitKey(httpContext),
            _ => CreateFixedWindowOptions(
                rateLimits.SensitivePermitLimit,
                rateLimits.SensitiveWindowMinutes));
    });

    options.AddPolicy(SecurityRateLimitPolicies.Webhook, httpContext =>
    {
        var rateLimits = ResolveRateLimits(httpContext);
        return RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => CreateFixedWindowOptions(
                rateLimits.WebhookPermitLimit,
                rateLimits.WebhookWindowMinutes));
    });
});

builder.Services.AddScoped<IReferenceGenerator, ReferenceGenerator>();
builder.Services.AddScoped<IProviderRequestAuditService, ProviderRequestAuditService>();
builder.Services.AddScoped<IBlaaizTokenService, BlaaizTokenService>();
builder.Services.AddScoped<IRemittanceProvider, BlaaizRemittanceProvider>();
builder.Services.AddScoped<IProviderCustomerService, ProviderCustomerService>();
builder.Services.AddScoped<IBankDirectoryService, BankDirectoryService>();
builder.Services.AddScoped<IProviderOperationsQueryService, ProviderOperationsQueryService>();
builder.Services.AddScoped<IKycService, KycService>();
builder.Services.AddScoped<IBusinessKybService, BusinessKybService>();
builder.Services.AddScoped<IAdminKycService, AdminKycService>();
builder.Services.AddScoped<IAdminBusinessKybService, AdminBusinessKybService>();
builder.Services.AddScoped<IBlaaizWebhookService, BlaaizWebhookService>();

builder.Services.AddHttpClient("BlaaizAuth", (serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<BlaaizOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
});

builder.Services.AddHttpClient<IBlaaizApiClient, BlaaizApiClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<BlaaizOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
});

builder.Services.AddHttpClient(OpenSanctionsScreeningProvider.HttpClientName, (serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<OpenSanctionsOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
});

builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IMfaChallengeStore, MfaChallengeStore>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IRoleSeeder, RoleSeeder>();
builder.Services.AddScoped<ISeedUserProvisioner, SeedUserProvisioner>();
builder.Services.AddScoped<ISuperAdminSeeder, SuperAdminSeeder>();
builder.Services.AddScoped<INonProductionTestUserSeeder, NonProductionTestUserSeeder>();
builder.Services.AddScoped<IdentitySeedRunner>();

builder.Services.AddScoped<ICustomerProfileService, CustomerProfileService>();
builder.Services.AddScoped<IRecipientService, RecipientService>();
builder.Services.AddScoped<IBusinessBeneficiaryService, BusinessBeneficiaryService>();
builder.Services.AddScoped<IBusinessAccessService, BusinessAccessService>();
builder.Services.AddScoped<IBusinessUserService, BusinessUserService>();
builder.Services.AddScoped<IBusinessInvitationService, BusinessInvitationService>();
builder.Services.AddScoped<IBusinessTransferService, BusinessTransferService>();
builder.Services.AddScoped<IBusinessPaymentBatchService, BusinessPaymentBatchService>();
builder.Services.AddScoped<IBusinessReportExportService, BusinessReportExportService>();
builder.Services.AddScoped<IBusinessFundingService, BusinessFundingService>();
builder.Services.AddScoped<IFinancialReservationService, FinancialReservationService>();
builder.Services.Configure<DigitalAssetComplianceOptions>(
    builder.Configuration.GetSection("DigitalAssets:Compliance"));
builder.Services.AddScoped<IDigitalAssetProvider, BlaaizDigitalAssetProvider>();
builder.Services.AddScoped<IDigitalAssetProviderRegistry, DigitalAssetProviderRegistry>();
builder.Services.AddScoped<IDigitalAssetComplianceGate, DigitalAssetComplianceGate>();
builder.Services.AddScoped<IEmbeddedDigitalAssetService, DigitalAssetService>();
builder.Services.AddScoped<IDigitalAssetSettlementService, DigitalAssetService>();
builder.Services.AddScoped<IDigitalAssetDepositIntentSettlementService, DigitalAssetService>();
builder.Services.AddScoped<KorridorX.Services.BusinessDigitalAssets.IBusinessDigitalAssetService, KorridorX.Services.BusinessDigitalAssets.BusinessDigitalAssetService>();
builder.Services.AddScoped<IDigitalAssetReconciliationService, DigitalAssetReconciliationService>();
builder.Services.AddScoped<IDigitalAssetProviderOperationsService, DigitalAssetProviderOperationsService>();
builder.Services.AddScoped<IDigitalAssetEnablementService, DigitalAssetEnablementService>();
builder.Services.AddScoped<IDigitalAssetProviderWebhookService, DigitalAssetProviderWebhookService>();
builder.Services.AddScoped<IMarketplaceSettlementService, MarketplaceSettlementService>();
builder.Services.AddScoped<IMarketplaceOperationsService, MarketplaceOperationsService>();
builder.Services.AddScoped<IMarketplaceMatchingEngine, MarketplaceMatchingEngine>();
builder.Services.AddScoped<IMarketplaceOrderService, MarketplaceOrderService>();
builder.Services.AddScoped<IBusinessTradingRfqService, BusinessTradingRfqService>();
builder.Services.AddScoped<KorridorX.Services.BusinessTrading.IBusinessTradingService, KorridorX.Services.BusinessTrading.BusinessTradingService>();
builder.Services.AddScoped<IBusinessPricingService, BusinessPricingService>();
builder.Services.AddScoped<IInstantTradingService, InstantTradingService>();
builder.Services.AddScoped<IEmbeddedFinanceManagementService, EmbeddedFinanceManagementService>();
builder.Services.AddScoped<IEmbeddedFinanceAdminQueryService, EmbeddedFinanceAdminQueryService>();
builder.Services.AddScoped<IEmbeddedFinanceAdminCommandService, EmbeddedFinanceAdminCommandService>();
builder.Services.AddScoped<IEmbeddedFinanceAdminAssuranceService, EmbeddedFinanceAdminAssuranceService>();
builder.Services.AddScoped<IEmbeddedFinanceCustomerService, EmbeddedFinanceCustomerService>();
builder.Services.AddScoped<IEmbeddedFinanceCredentialAuthenticator, EmbeddedFinanceCredentialAuthenticator>();
builder.Services.AddScoped<IEmbeddedFinanceContextAccessor, HttpEmbeddedFinanceContextAccessor>();
builder.Services.AddScoped<ICollectionAccountProvisioningService, CollectionAccountProvisioningService>();
builder.Services.AddScoped<IEmbeddedInboundCollectionService, EmbeddedInboundCollectionService>();
builder.Services.AddScoped<IEmbeddedFinancePayoutService, EmbeddedFinancePayoutService>();
builder.Services.AddScoped<IEmbeddedFinanceTransferService, EmbeddedFinanceTransferService>();
builder.Services.AddScoped<IEmbeddedTradingService, EmbeddedTradingService>();
builder.Services.AddScoped<IEmbeddedPayoutSettlementService, EmbeddedPayoutSettlementService>();
builder.Services.AddScoped<ICollectionAccountProvisioner, BlaaizCollectionAccountProvisioner>();
builder.Services.AddScoped<IEmbeddedWebhookUrlSecurityValidator, EmbeddedWebhookUrlSecurityValidator>();
builder.Services.AddScoped<IEmbeddedWebhookManagementService, EmbeddedWebhookManagementService>();
builder.Services.AddScoped<IEmbeddedWebhookOutboxStager, EmbeddedWebhookOutboxStager>();
builder.Services.AddScoped<IEmbeddedWebhookPublisher, EmbeddedWebhookPublisher>();
builder.Services.AddScoped<IEmbeddedWebhookSender, EmbeddedWebhookSender>();
builder.Services.AddHttpClient(EmbeddedWebhookSender.HttpClientName, client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    AllowAutoRedirect = false
});
builder.Services.AddScoped<INotificationQueueService, NotificationQueueService>();
builder.Services.AddScoped<INotificationOperationsService, NotificationOperationsService>();
builder.Services.AddScoped<INotificationDeliveryProvider, SmtpNotificationDeliveryProvider>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IAdminUserManagementService, AdminUserManagementService>();
builder.Services.AddScoped<IOperationalHealthService, OperationalHealthService>();
builder.Services.AddScoped<IBusinessContextAccessor, HttpBusinessContextAccessor>();
builder.Services.AddScoped<ITransferQuoteService, TransferQuoteService>();
builder.Services.AddScoped<IFxOperationsService, FxOperationsService>();
builder.Services.AddScoped<IFinanceReportingService, FinanceReportingService>();
builder.Services.AddScoped<IAccountingService, AccountingService>();
builder.Services.AddScoped<IFinancialCloseService, FinancialCloseService>();
builder.Services.AddScoped<ITreasuryService, TreasuryService>();
builder.Services.AddScoped<ITransferStatusService, TransferStatusService>();
builder.Services.AddScoped<ITransferService, TransferService>();
builder.Services.AddScoped<ITransactionPinService, TransactionPinService>();
// Required by collection, payout, and business-funding services.
builder.Services.AddScoped<IComplianceGateService, ComplianceGateService>();
builder.Services.AddScoped<IOutboundFundsRestrictionService, OutboundFundsRestrictionService>();
builder.Services.AddScoped<IComplianceLimitService, ComplianceLimitService>();
builder.Services.AddScoped<IComplianceCaseService, ComplianceCaseService>();
builder.Services.AddScoped<IComplianceScreeningService, ComplianceScreeningService>();
builder.Services.AddScoped<ITransactionMonitoringService, TransactionMonitoringService>();
builder.Services.AddScoped<IRegulatoryReportingService, RegulatoryReportingService>();
builder.Services.AddScoped<IDataRetentionService, DataRetentionService>();
builder.Services.AddScoped<IComplianceManagementReportService, ComplianceManagementReportService>();
builder.Services.AddScoped<IComplianceOperationsQueryService, ComplianceOperationsQueryService>();
builder.Services.AddScoped<ITransferRiskService, TransferRiskService>();
builder.Services.AddScoped<ISupportService, SupportService>();
builder.Services.AddSingleton<ISupportEvidenceStorage, FileSystemSupportEvidenceStorage>();
builder.Services.AddScoped<IAdminSupportService, AdminSupportService>();
builder.Services.AddSingleton<ConfiguredWatchlistScreeningProvider>();
builder.Services.AddSingleton<OpenSanctionsScreeningProvider>();
builder.Services.AddSingleton<ISanctionsScreeningProvider, ScreeningProviderRouter>();
builder.Services.AddScoped<ICollectionPaymentMethodPolicy, CollectionPaymentMethodPolicy>();
builder.Services.AddScoped<ICollectionStatusService, CollectionStatusService>();
builder.Services.AddScoped<ICollectionService, CollectionService>();
builder.Services.AddScoped<IPayoutStatusService, PayoutStatusService>();
builder.Services.AddScoped<IPayoutService, PayoutService>();
builder.Services.AddScoped<IRefundService, RefundService>();
builder.Services.AddScoped<IProviderReconciliationService, ProviderReconciliationService>();
builder.Services.AddHostedService<PayoutDispatchWorker>();
builder.Services.AddHostedService<BlaaizReconciliationWorker>();
builder.Services.AddHostedService<NotificationDeliveryWorker>();
builder.Services.AddHostedService<EmbeddedWebhookDeliveryWorker>();
builder.Services.AddHostedService<ComplianceRescreeningWorker>();
builder.Services.AddHostedService<DataRetentionWorker>();
builder.Services.AddHostedService<SupportSlaWorker>();
builder.Services.AddHostedService<TreasurySyncWorker>();
builder.Services.AddHostedService<AccountingSyncWorker>();

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
});

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
    {
        options.User.RequireUniqueEmail = true;

        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = false;

        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer();

// Configure JWT options lazily so WebApplicationFactory/test-host configuration
// overrides are available before the bearer handler is resolved. Eagerly reading
// builder.Configuration here prevents ConfigureAppConfiguration(...) overrides in
// integration tests from supplying Jwt settings.
builder.Services
    .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IConfiguration>((options, configuration) =>
    {
        var jwtOptions = configuration.GetSection("Jwt").Get<JwtOptions>()
            ?? throw new InvalidOperationException("Jwt configuration is missing.");

        if (string.IsNullOrWhiteSpace(jwtOptions.Key) || jwtOptions.Key.Length < 32)
        {
            throw new InvalidOperationException("Jwt:Key must be at least 32 characters.");
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key));

        // Keep standard JWT claim names such as "amr" intact. The MFA
        // validation below reads the RFC claim directly.
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,

            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,

            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = ValidateAccessTokenAsync,

            OnChallenge = async context =>
            {
                if (context.Response.HasStarted)
                    return;

                context.HandleResponse();

                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";

                var response = ApiResponses.Fail(
                    "Authentication required.",
                    "UNAUTHORIZED",
                    "A valid Bearer access token is required.");

                await context.Response.WriteAsJsonAsync(response);
            },

            OnForbidden = async context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json";

                var response = ApiResponses.Fail(
                    "You do not have permission to perform this action.",
                    "FORBIDDEN",
                    "Your account is authenticated but does not have the required permission.");

                await context.Response.WriteAsJsonAsync(response);
            }
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AngularClient", policy =>
    {
        policy
            .WithOrigins(hostingOptions.AllowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "KorridorX API",
        Version = "v1",
        Description = "KorridorX remittance backend API"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Enter JWT token only. Example: eyJhbGciOi...",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddHttpContextAccessor();

var app = builder.Build();
var runtimeHostingOptions = app.Services
    .GetRequiredService<IOptions<HostingOptions>>()
    .Value;

if (runtimeHostingOptions.SwaggerEnabled)
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "KorridorX API v1");
        options.RoutePrefix = "swagger";
    });
}

if (runtimeHostingOptions.TrustedProxies.Length > 0)
    app.UseForwardedHeaders();

app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseCors("AngularClient");

if (runtimeHostingOptions.RequireHttpsRedirection)
    app.UseHttpsRedirection();

app.UseAuthentication();
app.UseRateLimiter();
app.UseMiddleware<EmbeddedFinanceApiKeyMiddleware>();
app.UseAuthorization();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("live"),
    ResponseWriter = HealthCheckResponseWriter.WriteAsync
}).DisableRateLimiting();

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
    ResponseWriter = HealthCheckResponseWriter.WriteAsync
}).DisableRateLimiting();

app.MapControllers();

if (!app.Environment.IsEnvironment("Testing"))
{
    await using var seedScope = app.Services.CreateAsyncScope();
    var identitySeedRunner = seedScope.ServiceProvider.GetRequiredService<IdentitySeedRunner>();
    await identitySeedRunner.SeedAsync();
}

app.Run();

static FixedWindowRateLimiterOptions CreateFixedWindowOptions(int permitLimit, int windowMinutes) =>
    new()
    {
        PermitLimit = Math.Max(1, permitLimit),
        Window = TimeSpan.FromMinutes(Math.Max(1, windowMinutes)),
        QueueLimit = 0,
        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        AutoReplenishment = true
    };

static string ResolveRateLimitKey(HttpContext context) =>
    context.User.FindFirstValue(ClaimTypes.NameIdentifier)
    ?? context.Connection.RemoteIpAddress?.ToString()
    ?? "unknown";

static RateLimitOptions ResolveRateLimits(HttpContext context) =>
    context.RequestServices
        .GetRequiredService<IOptions<SecurityOptions>>()
        .Value
        .RateLimits;

static async Task ValidateAccessTokenAsync(TokenValidatedContext context)
{
    var userIdValue = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
    if (!Guid.TryParse(userIdValue, out var userId))
    {
        context.Fail("The access token does not identify a valid user.");
        return;
    }

    var userManager = context.HttpContext.RequestServices
        .GetRequiredService<UserManager<ApplicationUser>>();
    var user = await userManager.FindByIdAsync(userId.ToString());

    if (user is null || user.Status != UserStatus.Active)
    {
        context.Fail("The account is unavailable.");
        return;
    }

    if (await userManager.IsLockedOutAsync(user))
    {
        context.Fail("The account is temporarily locked.");
        return;
    }

    var claimedStamp = context.Principal?.FindFirstValue(SecurityStampSecurity.ClaimType);
    var currentStamp = await userManager.GetSecurityStampAsync(user);
    if (!SecurityStampSecurity.Matches(claimedStamp, currentStamp))
    {
        context.Fail("The access token was invalidated by an account security change.");
        return;
    }

    var securityOptions = context.HttpContext.RequestServices
        .GetRequiredService<IOptions<SecurityOptions>>()
        .Value;
    if (securityOptions.Accounts.RequireConfirmedEmail && !user.EmailConfirmed)
    {
        context.Fail("Email confirmation is required before this account can be used.");
        return;
    }

    var currentRoles = await userManager.GetRolesAsync(user);
    var requiresMfa =
        user.TwoFactorEnabled ||
        (securityOptions.Mfa.EnforceForPrivilegedRoles &&
         MfaSecurityPolicy.RequiresMfa(currentRoles));
    var usedMfa = context.Principal?
        .FindAll(MfaSecurityPolicy.AuthenticationMethodClaim)
        .Any(x => string.Equals(
            x.Value,
            MfaSecurityPolicy.MfaAuthenticationMethod,
            StringComparison.Ordinal)) == true;

    if (requiresMfa && (!user.TwoFactorEnabled || !usedMfa))
        context.Fail("Multi-factor authentication is required for this account.");
}

public partial class Program { }
