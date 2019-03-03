﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using IdentityServer4.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using NZFurs.Auth.Data;
using NZFurs.Auth.Filters;
using NZFurs.Auth.Models;
using NZFurs.Auth.Options;
using NZFurs.Auth.Resources;
using NZFurs.Auth.Services;

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
            services.Configure<SendGridOptions>(Configuration.GetSection("SendGrid"));
            #endregion

            #region DbContext
            services.AddDbContext<ApplicationDbContext>(ConfigureEfProvider);
            #endregion

            #region Localisation
            services.AddSingleton<LocService>();
            services.AddLocalization(options => options.ResourcesPath = "Resources");
            services.Configure<RequestLocalizationOptions>(
                options =>
                {
                    var supportedCultures = new List<CultureInfo>
                        {
                            new CultureInfo("en-US"),
                            new CultureInfo("de-DE"),
                            new CultureInfo("de-CH"),
                            new CultureInfo("it-IT"),
                            new CultureInfo("gsw-CH"),
                            new CultureInfo("fr-FR"),
                            new CultureInfo("zh-Hans")
                        };

                    options.DefaultRequestCulture = new RequestCulture(culture: "de-DE", uiCulture: "de-DE");
                    options.SupportedCultures = supportedCultures;
                    options.SupportedUICultures = supportedCultures;

                    var providerQuery = new LocalizationQueryProvider
                    {
                        QureyParamterName = "ui_locales"
                    };

                    options.RequestCultureProviders.Insert(0, providerQuery);
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

            #region Identity
            services.AddIdentity<ApplicationUser, IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddErrorDescriber<StsIdentityErrorDescriber>()
                .AddDefaultTokenProviders();
            #endregion

            #region MVC
            services
                .AddMvc(options =>
                {
                    options.Filters.Add(new SecurityHeadersAttribute());
                })
                .SetCompatibilityVersion(CompatibilityVersion.Version_2_2)
                .AddViewLocalization()
                .AddDataAnnotationsLocalization(options =>
                {
                    options.DataAnnotationLocalizerProvider = (type, factory) =>
                    {
                        var assemblyName = new AssemblyName(typeof(SharedResource).GetTypeInfo().Assembly.FullName);
                        return factory.Create("SharedResource", assemblyName.Name);
                    };
                });
            #endregion

            #region Application Services
            //services.AddTransient<IEmailSender, SendGridEmailSender>();
            services.AddTransient<IEmailSender, FakeEmailService>();
            services.AddTransient<IPasswordHasher<ApplicationUser>, Argon2iPasswordHasher<ApplicationUser>>();
            services.AddScoped<IKeyMaterialService, AzureKeyVaultKeyService>();
            services.AddScoped<ITokenCreationService, AzureKeyVaultKeyService>();

            //services.AddScoped<IUserClaimsPrincipalFactory<ApplicationUser>, ApplicationUserClaimsPrincipalFactory>();
            services.AddTransient<IProfileService, IdentityWithAdditionalClaimsProfileService>(); // QUESTION: What is this for? Some IS4 thing...
            #endregion

            #region Cookie Policy
            services.ConfigureApplicationCookie(options =>
            {
                options.LoginPath = $"/Account/Login";
                options.LogoutPath = $"/Account/Logout";
                options.AccessDeniedPath = $"/Account/AccessDenied";
            });

            services.Configure<CookiePolicyOptions>(options =>
            {
                // This lambda determines whether user consent for non-essential cookies is needed for a given request.
                options.CheckConsentNeeded = context => false;
                options.MinimumSameSitePolicy = SameSiteMode.None;
            });
            #endregion

            #region IdentityServer4
            services.AddIdentityServer()
                .AddAspNetIdentity<ApplicationUser>()
                .AddConfigurationStore(options =>
                {
                    options.ConfigureDbContext = ConfigureEfProvider;
                })
                // this adds the operational data from DB (codes, tokens, consents)
                .AddOperationalStore(options =>
                {
                    options.ConfigureDbContext = ConfigureEfProvider;

                    // this enables automatic token cleanup. this is optional.
                    options.EnableTokenCleanup = true;
                    // options.TokenCleanupInterval = 15; // frequency in seconds to cleanup stale grants. 15 is useful during debugging
                })
                .AddProfileService<IdentityWithAdditionalClaimsProfileService>();
            #endregion

            #region HTTPS/HSTS
            services.AddHsts(options =>
            {
                options.Preload = true;
                options.IncludeSubDomains = true;
                options.MaxAge = TimeSpan.FromDays(365);
            });

            services.AddHttpsRedirection(options =>
            {
                options.RedirectStatusCode = StatusCodes.Status301MovedPermanently;
                options.HttpsPort = 5001;
            });
            #endregion
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseDatabaseErrorPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();

            app.UseXContentTypeOptions();
            app.UseReferrerPolicy(opts => opts.NoReferrer());
            app.UseXXssProtection(options => options.EnabledWithBlockMode());

            app.UseCsp(opts => opts
                .BlockAllMixedContent()
                .StyleSources(s => s.Self())
                .StyleSources(s => s.UnsafeInline())
                .FontSources(s => s.Self())
                .FrameAncestors(s => s.Self())
                .ImageSources(imageSrc => imageSrc.Self())
                .ImageSources(imageSrc => imageSrc.CustomSources("data:"))
                .ScriptSources(s => s.Self())
                .ScriptSources(s => s.UnsafeInline()) // TODO: HELL NO
            );

            var locOptions = app.ApplicationServices.GetService<IOptions<RequestLocalizationOptions>>();
            app.UseRequestLocalization(locOptions.Value);

            app.UseStaticFiles(new StaticFileOptions
            {
                OnPrepareResponse = context =>
                {
                    if (context.Context.Response.Headers["feature-policy"].Count == 0)
                    {
                        var featurePolicy = "accelerometer 'none'; camera 'none'; geolocation 'none'; gyroscope 'none'; magnetometer 'none'; microphone 'none'; payment 'none'; usb 'none'";

                        context.Context.Response.Headers["feature-policy"] = featurePolicy;
                    }

                    if (context.Context.Response.Headers["X-Content-Security-Policy"].Count == 0)
                    {
                        var csp = "script-src 'self';style-src 'self';img-src 'self' data:;font-src 'self';form-action 'self';frame-ancestors 'self';block-all-mixed-content";
                        // IE
                        context.Context.Response.Headers["X-Content-Security-Policy"] = csp;
                    }
                }
            });
            app.UseIdentityServer();

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");
            });
        }

        private void ConfigureEfProvider(DbContextOptionsBuilder options)
        {
            var migrationsAssembly = typeof(Startup).GetTypeInfo().Assembly.GetName().Name;

            switch (Configuration.GetValue("Data:Database:Provider", string.Empty).ToLowerInvariant())
            {
                case "mysql":
                case "mariadb":
                    throw new NotSupportedException(@"MySQL/MariaDB is not currently supported.");
                case "postgres":
                case "postgresql":
                    var connectionString =
                        Configuration.GetValue<string>("Data:Database:ConnectionString", null)
                        ?? new NpgsqlConnectionStringBuilder
                        {
                            Database = Configuration.GetValue<string>("Data:Database:Database"),
                            Host = Configuration.GetValue<string>("Data:Database:Host"),
                            Port = Configuration.GetValue("Data:Database:Port", 5432),
                            Username = Configuration.GetValue("Data:Database:Username", string.Empty),
                            Password = Configuration.GetValue("Data:Database:Password", string.Empty),
                            SslMode = Configuration.GetValue<bool>("Data:Database:RequireSSL", false)
                                ? SslMode.Require
                                : SslMode.Prefer,
                        }.ToString();

                    options.UseNpgsql(connectionString, sql => sql.MigrationsAssembly(migrationsAssembly));

                    break;
                case "oracle":
                    throw new NotSupportedException(@"Ha, no. Oracle can fuck right off. https://web.archive.org/web/20150811052336/https://blogs.oracle.com/maryanndavidson/entry/no_you_really_can_t");
                case "sqlserver":
                    throw new NotSupportedException(@"Microsoft SQL Server is not currently supported. This may come in a future release because it's easy to implement, but it may be buggy and won't be officially maintained.");
                case "sqlite":
                default:
                    var databasePath = new DirectoryInfo(Path.Combine("data", "database"));
                    if (!databasePath.Exists)
                        databasePath.Create();

                    var connectionStringBuilder = new SqliteConnectionStringBuilder
                    {
                        DataSource = Path.Combine(
                            Directory.GetCurrentDirectory(),
                            Configuration.GetValue("Paths:Database", "data/database"),
                            Configuration.GetValue("Data:Database:Filename", "data.db"))
                    };
                    options.UseSqlite(connectionStringBuilder.ToString());
                    break;
            }
        }
    }
}
