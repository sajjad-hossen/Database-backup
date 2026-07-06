using DatabaseBackupAPI.Data;
using Microsoft.EntityFrameworkCore;
using Hangfire;
using Hangfire.PostgreSql;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddControllers();
builder.Services.AddScoped<DatabaseBackupAPI.Services.BackupService>();
builder.Services.AddScoped<DatabaseBackupAPI.Services.FinancialReportService>();
builder.Services.AddScoped<DatabaseBackupAPI.Services.IStorageService, DatabaseBackupAPI.Services.S3CompatibleStorageService>();
builder.Services.AddScoped<DatabaseBackupAPI.Services.BackupWorkflowService>();
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(c => c.UseNpgsqlConnection(builder.Configuration.GetConnectionString("DefaultConnection"))));

builder.Services.AddHangfireServer();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseHangfireDashboard();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var recurringJobManager = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();
    var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

    string dbConnectionString = configuration.GetConnectionString("NeonConnection") ?? "";
    string discordWebhookUrl = "YOUR_DISCORD_WEBHOOK_URL"; 

    recurringJobManager.AddOrUpdate<BackupWorkflowService>(
        "automated-daily-backup",
        workflow => workflow.ExecuteFullBackupAsync(dbConnectionString, "MessMealDb_Daily", discordWebhookUrl),
        Cron.Daily
    );
}

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
