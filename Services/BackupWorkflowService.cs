using System;
using System.IO;
using System.Threading.Tasks;
using Hangfire;

namespace DatabaseBackupAPI.Services;

public class BackupWorkflowService
{
    private readonly BackupService _backupService;
    private readonly IConfiguration _configuration;

    public BackupWorkflowService(BackupService backupService, IStorageService storageService, IConfiguration configuration)
    {
        _backupService = backupService;
        _storageService = storageService;
        _configuration = configuration;
    }

    /// <summary>
    /// Executes the entire backup workflow and notifies via Discord.
    /// </summary>
    public async Task ExecuteFullBackupAsync(string connectionString, string projectName, string discordWebhookUrl)
    {
        // Generate file paths
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string fileName = $"{projectName}_backup_{timestamp}.sql";
        string outputFilePath = Path.Combine(Path.GetTempPath(), fileName);

        // Fetch S3 settings
        string serviceUrl = _configuration["S3Storage:ServiceUrl"] ?? "";
        string accessKey = _configuration["S3Storage:AccessKey"] ?? "";
        string secretKey = _configuration["S3Storage:SecretKey"] ?? "";
        string bucketName = _configuration["S3Storage:BucketName"] ?? "";
        int keepDays = _configuration.GetValue<int>("S3Storage:RetentionDays", 7);

        // 1. Run pg_dump (Local Backup)
        bool success = _backupService.CreateLocalBackup(connectionString, outputFilePath);
        
        if (!success)
        {
            await NotifyDiscordAsync(discordWebhookUrl, $"❌ **Backup Failed** for `{projectName}`. Could not generate local pg_dump file.");
            throw new Exception("Local pg_dump backup failed. Aborting upload workflow.");
        }

        try
        {
            // 2. Upload to S3-Compatible Storage
            await _storageService.UploadFileAsync(serviceUrl, accessKey, secretKey, bucketName, fileName, outputFilePath);

            // 3. Apply Retention Policy (Delete old backups)
            await _storageService.DeleteOldBackupsAsync(bucketName, keepDays, accessKey, secretKey, serviceUrl);

            // 4. Success notification
            await NotifyDiscordAsync(discordWebhookUrl, $"✅ **Backup Successful** for `{projectName}`.\nFile: `{fileName}` uploaded to `{bucketName}`.");
        }
        catch (Exception ex)
        {
            await NotifyDiscordAsync(discordWebhookUrl, $"❌ **Upload/Cleanup Failed** for `{projectName}`.\nError: {ex.Message}");
            throw;
        }
    }

    private static async Task NotifyDiscordAsync(string webhookUrl, string message)
    {
        if (string.IsNullOrWhiteSpace(webhookUrl) || webhookUrl == "YOUR_DISCORD_WEBHOOK_URL") return;

        try
        {
            using var client = new System.Net.Http.HttpClient();
            var payload = new { content = message };
            var json = System.Text.Json.JsonSerializer.Serialize(payload);
            var content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json");
            await client.PostAsync(webhookUrl, content);
        }
        catch 
        {
            // Ignore discord failures so backup job doesn't fail
        }
    }
}
