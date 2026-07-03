using System;
using System.IO;
using DatabaseBackupAPI.Services;
using Microsoft.AspNetCore.Mvc;

using Microsoft.Extensions.Configuration;

namespace DatabaseBackupAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BackupController : ControllerBase
{
    private readonly BackupService _backupService;
    private readonly IConfiguration _configuration;
    private readonly FinancialReportService _financialReportService;

    public BackupController(
        BackupService backupService,
        IConfiguration configuration,
        FinancialReportService financialReportService)
    {
        _backupService = backupService;
        _configuration = configuration;
        _financialReportService = financialReportService;
    }

    [HttpPost("run-backup")]
    public IActionResult RunBackup()
    {
        try
        {
            // Use the actual credentials from appsettings.json
            string dbConnectionString = _configuration.GetConnectionString("DefaultConnection");
            
            // Note: pg_dump expects a connection string in URI format or libpq format.
            // A simple conversion from the ADO.NET format:
            var builder = new System.Data.Common.DbConnectionStringBuilder { ConnectionString = dbConnectionString };
            string host = builder.ContainsKey("Host") ? builder["Host"].ToString() : "localhost";
            string port = builder.ContainsKey("Port") ? builder["Port"].ToString() : "5432";
            string database = builder.ContainsKey("Database") ? builder["Database"].ToString() : "";
            string username = builder.ContainsKey("Username") ? builder["Username"].ToString() : "";
            string password = builder.ContainsKey("Password") ? builder["Password"].ToString() : "";
            
            string connectionString = $"postgresql://{username}:{password}@{host}:{port}/{database}";

            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fileName = $"backup_{timestamp}.sql";
            string outputFilePath = Path.Combine(desktopPath, fileName);

            bool isSuccess = _backupService.CreateLocalBackup(connectionString, outputFilePath);

            if (isSuccess)
            {
                return Ok(new { Message = "Backup successful", FilePath = outputFilePath });
            }
            else
            {
                return BadRequest(new { Message = "Backup failed. Check server logs for details." });
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = $"An unexpected error occurred: {ex.Message}" });
        }
    }

    [HttpGet("schema-info")]
    public async Task<IActionResult> GetSchemaInfo()
    {
        var connStr = _configuration.GetConnectionString("NeonConnection");
        await using var conn = new Npgsql.NpgsqlConnection(connStr);
        await conn.OpenAsync();

        // List all public tables
        var tableList = new List<string>();
        await using (var listCmd = new Npgsql.NpgsqlCommand(
            "SELECT table_name FROM information_schema.tables WHERE table_schema='public' ORDER BY table_name", conn))
        await using (var lr = await listCmd.ExecuteReaderAsync())
            while (await lr.ReadAsync()) tableList.Add(lr.GetString(0));

        var result = new Dictionary<string, object> { ["AllTables"] = tableList };

        var tables = new[] { "Users", "Meals", "Deposits", "BazarCosts", "Messes" };
        foreach (var table in tables)
        {
            try
            {
                await using var cmd = new Npgsql.NpgsqlCommand($"SELECT * FROM \"{table}\" LIMIT 2", conn);
                await using var reader = await cmd.ExecuteReaderAsync();
                var cols = new List<string>();
                for (int i = 0; i < reader.FieldCount; i++) cols.Add(reader.GetName(i));
                var rows = new List<Dictionary<string,string>>();
                while (await reader.ReadAsync())
                {
                    var row = new Dictionary<string,string>();
                    for (int i = 0; i < reader.FieldCount; i++)
                        row[cols[i]] = reader.IsDBNull(i) ? "NULL" : reader.GetValue(i)?.ToString() ?? "NULL";
                    rows.Add(row);
                }
                result[table] = new { Columns = cols, SampleRows = rows };
            }
            catch (Exception ex) { result[table] = $"ERROR: {ex.Message}"; }
        }
        return Ok(result);
    }

    [HttpGet("financial-report")]
    public async Task<IActionResult> GenerateFinancialReport()
    {
        try
        {
            string html = await _financialReportService.GenerateHtmlReportAsync();

            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fileName = $"FinancialReport_{timestamp}.html";
            string outputFilePath = Path.Combine(desktopPath, fileName);

            await System.IO.File.WriteAllTextAsync(outputFilePath, html, System.Text.Encoding.UTF8);

            return Ok(new
            {
                Message = "Financial report generated successfully!",
                FilePath = outputFilePath,
                Tables = new[] { "Users", "Meals", "Deposits", "BazarCosts" }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = $"Report generation failed: {ex.Message}" });
        }
    }
}
