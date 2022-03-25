﻿using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using Relecloud.Web.Infrastructure;
using Relecloud.Web.Models;
using Relecloud.Web.Services;
using Relecloud.Web.Services.AzureSearchService;
using Relecloud.Web.Services.DummyServices;
using Relecloud.Web.Services.SqlDatabaseEventRepository;
using Relecloud.Web.Services.StorageAccountEventSenderService;
using System.Security.Claims;

namespace Relecloud.Web
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // Add framework services.
            services.AddMvc();
            services.AddRouting(options => options.LowercaseUrls = true);

            services.Configure<MvcOptions>(options =>
            {
                options.Filters.Add(new RequireHttpsAttribute());
                options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
            });

            // Retrieve application settings.
            var redisCacheConnectionString = Configuration.GetValue<string>("App:RedisCache:ConnectionString");
            var sqlDatabaseConnectionString = Configuration.GetValue<string>("App:SqlDatabase:ConnectionString");
            var azureSearchServiceName = Configuration.GetValue<string>("App:AzureSearch:ServiceName");
            var azureSearchAdminKey = Configuration.GetValue<string>("App:AzureSearch:AdminKey");
            var storageAccountConnectionString = Configuration.GetValue<string>("App:StorageAccount:ConnectionString");
            var storageAccountEventQueueName = Configuration.GetValue<string>("App:StorageAccount:EventQueueName");
            var applicationInsightsConnectionString = Configuration.GetValue<string>("App:ApplicationInsights:ConnectionString");
            var cdnUrlString = Configuration.GetValue<string>("App:Cdn:Url");
            var cdnUrl = default(Uri);
            if (Uri.TryCreate(cdnUrlString, UriKind.Absolute, out cdnUrl))
            {
                ExtensionMethods.CdnUrl = cdnUrl;
            }

            // Add custom services.
            if (!string.IsNullOrWhiteSpace(redisCacheConnectionString))
            {
                // If we have a connection string to Redis, use that as the distributed cache.
                // If not, ASP.NET Core automatically injects an in-memory cache.
                services.AddDistributedRedisCache(options => { options.Configuration = redisCacheConnectionString; });
            }
            if (string.IsNullOrWhiteSpace(sqlDatabaseConnectionString))
            {
                // Add a dummy concert repository in case the Azure SQL Database isn't provisioned and configured yet.
                services.AddScoped<IConcertRepository, DummyConcertRepository>();
                // Add a dummy concert search service as well since the Azure Search service needs the Azure SQL Database.
                services.AddScoped<IConcertSearchService, DummyConcertSearchService>();
            }
            else
            {
                // Add a concert repository based on Azure SQL Database.
                services.AddDbContextPool<ConcertDataContext>(options => options.UseSqlServer(sqlDatabaseConnectionString));
                services.AddScoped<IConcertRepository, SqlDatabaseConcertRepository>();
                if (string.IsNullOrWhiteSpace(azureSearchServiceName) || string.IsNullOrWhiteSpace(azureSearchAdminKey))
                {
                    // Add a dummy concert search service in case the Azure Search service isn't provisioned and configured yet.
                    services.AddScoped<IConcertSearchService, DummyConcertSearchService>();
                }
                else
                {
                    // Add a concert search service based on Azure Search.
                    services.AddScoped<IConcertSearchService>(x => new AzureSearchConcertSearchService(azureSearchServiceName, azureSearchAdminKey, sqlDatabaseConnectionString));
                }
            }
            if (string.IsNullOrWhiteSpace(storageAccountConnectionString) || string.IsNullOrWhiteSpace(storageAccountEventQueueName))
            {
                // Add a dummy event sender service in case the Azure Storage account isn't provisioned and configured yet.
                services.AddScoped<IEventSenderService, DummyEventSenderService>();
            }
            else
            {
                // Add an event sender service based on Azure Storage.
                services.AddScoped<IEventSenderService>(x => new StorageAccountEventSenderService(storageAccountConnectionString, storageAccountEventQueueName));
            }

            // Add authentication if configured.
            if (!string.IsNullOrWhiteSpace(Configuration.GetValue<string>("AzureAdB2C:ClientId")))
            {
                AddAzureAdB2cServices(services);
            }

            if (string.IsNullOrWhiteSpace(applicationInsightsConnectionString))
            {
                services.AddApplicationInsightsTelemetry();
            }
            else
            {
                services.AddApplicationInsightsTelemetry(applicationInsightsConnectionString);
            }

            // The ApplicationInitializer is injected in the Configure method with all its dependencies and will ensure
            // they are all properly initialized upon construction.
            services.AddScoped<ApplicationInitializer, ApplicationInitializer>();

            // Add support for session state.
            // NOTE: If there is a distibuted cache service (e.g. Redis) then this will be used to store session data.
            services.AddSession();
        }

        private void AddAzureAdB2cServices(IServiceCollection services)
        {
            services.AddRazorPages().AddMicrosoftIdentityUI();
            services.AddMicrosoftIdentityWebAppAuthentication(Configuration, Constants.AzureAdB2C);

            services.Configure<OpenIdConnectOptions>(Configuration.GetSection("AzureAdB2C"));
            services.Configure((Action<MicrosoftIdentityOptions>)(options =>
            {
                options.Events = new OpenIdConnectEvents
                {
                    OnTokenValidated = async ctx =>
                    {
                        TransformRoleClaims(ctx);
                        await CreateOrUpdateUserInformation(ctx);
                    }
                };
            }));
        }

        private static async Task CreateOrUpdateUserInformation(TokenValidatedContext ctx)
        {
            try
            {
                if (ctx.Principal?.Identity is not null)
                {
                    // The user has signed in, ensure the information in the database is up-to-date.
                    var user = new User
                    {
                        Id = ctx.Principal.GetUniqueId(),
                        DisplayName = ctx.Principal.Identity.Name ?? "New User"
                    };

                    var repository = ctx.HttpContext.RequestServices.GetRequiredService<IConcertRepository>();
                    await repository.CreateOrUpdateUserAsync(user);
                }
            }
            catch (Exception ex)
            {
                var logger = ctx.HttpContext.RequestServices.GetRequiredService<ILogger<Startup>>();
                logger.LogError(ex, "Unhandled exception from Startup.TransformRoleClaims");
            }
        }

        private static void TransformRoleClaims(TokenValidatedContext ctx)
        {
            try
            {
                const string RoleClaim = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role";
                if (ctx.Principal?.Identity is not null)
                {
                    // Find all claims of the requested claim type, split their values by spaces
                    // and then take the ones that aren't yet on the principal individually.
                    var claims = ctx.Principal.FindAll("extension_AppRoles")
                    .SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                    .Where(s => !ctx.Principal.HasClaim(RoleClaim, s)).ToList();

                    // Add all new claims to the principal's identity.
                    ((ClaimsIdentity)ctx.Principal.Identity).AddClaims(claims.Select(s => new Claim(RoleClaim, s)));
                }
            }
            catch (Exception ex)
            {
                var logger = ctx.HttpContext.RequestServices.GetRequiredService<ILogger<Startup>>();
                logger.LogError(ex, "Unhandled exception from Startup.TransformRoleClaims");
            }
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(WebApplication app)
        {
            using var serviceScope = app.Services.CreateScope();
            serviceScope.ServiceProvider.GetService<ApplicationInitializer>();


            if (app.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/home/error");
            }

            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseSession();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
                endpoints.MapRazorPages();
            });
        }
    }
}