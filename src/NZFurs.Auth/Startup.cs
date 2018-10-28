using IdentityServer4.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using NZFurs.Auth.Data;
using NZFurs.Auth.Models;
using NZFurs.Auth.Options;
using NZFurs.Auth.Services;
using System;
using System.Reflection;

namespace NZFurs.Auth
{
    public class Startup
    {
        public IConfiguration Configuration { get; }
        public IHostingEnvironment Environment { get; }

        public Startup(IConfiguration configuration, IHostingEnvironment environment)
        {
            Configuration = configuration;
            Environment = environment;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            // ConfigureServices becomes a mess pretty quickly, so gonna use some regions
            // Not even sorry. #fiteme

            #region Options
            services.AddOptions();
            services.Configure<Argon2iPasswordHasherOptions>(Configuration.GetSection("Argon2i"));
            services.Configure<AzureKeyVaultKeyServiceOptions>(Configuration.GetSection("Azure:KeyVault"));
            services.Configure<AzureKeyVaultKeyServiceOptions>(Configuration.GetSection("Azure:ActiveDirectory"));
            #endregion

            #region DbContext
            string connectionString = Configuration.GetConnectionString("DefaultConnection");
            var migrationsAssembly = typeof(Startup).GetTypeInfo().Assembly.GetName().Name;

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlite(connectionString));
            #endregion

            #region Identity
            services.AddIdentity<ApplicationUser, IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultTokenProviders();
            #endregion

            #region Application Services
            services.AddTransient<IPasswordHasher<ApplicationUser>, Argon2iPasswordHasher<ApplicationUser>>();
            services.AddScoped<IKeyMaterialService, AzureKeyVaultKeyService>();
            services.AddScoped<ITokenCreationService, AzureKeyVaultKeyService>();
            #endregion

            #region MVC
            services.AddMvc();
            #endregion

            #region IIS
            services.Configure<IISOptions>(iis =>
            {
                iis.AuthenticationDisplayName = "Windows";
                iis.AutomaticAuthentication = false;
            });
            #endregion

            #region IdentityServer4
            var builder = services.AddIdentityServer(options =>
            {
                options.Events.RaiseErrorEvents = true;
                options.Events.RaiseInformationEvents = true;
                options.Events.RaiseFailureEvents = true;
                options.Events.RaiseSuccessEvents = true;
            })
            .AddAspNetIdentity<ApplicationUser>()
            // this adds the config data from DB (clients, resources)
            .AddConfigurationStore(options =>
            {
                options.ConfigureDbContext = b =>
                    b.UseSqlite(connectionString,
                        sql => sql.MigrationsAssembly(migrationsAssembly));
            })
            // this adds the operational data from DB (codes, tokens, consents)
            .AddOperationalStore(options =>
            {
                options.ConfigureDbContext = b =>
                    b.UseSqlite(connectionString,
                        sql => sql.MigrationsAssembly(migrationsAssembly));

                // this enables automatic token cleanup. this is optional.
                options.EnableTokenCleanup = true;
                // options.TokenCleanupInterval = 15; // frequency in seconds to cleanup stale grants. 15 is useful during debugging
            });
            #endregion

            #region Security Headers
            services.AddHsts(options =>
            {
                options.Preload = true;
                options.IncludeSubDomains = true;
                options.MaxAge = TimeSpan.FromDays(365);
            });

            services.AddHttpsRedirection(options =>
            {
                options.RedirectStatusCode = StatusCodes.Status301MovedPermanently;
                options.HttpsPort = 443;
            });
            #endregion

            #region Authentication
            services.AddAuthentication()
            .AddGoogle(options =>
            {
                options.ClientId = Configuration["Authentication:Google:ClientId"];
                options.ClientSecret = Configuration["Authentication:Google:ClientSecret"];
            })
            .AddTwitter(options =>
            {
                options.ConsumerKey = Configuration["Authentication:Twitter:ConsumerKey"];
                options.ConsumerSecret = Configuration["Authentication:Twitter:ConsumerSecret"];
            });
            #endregion
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            // TODO: Watch https://github.com/aspnet/BasicMiddleware/issues/323
            app.Use(async (context, next) =>
            {
                context.Response.Headers.Add("X-Frame-Options", "deny");
                context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
                context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
                await next();
            });

            if (env.IsDevelopment())
            {
                // Development
                app.UseDeveloperExceptionPage();
                app.UseDatabaseErrorPage();
            }
            else
            {
                // Production
                app.UseExceptionHandler("/Home/Error");
                app.UseHttpsRedirection();
                app.UseHsts();
            }

            app.UseStaticFiles();
            app.UseIdentityServer();
            app.UseMvcWithDefaultRoute();
        }
    }
}
