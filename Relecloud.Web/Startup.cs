using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Relecloud.Web.Infrastructure;
using Relecloud.Web.Services;
using Relecloud.Web.Services.AzureSearchService;
using Relecloud.Web.Services.EventBusEventSenderService;
using Relecloud.Web.Services.SqlDatabaseEventRepository;

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
            });

            // Add custom services.
            var redisCacheConnection = Configuration.GetValue<string>("App:RedisCache:ConnectionString");
            if (!string.IsNullOrWhiteSpace(redisCacheConnection))
            {
                // If we have a connection string to Redis, use that as the distributed cache.
                // If not, ASP.NET Core automatically injects an in-memory cache.
                services.AddDistributedRedisCache(options => { options.Configuration = redisCacheConnection; });
            }
            services.AddDbContextPool<ConcertDataContext>(options => options.UseSqlServer(Configuration.GetValue<string>("App:SqlDatabase:ConnectionString")));
            services.AddScoped<IConcertRepository, SqlDatabaseConcertRepository>();
            services.AddScoped<IConcertSearchService>(x => new AzureSearchConcertSearchService(Configuration.GetValue<string>("App:AzureSearch:ServiceName"), Configuration.GetValue<string>("App:AzureSearch:AdminKey"), Configuration.GetValue<string>("App:SqlDatabase:ConnectionString")));
            services.AddScoped<IEventSenderService>(x => new EventBusEventSenderService(Configuration.GetValue<string>("App:ServiceBus:ConnectionString"), Configuration.GetValue<string>("App:ServiceBus:QueueName")));

            // Add authentication.
            services.Configure<AuthenticationConfiguration>(Configuration.GetSection("App:Authentication"));
            services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options => options.LoginPath = new PathString("/account/signin"))
                .AddOpenIdConnect(/* The options are more complex and are therefore configured separately using IConfigureOptions for clarity */);
            services.AddSingleton<IConfigureOptions<OpenIdConnectOptions>, OpenIdConnectOptionsSetup>();

            // The ApplicationInitializer is injected in the Configure method with all its dependencies and will ensure
            // they are all properly initialized upon construction.
            services.AddScoped<ApplicationInitializer, ApplicationInitializer>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ApplicationInitializer applicationInitializer)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseBrowserLink();
            }
            else
            {
                app.UseExceptionHandler("/home/error");
            }

            app.UseStaticFiles();

            app.UseAuthentication();

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=home}/{action=index}/{id?}");
            });
        }
    }
}