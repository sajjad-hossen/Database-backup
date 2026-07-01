using System;
using System.Collections.Generic;

namespace DatabaseBackupAPI.Data.Entities;

public class DatabaseConfig
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    
    public string ProjectName { get; set; } = string.Empty;
    public string DatabaseType { get; set; } = string.Empty; // PostgreSQL/MySQL
    public string EncryptedConnectionString { get; set; } = string.Empty;
    public string EncryptedStorageConfig { get; set; } = string.Empty; // AWS S3/Google Drive keys
    public string BackupScheduleCron { get; set; } = string.Empty;
    public string? NotificationWebhookUrl { get; set; } // Discord/Slack
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<BackupLog> BackupLogs { get; set; } = new List<BackupLog>();
}
