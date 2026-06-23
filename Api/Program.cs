
using Api.BackgroundServices;
using Api.Filters;
using Api.Hangfire;
using Api.Auth;
using Api.Hubs;
using Api.RealTime;
using Api.Services;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Business.Abstract;
using Business.Resources;
using Business.DependencyResolvers.Autofac;
using Core.DependencyResolvers;
using Core.Extensions;
using Core.Utilities.IoC;
using Core.Utilities.Configuration;
using Core.Utilities.Security.Encryption;
using Core.Utilities.Security.JWT;
using Core.Utilities.Security.PhoneSetting;
using DataAccess.Concrete;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IO.Compression;
using System.Threading.RateLimiting;
using Polly;
using Polly.Extensions.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Serilog;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);

// Serilog — tüm ILogger çağrıları (worker, Firebase, SignalR vb.) Logs/app-YYYYMMDD.log'a yazılır
builder.Host.UseSerilog((ctx, cfg) =>
    cfg.ReadFrom.Configuration(ctx.Configuration)
       .Enrich.FromLogContext());

// Add services to the container.

builder.Services.AddScoped<UserStatusFilter>();

builder.Services.AddControllers(opt =>
{
    var policy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    opt.Filters.Add(new AuthorizeFilter(policy));
    opt.Filters.Add(typeof(UserStatusFilter));
})
.AddJsonOptions(options =>
{
    // JSON serialization için camelCase kullan
    options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    options.JsonSerializerOptions.WriteIndented = false;
});

builder.Services.AddDbContext<DatabaseContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Response Compression
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});

builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Optimal;
});

builder.Services.Configure<GzipCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Optimal;
});

builder.Services.Configure<SecurityOption>(
    builder.Configuration.GetSection("SecurityOptions"));
builder.Services.Configure<Core.Utilities.Configuration.AppointmentSettings>(
    builder.Configuration.GetSection("AppointmentSettings"));
builder.Services.Configure<Core.Utilities.Configuration.BackgroundServicesSettings>(
    builder.Configuration.GetSection("BackgroundServices"));
builder.Services.Configure<HangfireSettings>(
    builder.Configuration.GetSection("Hangfire"));
builder.Services.Configure<AdminAuthCookieSettings>(
    builder.Configuration.GetSection(AdminAuthCookieSettings.SectionName));
builder.Services.AddSingleton<IAdminAuthCookieService, AdminAuthCookieService>();

var adminAuthCookieSettings = builder.Configuration
    .GetSection(AdminAuthCookieSettings.SectionName)
    .Get<AdminAuthCookieSettings>() ?? new AdminAuthCookieSettings();

var defaultConnection = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("DefaultConnection yapılandırması bulunamadı.");
var hangfireSettings = builder.Configuration.GetSection("Hangfire").Get<HangfireSettings>() ?? new HangfireSettings();

if (hangfireSettings.Enabled)
{
    builder.Services.AddHangfire((_, configuration) => configuration
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UsePostgreSqlStorage(
            options => options.UseNpgsqlConnection(defaultConnection),
            new PostgreSqlStorageOptions
            {
                PrepareSchemaIfNecessary = true,
                SchemaName = "hangfire",
            }));

    builder.Services.AddHangfireServer(options =>
    {
        options.WorkerCount = Math.Max(1, hangfireSettings.WorkerCount);
        options.SchedulePollingInterval = TimeSpan.FromSeconds(15);
    });

    builder.Services.AddScoped<AppointmentTimeoutProcessor>();
    builder.Services.AddScoped<SubscriptionReminderProcessor>();
}
builder.Services.AddCors(options =>
{
    var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
        ?? Array.Empty<string>();

    // Dev environment'ta admin panel için yaygın localhost origin'leri (Vite default 5173)
    // otomatik allow edilir. Production'da AllowedOrigins listesi sadece prod'u içerir.
    if (builder.Environment.IsDevelopment())
    {
        var devOrigins = new[]
        {
            "http://localhost:5173",
            "http://127.0.0.1:5173",
            "http://localhost:5174",
            "http://localhost:3000"
        };
        allowedOrigins = allowedOrigins.Concat(devOrigins).Distinct().ToArray();
    }

    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // SignalR için gerekli
    });
});


// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var tokenOptions = builder.Configuration
    .GetSection("TokenOptions")
    .Get<TokenOption>() ?? throw new InvalidOperationException("TokenOptions yapılandırması bulunamadı.");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = "Smart";
    options.DefaultChallengeScheme = "Smart";
})
    .AddPolicyScheme("Smart", "Smart JWT or Admin Session", options =>
    {
        options.ForwardDefaultSelector = context =>
        {
            if (context.Request.Cookies.ContainsKey(adminAuthCookieSettings.SessionCookieName))
                return "AdminSession";
            return JwtBearerDefaults.AuthenticationScheme;
        };
    })
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidIssuer = tokenOptions.Issuer,
            ValidAudience = tokenOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = SecurityKeyHelper.CreateSecurityKey(tokenOptions.SecurityKey),
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/app"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    })
    .AddScheme<AuthenticationSchemeOptions, AdminSessionAuthenticationHandler>(
        "AdminSession",
        _ => { });
builder.Services.AddDependencyResolvers(new Core.Utilities.IoC.ICoreModule[]
{
    new CoreModule(),
});

// IHttpContextAccessor CoreModule'de zaten kayıtlı, burada eklemeye gerek yok

builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
builder.Host.ConfigureContainer<ContainerBuilder>(options =>
{
    options.RegisterModule(new AutofacBusinessModule());
});
builder.Services.AddSingleton<IRealTimePublisher, SignalRRealtimePublisher>();
// Anlık çevrimiçi kullanıcı takibi (SignalR bağlantılarına dayalı, in-memory). Singleton olmalı.
builder.Services.AddSingleton<Business.Abstract.IPresenceTracker, Business.Concrete.PresenceTracker>();
builder.Services.AddScoped<IapMobileSubscriptionService>();

// HttpClient for FCM (v1 API) - with retry + circuit breaker
builder.Services.AddHttpClient("FCM", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.BaseAddress = new Uri("https://fcm.googleapis.com/");
})
.AddTransientHttpErrorPolicy(p => p.WaitAndRetryAsync(
    retryCount: 3,
    sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)))) // 2s, 4s, 8s
.AddTransientHttpErrorPolicy(p => p.CircuitBreakerAsync(
    handledEventsAllowedBeforeBreaking: 5,
    durationOfBreak: TimeSpan.FromSeconds(30)));

// HttpClient for AI services (Gemini + Groq) - with retry + circuit breaker
builder.Services.AddHttpClient("AI", client =>
{
    client.Timeout = TimeSpan.FromSeconds(90);
})
.AddTransientHttpErrorPolicy(p => p.WaitAndRetryAsync(
    retryCount: 2,
    sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)))) // 2s, 4s
.AddTransientHttpErrorPolicy(p => p.CircuitBreakerAsync(
    handledEventsAllowedBeforeBreaking: 5,
    durationOfBreak: TimeSpan.FromSeconds(60)));

// HttpClient for Azure AI Content Safety - görsel moderasyon
builder.Services.AddHttpClient("Azure", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddTransientHttpErrorPolicy(p => p.WaitAndRetryAsync(
    retryCount: 2,
    sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt))));

// HttpClient for NetGSM SMS - with retry
builder.Services.AddHttpClient("NetGsm", client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
    client.BaseAddress = new Uri("https://api.netgsm.com.tr/");
})
.AddTransientHttpErrorPolicy(p => p.WaitAndRetryAsync(
    retryCount: 2,
    sleepDurationProvider: attempt => TimeSpan.FromSeconds(attempt))); // 1s, 2s

// IMemoryCache - NetGSM OTP kod saklama için
builder.Services.AddMemoryCache();

// DataProtection - IIS uygulama havuzu yeniden başladığında anahtarları korur
// (ephemeral key uyarısını ve oturum geçersizleşmelerini önler)
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(
        Path.Combine(AppContext.BaseDirectory, "DataProtection-Keys")))
    .SetApplicationName("HairDresserApi");

// Register IPushNotificationService (will be resolved by Autofac, but we need to register it in DI container too for optional injection)
builder.Services.AddScoped<Business.Abstract.IPushNotificationService, Business.Concrete.FirebasePushNotificationService>();

// Arka plan işleri Hangfire recurring job olarak çalışır (BackgroundService yerine).

// SignalR için JSON serialization ayarları - camelCase kullan
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
})
.AddJsonProtocol(options =>
{
    options.PayloadSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    options.PayloadSerializerOptions.WriteIndented = false;
});



// Health Checks - PostgreSQL
builder.Services.AddHealthChecks()
    .AddNpgSql(
        builder.Configuration.GetConnectionString("DefaultConnection")!,
        name: "postgresql",
        timeout: TimeSpan.FromSeconds(5));

