using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using GSA.FM.Utility.Core.Interfaces;
using GSA.FM.Utility.Core.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace ASP_Core_MVC_Template
{
    public class Startup
    {
        public Startup(IConfiguration configuration, IWebHostEnvironment hostingEnvironment)
        {
            Configuration = configuration;
            _hostingEnvironment = hostingEnvironment;
        }

        internal static IConfiguration Configuration { get; private set; }
        private readonly IWebHostEnvironment _hostingEnvironment;

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // Initialize the Utility Config Service as a singleton.
            services.AddSingleton<IFMUtilityConfigService>(client => new FMUtilityConfigService(client.GetService<ILogger<FMUtilityConfigService>>()));

            // Initialize the Utility password service as a singleton.
            services.AddHttpClient<IFMUtilityPasswordService>("FMUtilityPasswordService").ConfigurePrimaryHttpMessageHandler(() => {
                var handler = new HttpClientHandler();
                // Disable SSL check on Password Manager Pro http requests.
                // This is because the PMP servers use self-signed certs and non-FQDN.
                // We will undo this if FQDN's are added in the future.
                handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => { return true; };
                return handler;
            });
            services.AddSingleton<IFMUtilityPasswordService>(client => new FMUtilityPasswordService(client.GetService<ILogger<FMUtilityPasswordService>>(),
                    client.GetService<IHttpClientFactory>()));

            // Add Cache services
            services.AddMemoryCache();

            // Initialize the Utility audit service as a singleton.
            services.AddSingleton<IFMUtilityAuditService>(client => new FMUtilityAuditService(client.GetService<ILogger<FMUtilityAuditService>>(),
                client.GetService<IFMUtilityConfigService>(), client.GetService<IFMUtilityPasswordService>(),
                Configuration["Databases:Audit:Database"], Configuration["Databases:Audit:Username"]));

            // Initialize the FM Data API service as a singleton.
            var dataAPIURL = Configuration["FMDataAPIURL"];
            services.AddHttpClient<IFMUtilityDataAPIService>("FMUtilityDataAPIService", client =>
            {
                // Tell the service where we expect the FM Data API to be found.
                client.BaseAddress = new Uri(dataAPIURL);
            })
                .ConfigurePrimaryHttpMessageHandler(() =>
                {
                    var handler = new HttpClientHandler();
                    // Don't use cookies at the handler level. We set the authorization cookie at the client.
                    handler.UseCookies = false;
                    // Disable SSL check on FM Data API requests because it will be running on the same server as our app.
                    // This must be removed if the API is running on a separate server.
                    handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => { return true; };
                    return handler;
                });
            services.AddSingleton<IFMUtilityDataAPIService>(client => new FMUtilityDataAPIService(client.GetService<ILogger<FMUtilityDataAPIService>>(),
                client.GetService<IConfiguration>(), client.GetService<IHttpClientFactory>(),
                dataAPIURL.Contains("localhost")));

            // Configure cookie policy.
            services.Configure<CookiePolicyOptions>(options =>
            {
                options.Secure = CookieSecurePolicy.Always;
            });

            // Allow custom components to get to the HTTP context (i.e., our dbContext).
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

            // Cookie authentication
            services.AddAuthentication(options =>
            {
                options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            }).AddCookie(options =>
            {
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.Cookie.Name = Configuration["SharedCookieName"];
                options.LoginPath = "/Home/Index";
                options.AccessDeniedPath = "/Home/Index"; // Point this to a custom view to give the user more detail.
            });

            // Let configuration be injected.
            services.AddSingleton(Configuration);

            // Add the localization services to the services container
            services.AddLocalization(options => options.ResourcesPath = "Resources");

            // Configure supported cultures and localization options
            services.Configure<RequestLocalizationOptions>(options =>
            {
                var supportedCultures = new[]
                {
                    new CultureInfo("en-US")
                };

                // State what the default culture for your application is. This will be used if no specific culture
                // can be determined for a given request.
                options.DefaultRequestCulture = new RequestCulture(culture: "en-US", uiCulture: "en-US");

                // You must explicitly state which cultures your application supports.
                // These are the cultures the app supports for formatting numbers, dates, etc.
                options.SupportedCultures = supportedCultures;

                // These are the cultures the app supports for UI strings, i.e. we have localized resources for.
                options.SupportedUICultures = supportedCultures;
            });

            services.AddControllersWithViews()
                // Add support for finding localized views, based on file name suffix, e.g. Index.fr.cshtml
                .AddViewLocalization(LanguageViewLocationExpanderFormat.Suffix)
                // Add support for localizing strings in data annotations (e.g. validation messages) via the
                // IStringLocalizer abstractions.
                .AddDataAnnotationsLocalization()
                .AddNewtonsoftJson(options =>
                {
                    options.SerializerSettings.ContractResolver = new DefaultContractResolver();
                    options.SerializerSettings.Formatting = Formatting.Indented;
                })
                .AddSessionStateTempDataProvider();

            // Allow controllers to access the http context.
            services.AddHttpContextAccessor();

            // Add Kendo UI services to the services container
            services.AddKendo();

            // Set session timeout.
            double sessionTimeoutSeconds = Int32.Parse(Configuration["CookieAuthentication:ExpireMinutes"]) * 60;
            services.ConfigureApplicationCookie(options =>
            {
                options.Cookie.Expiration = TimeSpan.FromSeconds(sessionTimeoutSeconds);
                options.ExpireTimeSpan = TimeSpan.FromSeconds(sessionTimeoutSeconds);
                options.SlidingExpiration = false;
            });

            // Add support for sessions.
            services.AddSession(options =>
            {
                options.Cookie.Name = ".Core-Web-Template.Session";
                options.IdleTimeout = TimeSpan.FromSeconds(sessionTimeoutSeconds);
                options.Cookie.IsEssential = true;
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env,
            ILoggerFactory loggerFactory)
        {
            var logDirectory = Environment.GetEnvironmentVariable("LOGFILE_DIRECTORY");
            if (logDirectory == null) logDirectory = "Logs";
            loggerFactory.AddFile(logDirectory + "/Core-Web-Template-{Date}.log");

            app.UsePathBase("/core-web");

            var locOptions = app.ApplicationServices.GetService<IOptions<RequestLocalizationOptions>>();
            app.UseRequestLocalization(locOptions.Value);

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseRouting();
            app.UseCookiePolicy();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseSession();
            app.UseEndpoints(endpoints =>
            {
                // We need MapControllers() to map routes that don't have resulting views (such as redirects).
                // These must be decorated with [Route()] attributes in the controller.
                endpoints.MapControllers();

                // Default route.
                endpoints.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}
