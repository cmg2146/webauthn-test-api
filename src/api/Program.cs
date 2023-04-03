namespace WebAuthnTest.Api;

using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Azure.Identity;
using Azure.Security.KeyVault.Keys;
using WebAuthnTest.Database;

public class Program
{
    private const string HEALTHCHECK_READY_TAG = "ready";
    private const string HEALTHCHECK_LIVE_TAG = "live";

    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        AddServices(builder);
        var app = builder.Build();

        if (args.Contains("--migrate-database"))
        {
            MigrateDatabase(app);

            // the "--migrate-database" option allows a on-off process to migrate the database,
            // so dont run the app if it's present.
            return;
        }

        ConfigureRequestPipeline(app);
        app.Run();
    }

    public static void AddServices(WebApplicationBuilder builder)
    {
        AddDatabaseServices(builder);
        AddDataProtectionServices(builder);
        AddCachingServices(builder);
        AddFido2Services(builder);
        AddAuthServices(builder);
        AddOpenApiDocServices(builder);

        builder.Services.AddControllers();

        // app liveness healthcheck
        builder.Services.AddHealthChecks()
            .AddCheck(
                "liveness",
                () => HealthCheckResult.Healthy(),
                tags: new string[] { HEALTHCHECK_LIVE_TAG });
    }

    public static void AddDatabaseServices(WebApplicationBuilder builder)
    {
        builder.Services.AddDbContext<WebAuthnTestDbContext>(options =>
        {
            options
                .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTrackingWithIdentityResolution)
                .UseSqlServer(builder.Configuration.GetConnectionString("Default"));
        });

        // database readiness healthcheck
        builder.Services.AddHealthChecks()
            .AddDbContextCheck<WebAuthnTestDbContext>(
                tags: new string[] { "database", HEALTHCHECK_READY_TAG });
    }

    public static void AddDataProtectionServices(WebApplicationBuilder builder)
    {
        var dataProtectionBuilder = builder.Services
            .AddDataProtection()
            .PersistKeysToDbContext<WebAuthnTestDbContext>();

        // encrypt data protection keys in production using key vault
        if (!builder.Environment.IsDevelopment())
        {
            var keyUri = new Uri(Environment.GetEnvironmentVariable("KEY_VAULT_DATAPROTECTION_KEY_ID")!);
            var keyProps = new KeyProperties(keyUri);

            dataProtectionBuilder.ProtectKeysWithAzureKeyVault(
                keyProps.Id,
                new ManagedIdentityCredential());

            // key vault readiness healthcheck
            builder.Services.AddHealthChecks()
                .AddAzureKeyVault(
                    keyProps.VaultUri,
                    new ManagedIdentityCredential(),
                    options =>
                    {
                        options.AddKey(keyProps.Name);
                    },
                    tags: new string[] { "keyvault", HEALTHCHECK_READY_TAG });
        }
    }

    public static void AddOpenApiDocServices(WebApplicationBuilder builder)
    {
        // Swagger/OpenAPI docs. Learn more at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename));
        });
    }

    public static void AddCachingServices(WebApplicationBuilder builder)
    {
        builder.Services.AddMemoryCache();
        builder.Services.AddDistributedSqlServerCache(options =>
        {
            options.ConnectionString = builder.Configuration.GetConnectionString("Default");
            options.TableName = DistributedCacheEntryConstants.TableName;
            options.SchemaName = DistributedCacheEntryConstants.SchemaName;
        });
    }

    public static void AddFido2Services(WebApplicationBuilder builder)
    {
        //implemented as lambda functions to prevent exceptions when using ef tools during development
        static Uri frontendAppUri() => new(Environment.GetEnvironmentVariable("WEB_URL")!);
        static string frontendAppOrigin() => $"{frontendAppUri().Scheme}://{frontendAppUri().Authority}";

        // Session Middleware used for the WebAuthn ceremonies to store the challenge
        builder.Services.AddSession(options =>
        {
            options.IdleTimeout = TimeSpan.FromMinutes(5);
            options.IOTimeout = TimeSpan.FromSeconds(10);
            options.Cookie.Name = "WebAuthnTest-ChallengeCookie";
            options.Cookie.MaxAge = TimeSpan.FromMinutes(5);
            options.Cookie.IsEssential = true;
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        });

        // FIDO2 authentication - for registering and logging in with authenticator devices
        builder.Services
            .AddFido2(options =>
            {
                options.ServerDomain = frontendAppUri().Host;
                options.ServerName = "WebAuthn Test";
                options.Origins = new HashSet<string>(Enumerable.Repeat(frontendAppOrigin(), 1));
                options.TimestampDriftTolerance = 100;
            })
            .AddCachedMetadataService(options =>
            {
                //TODO: because the metadata service relies on the "less than ideal" memory cache above
                //there could be some performance degradation on startup.
                // Why?
                options.AddFidoMetadataRepository();
            });
    }

    public static void AddAuthServices(WebApplicationBuilder builder)
    {
        // Cookies are used for all request authentication (except WebAuthn initial login).
        builder.Services
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
            {
                options.SlidingExpiration = true;
                options.Cookie.Name = "WebAuthnTest-IdentityCookie";
                options.Cookie.IsEssential = true;
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Events.OnRedirectToAccessDenied = (context) =>
                {
                    //dont redirect, default cookie behavior is to redirect
                    //to accessDeniedPath
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToLogin = (context) =>
                {
                    //dont redirect to login, default cookie behavior is to redirect
                    //dont redirect, default cookie behavior is to redirect
                    //to loginPath
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToReturnUrl = (_) => Task.CompletedTask;
                options.Events.OnRedirectToLogout = (_) => Task.CompletedTask;
                //Note: This is a session cookie, would need something
                //a bit more secure/robust for a real application
            });

        //always require authenticated users to access API
        builder.Services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });
    }

    public static void ConfigureRequestPipeline(WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        if (!app.Environment.IsDevelopment())
        {
            //TODO: For an API, we dont really need this because there should only be an HTTPS binding.
            //Try to remove HTTP binding from Azure App Service.
            app.UseHttpsRedirection();
            app.UseHsts();
        }

        app.UseSession();

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();

        app.MapHealthChecks("/healthcheck/ready", new HealthCheckOptions
        {
            Predicate = healthCheck => healthCheck.Tags.Contains(HEALTHCHECK_READY_TAG),
            AllowCachingResponses = false
        })
        .RequireAuthorization();

        app.MapHealthChecks("/healthcheck/live", new HealthCheckOptions
        {
            Predicate = healthCheck => healthCheck.Tags.Contains(HEALTHCHECK_LIVE_TAG),
            AllowCachingResponses = false
        })
        .RequireAuthorization();
    }

    public static void WaitForDatabase(WebApplication app)
    {
        Console.WriteLine("Waiting for Database...");

        using (var serviceScope = app.Services.CreateScope())
        {
            var db = serviceScope
                .ServiceProvider
                .GetRequiredService<WebAuthnTestDbContext>()
                .Database;

            // try to connect to the database at most 10 times, increasing the retry time on
            // each attempt by 1 second
            var maxAttempts = 10;
            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                Thread.Sleep(attempt * 1000);
                if (db.CanConnect())
                {
                    Console.WriteLine("Database is ready!");
                    return;
                }
            }
        }

        throw new Exception("Could not connect to the database.");
    }

    public static void MigrateDatabase(WebApplication app)
    {
        Console.WriteLine("Migrating Database...");

        WaitForDatabase(app);

        using (var serviceScope = app.Services.CreateScope())
        {
            serviceScope
                .ServiceProvider
                .GetRequiredService<WebAuthnTestDbContext>()
                .Database
                .Migrate();
        }

        Console.WriteLine("Database migration complete.");
    }
}