// Rate Limiting - auth endpoint'leri için brute-force koruması
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;

    // 429 yanıtına JSON body ekle (frontend parse edebilsin)
    options.OnRejected = async (ctx, ct) =>
    {
        ctx.HttpContext.Response.ContentType = "application/json";
        var retryAfter = ctx.Lease.TryGetMetadata(System.Threading.RateLimiting.MetadataName.RetryAfter, out var retryAfterValue)
            ? (int)retryAfterValue.TotalSeconds
            : 300;
        await ctx.HttpContext.Response.WriteAsJsonAsync(new
        {
            success = false,
            message = string.Format(Messages.ApiRateLimitTooManyRequests, retryAfter),
            retryAfterSeconds = retryAfter
        }, ct);
    };

    // OTP gönderme limiti: 15 istek / 5 dakika per IP
    // (3 farklı kullanıcı tipi × tekrar denemeler = rahat çalışması için artırıldı)
    options.AddPolicy("send-otp", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 15,
                Window = TimeSpan.FromMinutes(5)
            }));

    // OTP doğrulama limiti: 10 istek / 5 dakika per IP
    options.AddPolicy("verify-otp", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(5)
            }));

    // Refresh token yenileme limiti: 20 istek / 5 dakika per IP
    options.AddPolicy("refresh-token", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(5)
            }));

    // Geriye dönük uyumluluk için "auth" policy'si korunuyor
    options.AddPolicy("auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(5)
            }));

    // Genel API limiti
    options.AddPolicy("general", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 300,
                Window = TimeSpan.FromMinutes(1)
            }));

    // Konum güncelleme limiti - frontend 15sn debounce zaten var, backend güvencesi
    // Kullanıcı ID'sine göre partition (IP değil - aynı ağdaki farklı kullanıcılar etkilenmesin)
    options.AddPolicy("location", context =>
    {
        var userId = context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                     ?? context.Connection.RemoteIpAddress?.ToString()
                     ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: userId,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 1,
                Window = TimeSpan.FromSeconds(20)
            });
    });

    // Keşif/arama limiti - nearby ve filtered endpoint'leri için
    // Kullanıcı ID veya IP (anonim kullanıcılar da nearby görebilir)
    // 60 req/dk: harita kaydırma ve filtre değişimlerinde darboğaz olmaması için yeterli
    options.AddPolicy("discover", context =>
    {
        var key = context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                  ?? context.Connection.RemoteIpAddress?.ToString()
                  ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: key,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1)
            });
    });

    // Mesajlaşma limiti - send action'ları için (typing hariç)
    // Kullanıcı ID'sine göre partition
    options.AddPolicy("messaging", context =>
    {
        var userId = context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                     ?? context.Connection.RemoteIpAddress?.ToString()
                     ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: userId,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 50,
                Window = TimeSpan.FromMinutes(1)
            });
    });

    // Mesaj silme / düzenleme / thread silme - spam ve kötüye kullanımı engellemek için
    // Kullanıcı ID'sine göre partition
    options.AddPolicy("messaging-delete", context =>
    {
        var userId = context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                     ?? context.Connection.RemoteIpAddress?.ToString()
                     ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: userId,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1)
            });
    });

    // Typing indicator - yazarken her karakterde gönderilmesin diye frontend debounce var
    // ama backend güvencesi olarak 240 req/dk (saniyede ~4) sınır
    options.AddPolicy("messaging-typing", context =>
    {
        var userId = context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                     ?? context.Connection.RemoteIpAddress?.ToString()
                     ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: userId,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 240,
                Window = TimeSpan.FromMinutes(1)
            });
    });
});

// builder.WebHost.UseUrls("http://localhost:5000", "https://localhost:5001");

var app = builder.Build();

// Set ServiceTool.ServiceProvider for aspect classes (SecuredOperation, LogAspect)
ServiceTool.ServiceProvider = app.Services;

// Seed Categories
// Configure the HTTP request pipeline.

app.ConfigureCustomExceptionMiddleware();

// Cloudflare / reverse proxy arkasında gerçek IP ve HTTPS bilgisini al
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}


// Response Compression (CORS'tan önce)
app.UseResponseCompression();

app.UseCors();


// Static files - hem wwwroot hem de özel upload klasörünü serve et
app.UseStaticFiles();

