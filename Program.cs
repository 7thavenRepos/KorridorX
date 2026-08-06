using KorridorX.BackgroundJobs;
using KorridorX.Configuration;
using KorridorX.Data;
using KorridorX.Data.Seed;
using KorridorX.Infrastructure;
using KorridorX.Middleware;
using KorridorX.Models.Identity;
using KorridorX.Providers.Remittance;
using KorridorX.Providers.Remittance.Blaaiz;
using KorridorX.Services.Auth;
using KorridorX.Services.BusinessBeneficiaries;
using KorridorX.Services.Compliance;
using KorridorX.Services.Fx;
using KorridorX.Services.Payments;
using KorridorX.Services.Recipients;
using KorridorX.Services.References;
using KorridorX.Services.Transfers;
using KorridorX.Services.Providers;
using KorridorX.Services.Reconciliation;
using KorridorX.Services.Webhooks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services
    .AddOptions<BlaaizOptions>()
    .Bind(builder.Configuration.GetSection("Blaaiz"))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<BlaaizOptions>, BlaaizOptionsValidator>();
builder.Services.AddMemoryCache();
builder.Services.AddDataProtection();

builder.Services.AddControllers();

builder.Services.AddScoped<IReferenceGenerator, ReferenceGenerator>();
builder.Services.AddScoped<IProviderRequestAuditService, ProviderRequestAuditService>();
builder.Services.AddScoped<IBlaaizTokenService, BlaaizTokenService>();
builder.Services.AddScoped<IRemittanceProvider, BlaaizRemittanceProvider>();
builder.Services.AddScoped<IProviderCustomerService, ProviderCustomerService>();
builder.Services.AddScoped<IBankDirectoryService, BankDirectoryService>();
builder.Services.AddScoped<IProviderOperationsQueryService, ProviderOperationsQueryService>();
builder.Services.AddScoped<IKycService, KycService>();
builder.Services.AddScoped<IBusinessKybService, BusinessKybService>();
builder.Services.AddScoped<IComplianceGateService, ComplianceGateService>();
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

builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();

builder.Services.AddScoped<IRecipientService, RecipientService>();
builder.Services.AddScoped<IBusinessBeneficiaryService, BusinessBeneficiaryService>();
builder.Services.AddScoped<ITransferQuoteService, TransferQuoteService>();
builder.Services.AddScoped<ITransferStatusService, TransferStatusService>();
builder.Services.AddScoped<ITransferService, TransferService>();
builder.Services.AddScoped<ICollectionPaymentMethodPolicy, CollectionPaymentMethodPolicy>();
builder.Services.AddScoped<ICollectionStatusService, CollectionStatusService>();
builder.Services.AddScoped<ICollectionService, CollectionService>();
builder.Services.AddScoped<IPayoutStatusService, PayoutStatusService>();
builder.Services.AddScoped<IPayoutService, PayoutService>();
builder.Services.AddScoped<IRefundService, RefundService>();
builder.Services.AddScoped<IProviderReconciliationService, ProviderReconciliationService>();
builder.Services.AddHostedService<PayoutDispatchWorker>();
builder.Services.AddHostedService<BlaaizReconciliationWorker>();

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

var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>()
    ?? throw new InvalidOperationException("Jwt configuration is missing.");

if (string.IsNullOrWhiteSpace(jwtOptions.Key) || jwtOptions.Key.Length < 32)
{
    throw new InvalidOperationException("Jwt:Key must be at least 32 characters.");
}

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key));

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
            OnChallenge = async context =>
            {
                context.HandleResponse();

                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";

                var response = ApiResponses.Fail(
                    "Authentication required.",
                    "UNAUTHORIZED",
                    "A valid Bearer access token was not supplied.");

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
            },

            OnAuthenticationFailed = async context =>
            {
                context.NoResult();

                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";

                var isExpired = context.Exception is SecurityTokenExpiredException;

                var response = ApiResponses.Fail(
                    isExpired ? "Your session has expired." : "Invalid access token.",
                    isExpired ? "TOKEN_EXPIRED" : "INVALID_TOKEN",
                    isExpired
                        ? "Please refresh your access token or log in again."
                        : "The supplied JWT could not be validated.");

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
            .WithOrigins("http://localhost:4200")
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

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "KorridorX API v1");
        options.RoutePrefix = "swagger";
    });
}

app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseCors("AngularClient");

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

await RoleSeeder.SeedRolesAsync(app.Services);

app.Run();