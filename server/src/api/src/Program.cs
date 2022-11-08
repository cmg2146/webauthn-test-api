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
                policy.WithOrigins(Environment.GetEnvironmentVariable("APP_URL")!);
            });
        });

        builder.Services.AddControllers();
        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        builder.Services
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
            {
                options.SlidingExpiration = true;
                options.Cookie.Name = Environment.GetEnvironmentVariable("AUTH_COOKIE_NAME");
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
            options.Cookie.Name = "WebAuthnOptionsCookies";
            options.Cookie.IsEssential = true;
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        });

        builder.Services.AddFido2(options =>
        {
            options.ServerDomain = new Uri(Environment.GetEnvironmentVariable("APP_URL")!).Host;
            options.ServerName = "WebAuthn Test";
            options.Origins.Add(Environment.GetEnvironmentVariable("APP_URL"));
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
