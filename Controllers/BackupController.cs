using System;
using System.IO;
using DatabaseBackupAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace DatabaseBackupAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BackupController : ControllerBase
{
    private readonly BackupService _backupService;

    public BackupController(BackupService backupService)
    {
        _backupService = backupService;
    }

    [HttpPost("run-backup")]
    public IActionResult RunBackup()
    {
        try
        {
            // Use the actual credentials from appsettings.json
            string connectionString = "postgresql://postgres:sajjad@localhost:5432/DatabaseBackupDB";
            
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fileName = $"backup_{timestamp}.dump";
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
}
