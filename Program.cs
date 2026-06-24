using BoomBust.Logging;
using BoomBust.HealthChecks;
using FantasyCalcScrape.Configuration;
using FantasyCalcScrape.Jobs;
using FantasyCalcScrape.Services;
using FantasyCalcScrape.Services.Interfaces;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using Quartz;
using Serilog;

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Configure logging with BoomBust.Logging
    builder.UseBoomBustLogging(options =>
    {
        options.ApplicationName = "FantasyCalcScrape";
        options.LogFilePath = "logs/fantasy-calc-scrape-.txt";
        options.OverrideToWarning = new[] { "Microsoft", "System" };
    });

    Log.Information("Starting Fantasy Calc Scrape Service");

    // Configure resilience settings
    builder.Services.Configure<ResilienceConfiguration>(builder.Configuration.GetSection("Resilience"));
    var resilienceConfig = builder.Configuration.GetSection("Resilience").Get<ResilienceConfiguration>()
        ?? new ResilienceConfiguration();

    // Register services
    builder.Services.AddScoped<IFantasyCalcApiService, FantasyCalcApiService>();
    builder.Services.AddScoped<ISupabaseDatabaseService, SupabaseDatabaseService>();

    // Add HTTP client for Fantasy Calc API with resilience policies
    builder.Services.AddHttpClient<FantasyCalcApiService>(client =>
    {
        client.BaseAddress = new Uri("https://api.fantasycalc.com/");
        client.Timeout = TimeSpan.FromSeconds(resilienceConfig.TimeoutSeconds);
        client.DefaultRequestHeaders.Add("User-Agent", "FantasyCalcScraper/1.0");
        client.DefaultRequestHeaders.Add("Accept", "application/json");
    })
    .AddStandardResilienceHandler(options =>
    {
        // Configure retry with exponential backoff
        options.Retry.MaxRetryAttempts = resilienceConfig.MaxRetryAttempts;
        options.Retry.Delay = TimeSpan.FromSeconds(resilienceConfig.DelayBetweenRetriesSeconds);
        options.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
        options.Retry.UseJitter = true;
        options.Retry.OnRetry = args =>
        {
            Log.Warning("Retry attempt {AttemptNumber} for Fantasy Calc API after {Delay}ms delay. Exception: {Exception}",
                args.AttemptNumber, args.RetryDelay.TotalMilliseconds, args.Outcome.Exception?.Message);
            return default;
        };

        // Configure timeout
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(resilienceConfig.TimeoutSeconds);
        options.TotalRequestTimeout.OnTimeout = args =>
        {
            Log.Error("Request to Fantasy Calc API timed out after {Timeout}s", resilienceConfig.TimeoutSeconds);
            return default;
        };

        // Configure circuit breaker
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
        options.CircuitBreaker.FailureRatio = 0.5;
        options.CircuitBreaker.MinimumThroughput = 3;
        options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);
        options.CircuitBreaker.OnOpened = args =>
        {
            Log.Error("Circuit breaker opened for Fantasy Calc API. Will retry after {BreakDuration}s",
                options.CircuitBreaker.BreakDuration.TotalSeconds);
            return default;
        };
        options.CircuitBreaker.OnClosed = args =>
        {
            Log.Information("Circuit breaker closed for Fantasy Calc API. Service is healthy again");
            return default;
        };
    });

    Log.Information("Resilience policies configured: MaxRetries={MaxRetries}, Timeout={Timeout}s, Delay={Delay}s",
        resilienceConfig.MaxRetryAttempts, resilienceConfig.TimeoutSeconds, resilienceConfig.DelayBetweenRetriesSeconds);

    // Add ASP.NET Core services for diagnostic endpoints
    builder.Services.AddControllers();

    // Required for BoomBust.HealthChecks
    builder.Services.AddHttpClient();

    // Get Supabase connection string
    var supabaseUrl = builder.Configuration["Supabase:Url"];
    var supabaseServiceKey = builder.Configuration["Supabase:ServiceRoleKey"];
    var supabaseConnectionString = !string.IsNullOrEmpty(supabaseUrl) && !string.IsNullOrEmpty(supabaseServiceKey)
        ? $"Host={new Uri(supabaseUrl).Host};Database=postgres;Username=postgres;Password={supabaseServiceKey}"
        : null;

    // Add comprehensive health checks with BoomBust.HealthChecks
    builder.Services.AddHealthChecks()
        // Liveness - is the app alive?
        .AddCheck("self", () => HealthCheckResult.Healthy("Application is running"), tags: ["live"])

        // Readiness - Database check
        .AddSupabaseHealthCheck(
            connectionString: supabaseConnectionString ?? "Host=localhost;Database=postgres;Username=postgres;Password=postgres",
            name: "supabase",
            healthCheckName: "Supabase Database",
            failureStatus: HealthStatus.Unhealthy,
            tags: ["db", "supabase", "ready"],
            timeout: TimeSpan.FromSeconds(10)
        )

        // Readiness - External API checks
        .AddApiHealthCheck(
            apiUrl: "https://api.fantasycalc.com/values/current",
            name: "fantasycalc-api",
            healthCheckName: "FantasyCalc API",
            failureStatus: HealthStatus.Degraded, // Non-critical
            tags: ["external", "api", "ready"],
            timeout: TimeSpan.FromSeconds(10)
        );

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

        // Immediate trigger on startup
        q.AddTrigger(opts => opts
            .ForJob(valuesJobKey)
            .WithIdentity("FantasyCalcValuesJob-startup-trigger")
            .StartNow()
            .WithDescription("Fantasy Calc values sync - Run on startup"));

        // Values sync every 4 hours
        q.AddTrigger(opts => opts
            .ForJob(valuesJobKey)
            .WithIdentity("FantasyCalcValuesJob-scheduled-trigger")
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

    // Health check endpoints - Kubernetes style
    app.MapHealthChecks("/health/live", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("live"),
        AllowCachingResponses = false
    });

    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready"),
        AllowCachingResponses = false
    });

    // Detailed health check with JSON response
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        ResponseWriter = async (context, report) =>
        {
            context.Response.ContentType = "application/json";

            var response = new
            {
                status = report.Status.ToString(),
                timestamp = DateTime.UtcNow,
                checks = report.Entries.Select(e => new
                {
                    name = e.Key,
                    status = e.Value.Status.ToString(),
                    description = e.Value.Description,
                    duration = $"{e.Value.Duration.TotalMilliseconds:F2}ms",
                    exception = e.Value.Exception?.Message,
                    data = e.Value.Data
                }),
                totalDuration = $"{report.TotalDuration.TotalMilliseconds:F2}ms"
            };

            await context.Response.WriteAsJsonAsync(response);
        }
    });

    Log.Information("Application started successfully with BetterStack logging and comprehensive health checks configured");
    Log.Information("Health check endpoints: /health, /health/live, /health/ready");

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
