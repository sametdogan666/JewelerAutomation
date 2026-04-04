using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using JewelerAutomation.Application.Interfaces;
using JewelerAutomation.Application.Options;
using JewelerAutomation.Application.Services;
using JewelerAutomation.Core.Entities;
using JewelerAutomation.Infrastructure.Auditing;
using JewelerAutomation.Infrastructure.Data;
using JewelerAutomation.Infrastructure.GoldRates;
using JewelerAutomation.Infrastructure.Repositories;
using JewelerAutomation.WebAPI.HostedServices;
using JewelerAutomation.WebAPI.Hubs;
using JewelerAutomation.WebAPI.Services;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// DbContext - MSSQL veya PostgreSQL (appsettings'ten ConnectionStrings:DefaultConnection)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var usePostgres = builder.Configuration.GetValue<bool>("UsePostgres");

builder.Services.AddMemoryCache();
builder.Services.Configure<HaremGoldOptions>(builder.Configuration.GetSection(HaremGoldOptions.SectionName));
builder.Services.Configure<GoldRateFallbackOptions>(builder.Configuration.GetSection(GoldRateFallbackOptions.SectionName));
builder.Services.Configure<GoldScraperOptions>(builder.Configuration.GetSection(GoldScraperOptions.SectionName));
builder.Services.AddHttpClient("GoldScraper", client =>
{
    client.Timeout = TimeSpan.FromSeconds(6);
    client.DefaultRequestHeaders.UserAgent.ParseAdd(
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");
});
builder.Services.AddHttpClient("HaremAltin", (sp, client) =>
{
    var o = sp.GetRequiredService<IOptions<HaremGoldOptions>>().Value;
    var baseUrl = string.IsNullOrWhiteSpace(o.BaseUrl) ? "https://haremapi.tr/api/v1" : o.BaseUrl.TrimEnd('/');
    client.BaseAddress = new Uri(baseUrl + "/");
    client.Timeout = TimeSpan.FromSeconds(Math.Clamp(
        sp.GetRequiredService<IOptions<HaremGoldOptions>>().Value.RequestTimeoutSeconds + 2,
        3,
        120));
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<AuditSaveChangesInterceptor>();

builder.Services.AddDbContext<AppDbContext>((sp, options) =>
{
    if (usePostgres)
        options.UseNpgsql(connectionString);
    else
        options.UseSqlServer(connectionString);
    options.AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>());
});

// Repositories & Unit of Work
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
builder.Services.AddScoped<IDailyGoldRateRepository, DailyGoldRateRepository>();
builder.Services.AddScoped<IGoldRatesRepository, GoldRatesRepository>();
builder.Services.AddSingleton<IGoldRateCircuitBreaker, GoldRateCircuitBreaker>();
builder.Services.AddScoped<IGoldRateService, GoldRateService>();
builder.Services.AddScoped<IDashboardSummaryService, DashboardRawSummaryService>();
builder.Services.AddHostedService<GoldRateBackgroundService>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
builder.Services.AddScoped<ISafeMovementRepository, SafeMovementRepository>();
builder.Services.AddScoped<IInventoryRepository, InventoryRepository>();
builder.Services.AddScoped<ICustomerMovementRepository, CustomerMovementRepository>();
builder.Services.AddScoped<ICustomerTransactionRepository, CustomerTransactionRepository>();
builder.Services.AddScoped<ICashPeggingLogRepository, CashPeggingLogRepository>();
builder.Services.AddScoped<ILedgerRepository, LedgerRepository>();
builder.Services.AddScoped<ICashToGoldConversionRepository, CashToGoldConversionRepository>();
builder.Services.AddScoped<IProductTemplateRepository, ProductTemplateRepository>();
builder.Services.AddScoped<IGoldTransactionRepository, GoldTransactionRepository>();
builder.Services.AddScoped<ILinkingProcessRepository, LinkingProcessRepository>();
builder.Services.AddScoped<IRepository<LinkingDetail>, LinkingDetailRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

builder.Services.AddScoped<IAccountingService, AccountingService>();
builder.Services.AddScoped<ICashPeggingService, CashPeggingService>();
builder.Services.AddScoped<IGoldLinkingService, GoldLinkingService>();
builder.Services.AddScoped<IPeggingService, PeggingService>();
builder.Services.AddScoped<ICapitalCalculationService, CapitalCalculationService>();
builder.Services.AddScoped<ISafeStatusService, SafeStatusService>();
builder.Services.AddScoped<IProfitCalculationService, ProfitCalculationService>();
builder.Services.AddScoped<ILedgerService, LedgerService>();
builder.Services.AddScoped<ILedgerMigrationService, LedgerMigrationService>();
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();

// JWT
var jwtKey = builder.Configuration["Jwt:Key"] ?? "JewelerAutomationSecretKeyMinimum32Characters!";
var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "JewelerAutomation",
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "JewelerAutomation",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30), // Küçük saat farkında 401 önlenir
            NameClaimType = ClaimTypes.Name,
            RoleClaimType = ClaimTypes.Role
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var path = context.HttpContext.Request.Path;
                if (path.StartsWithSegments("/hubs")
                    && context.Request.Query.TryGetValue("access_token", out var token)
                    && !string.IsNullOrEmpty(token))
                    context.Token = token;
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddSignalR();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins((builder.Configuration["Cors:Origins"] ?? "http://localhost:4200").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .AllowAnyHeader()
            .AllowAnyMethod()
            .WithExposedHeaders("Authorization")
            .AllowCredentials();
    });
});
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.WriteIndented = false;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Jeweler Automation API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
// Development'ta HTTPS redirect yok (proxy + token için); production'da aktif
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<GoldRatesHub>("/hubs/gold-rates");

// İlk çalıştırmada migration uygula (veritabanı yoksa oluşturulur)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync().ConfigureAwait(false);
}

// Seed: admin kullanıcı + örnek veriler (cariler, kasa hareketleri)
await SeedData.SeedAdminUserAsync(app.Services).ConfigureAwait(false);
await SeedData.SeedSampleDataAsync(app.Services).ConfigureAwait(false);

// Ledger: rebuild from source data on every startup to ensure consistency
using (var scope = app.Services.CreateScope())
{
    var migrationService = scope.ServiceProvider.GetRequiredService<ILedgerMigrationService>();
    await migrationService.RebuildLedgerAsync().ConfigureAwait(false);
}

app.Run();
