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
        //these are lambda functions to prevent exceptions when using ef tools during development
        Func<Uri> frontendAppUri = () => new Uri(Environment.GetEnvironmentVariable("WEB_URL")!);
        Func<string> frontendAppOrigin = () => $"{frontendAppUri().Scheme}://{frontendAppUri().Authority}";
        
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddDbContext<WebAuthnTestDbContext>(
            options => options
                .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTrackingWithIdentityResolution)
                .UseSqlServer(builder.Configuration.GetConnectionString("Default"))
        );

        var dataProtectionBuilder = builder.Services
            .AddDataProtection()
            .PersistKeysToDbContext<WebAuthnTestDbContext>();

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
        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename));
        });

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
                options.Events.OnRedirectToLogin = (context) =>
                {
                    //dont redirect to login, default cookie behavior is to redirect
                    //to loginPath
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };                
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

        //session used for WebAuthn Attestation and Assertion options
        builder.Services.AddMemoryCache();
        builder.Services.AddDistributedMemoryCache();
        builder.Services.AddSession(options =>
        {
            options.IdleTimeout = TimeSpan.FromMinutes(5);
            options.Cookie.Name = "WebAuthnTest-ChallengeCookie";
            options.Cookie.MaxAge = TimeSpan.FromMinutes(5);
            options.Cookie.IsEssential = true;
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        });

        builder.Services.AddFido2(options =>
        {
            options.ServerDomain = frontendAppUri().Host;
            options.ServerName = "WebAuthn Test";
            options.Origins = new HashSet<string>(Enumerable.Repeat(frontendAppOrigin(), 1));
            options.TimestampDriftTolerance = 100;
        });

        var app = builder.Build();
        
        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        if (!app.Environment.IsDevelopment())
        {
            app.UseHttpsRedirection();
            app.UseHsts();
        }

        app.UseSession();

        app.UseCors();
        
        app.UseAuthentication();
        app.UseAuthorization();
        
        app.MapControllers();
        
        using (var serviceScope = app.Services.CreateScope())
        {
            serviceScope
                .ServiceProvider
                .GetRequiredService<WebAuthnTestDbContext>()
                .Database
                .Migrate();
        }

        app.Run();
    }
}
