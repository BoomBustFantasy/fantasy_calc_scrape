using FantasyCalcScrape.Jobs;
using FantasyCalcScrape.Services;
using FantasyCalcScrape.Services.Interfaces;
using FantasyCalcScrape.HealthChecks;
using FantasyCalcScrape.Configuration;
using FantasyCalcScrape.Models;
using Quartz;
using Serilog;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File("logs/fantasy-calc-scrape-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    Log.Information("Starting Fantasy Calc Scrape Service");

    var builder = WebApplication.CreateBuilder(args);

    // Add Serilog
    builder.Services.AddSerilog();

    // Configure cache settings
    builder.Services.Configure<CacheConfiguration>(builder.Configuration.GetSection("Cache"));

    // Configure resilience settings
    builder.Services.Configure<ResilienceConfiguration>(builder.Configuration.GetSection("Resilience"));

    // Configure logging settings
    builder.Services.Configure<LoggingConfiguration>(builder.Configuration.GetSection("Logging"));

    // Register services
    builder.Services.AddScoped<IFantasyCalcApiService, FantasyCalcApiService>();
    builder.Services.AddScoped<ISupabaseDatabaseService, SupabaseDatabaseService>();    // Add HTTP client for Fantasy Calc API
    builder.Services.AddHttpClient<FantasyCalcApiService>(client =>
    {
        client.BaseAddress = new Uri("https://api.fantasycalc.com/");
        client.Timeout = TimeSpan.FromSeconds(30);
        client.DefaultRequestHeaders.Add("User-Agent", "FantasyCalcScraper/1.0");
        client.DefaultRequestHeaders.Add("Accept", "application/json");
    });

    // Add ASP.NET Core services for diagnostic endpoints
    builder.Services.AddControllers();

    // Add HTTP client for health checks
    builder.Services.AddHttpClient<FantasyCalcApiHealthCheck>();

    // Add health checks
    builder.Services.AddHealthChecks()
        .AddCheck<FantasyCalcApiHealthCheck>("fantasy_calc_api", tags: new[] { "fantasy_calc", "api" });

    // Add Quartz
    builder.Services.AddQuartz(q =>
    {
        // Create a "key" for the Fantasy Calc Values job
        var valuesJobKey = new JobKey("FantasyCalcValuesJob");

        // Register the Fantasy Calc Values job with the DI container
        q.AddJob<FantasyCalcValuesJob>(opts => opts
            .WithIdentity(valuesJobKey)
            .DisallowConcurrentExecution()
            .StoreDurably());

        // Values sync every 4 hours
        q.AddTrigger(opts => opts
            .ForJob(valuesJobKey)
            .WithIdentity("FantasyCalcValuesJob-trigger")
            .WithCronSchedule("0 0 */4 * * ?") // Every 4 hours
            .WithDescription("Fantasy Calc values sync - Every 4 hours"));
    });

    // Add Quartz hosted service
    builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

    var app = builder.Build();

    // Configure the HTTP request pipeline
    if (app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
    }

    app.UseRouting();
    app.MapControllers();
    app.MapHealthChecks("/health");

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