// Yüklenen fotoğraflar için özel klasör (LocalStorage:UploadRoot → /uploads/ URL'i)
// Local dev makinesinde sunucudaki C:\inetpub\... yolu erişilemez olabilir; bu durumda
// ContentRoot altındaki güvenli bir fallback'e geçeriz, böylece uygulama açılışta patlamaz.
var uploadRoot = app.Configuration["LocalStorage:UploadRoot"] ?? "wwwroot/hairdresser/uploads";
var resolvedUploadRoot = uploadRoot;
try
{
    if (!Directory.Exists(resolvedUploadRoot))
        Directory.CreateDirectory(resolvedUploadRoot);
}
catch (Exception ex)
{
    var fallback = Path.Combine(app.Environment.ContentRootPath, "wwwroot", "hairdresser", "uploads");
    Log.Warning(ex,
        "Configured upload root ({Configured}) is not accessible. Falling back to {FallbackUploadRoot}",
        resolvedUploadRoot, fallback);
    resolvedUploadRoot = fallback;
    try { Directory.CreateDirectory(resolvedUploadRoot); } catch { /* swallow */ }
}

// Son güvenlik kontrolü — hâlâ yoksa fallback'e geç ve oluştur.
if (!Directory.Exists(resolvedUploadRoot))
{
    var fallback = Path.Combine(app.Environment.ContentRootPath, "wwwroot", "hairdresser", "uploads");
    try { Directory.CreateDirectory(fallback); resolvedUploadRoot = fallback; } catch { /* swallow */ }
}

if (Directory.Exists(resolvedUploadRoot))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(
            Path.GetFullPath(resolvedUploadRoot)),
        RequestPath = "/uploads"
    });
}
else
{
    Log.Warning("Upload root için yazılabilir bir dizin bulunamadı; /uploads endpoint'i devre dışı.");
}

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "API v1");
});

app.UseAuthentication();
app.UseAuthorization();

if (hangfireSettings.Enabled)
{
    app.UseHangfireDashboard(
        hangfireSettings.DashboardPath,
        new DashboardOptions
        {
            Authorization = new[] { new HangfireDashboardAuthorizationFilter() },
            DisplayStorageConnectionString = false,
        });

    var backgroundJobSettings = app.Configuration.GetSection("BackgroundServices").Get<BackgroundServicesSettings>()
        ?? new BackgroundServicesSettings();
    HangfireJobRegistration.RegisterRecurringJobs(hangfireSettings, backgroundJobSettings);
}

app.UseRateLimiter();


//app.UseDeveloperExceptionPage();
//app.MapOpenApi();


app.MapHub<AppHub>("/hubs/app");
app.MapControllers();

// Health check endpoint - load balancer ve monitoring için
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                duration = e.Value.Duration.TotalMilliseconds
            })
        };
        await context.Response.WriteAsJsonAsync(result);
    }
}).AllowAnonymous();

// ---- Seed initial admin user (appsettings:SeedAdmin) ----
// IIS'te Console.WriteLine sessizdir; tüm seed log'larını Serilog üzerinden yazıyoruz.
using (var scope = app.Services.CreateScope())
{
    try
    {
        var cfg = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var seedSection = cfg.GetSection("SeedAdmin");

        Log.Information("[Seed] SeedAdmin kontrolü başladı | Exists={Exists} | Enabled={Enabled}",
            seedSection.Exists(), seedSection.GetValue<bool>("Enabled"));

        if (!seedSection.Exists())
        {
            Log.Warning("[Seed] appsettings'de SeedAdmin bloğu bulunamadı, atlanıyor.");
        }
        else if (!seedSection.GetValue<bool>("Enabled"))
        {
            Log.Information("[Seed] SeedAdmin.Enabled=false, atlanıyor.");
        }
        else
        {
            var adminDal = scope.ServiceProvider.GetRequiredService<DataAccess.Abstract.IAdminUserDal>();
            var email = (seedSection["Email"] ?? string.Empty).Trim().ToLowerInvariant();
            var password = seedSection["Password"] ?? string.Empty;
            var fullName = seedSection["FullName"] ?? "Admin";

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                Log.Warning("[Seed] Email veya Password boş, seed atlandı. Email='{Email}' PasswordEmpty={Pwd}",
                    email, string.IsNullOrWhiteSpace(password));
            }
            else
            {
                var existing = await adminDal.GetByEmail(email);
                if (existing != null)
                {
                    Log.Information("[Seed] Admin zaten var, atlandı. Email={Email} Id={Id}", email, existing.Id);
                }
                else
                {
                    await adminDal.Add(new Entities.Concrete.Entities.AdminUser
                    {
                        Id = Guid.NewGuid(),
                        Email = email,
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                        FullName = fullName,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                    Log.Information("[Seed] Admin oluşturuldu: {Email}", email);
                }
            }
        }
    }
    catch (Exception ex)
    {
        Log.Error(ex, "[Seed] Admin seed hatası: {Message}", ex.Message);
    }
}

app.Run();

