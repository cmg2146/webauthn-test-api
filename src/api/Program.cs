namespace WebAuthnTest.Api;

using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Azure.Identity;
using WebAuthnTest.Database;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        AddServices(builder);
        var app = builder.Build();

        // always wait for database before starting the app
        WaitForDatabase(app);

        var hasMigrateDatabaseOption = args.Contains("--migrate-database");
        if (hasMigrateDatabaseOption || app.Environment.IsDevelopment())
        {
            MigrateDatabase(app);

            // the "--migrate-database" option allows a on-off process to migrate the database,
            // so dont run the app if it's present.
            if (hasMigrateDatabaseOption)
            {
                return;
            }
        }

        ConfigureRequestPipeline(app);
        app.Run();
    }

    public static void AddServices(WebApplicationBuilder builder)
    {
        //implemented as lambda functions to prevent exceptions when using ef tools during development
        static Uri frontendAppUri() => new(Environment.GetEnvironmentVariable("WEB_URL")!);
        static string frontendAppOrigin() => $"{frontendAppUri().Scheme}://{frontendAppUri().Authority}";

        // Database
        builder.Services.AddDbContext<WebAuthnTestDbContext>(options =>
        {
            options
                .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTrackingWithIdentityResolution)
                .UseSqlServer(builder.Configuration.GetConnectionString("Default"));
        });

        // ASP.NET Core Data Protection
        var dataProtectionBuilder = builder.Services
            .AddDataProtection()
            .PersistKeysToDbContext<WebAuthnTestDbContext>();

        // encrypt data protection keys in production using key vault
        if (!builder.Environment.IsDevelopment())
        {
            dataProtectionBuilder
                .ProtectKeysWithAzureKeyVault(
                    new Uri(Environment.GetEnvironmentVariable("AZURE_KEY_VAULT_ID")!),
                    new ManagedIdentityCredential());
        }

        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.WithOrigins(frontendAppOrigin());
            });
        });

        builder.Services.AddControllers();

        // Swagger/OpenAPI docs. Learn more at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename));
        });

        //caches used by Session and Fido2 middleware
        builder.Services.AddMemoryCache();
        builder.Services.AddDistributedSqlServerCache(options =>
        {
            options.ConnectionString = builder.Configuration.GetConnectionString("Default");
            options.TableName = DistributedCacheEntryConstants.TableName;
            options.SchemaName = DistributedCacheEntryConstants.SchemaName;
        });

        // Session Middleware
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
                options.AddFidoMetadataRepository();
            });

        // For all request authentication (except initial login) - Cookies
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

        app.UseCors();

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();
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

            // try to connect to the database at most 20 times, increasing the retry time on
            // each attempt
            var maxAttempts = 20;
            var connected = false;
            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                Thread.Sleep(attempt * 1000);
                if (db.CanConnect())
                {
                    connected = true;
                    break;
                }
            }

            if (!connected)
            {
                throw new Exception("Could not connect to the database.");
            }
        }

        Console.WriteLine("Database is ready!");
    }

    public static void MigrateDatabase(WebApplication app)
    {
        Console.WriteLine("Migrating Database...");

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
