using Relecloud.Web;

var builder = WebApplication.CreateBuilder(args);

var startup = new Startup(builder.Configuration);

// Add services to the container.
startup.ConfigureServices(builder.Services);

var app = builder.Build();

startup.Configure(app);

app.Run();