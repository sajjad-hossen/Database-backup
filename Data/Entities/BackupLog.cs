using System;

namespace DatabaseBackupAPI.Data.Entities;

public class BackupLog
{
    public Guid Id { get; set; }
    public Guid DatabaseConfigId { get; set; }
    public DatabaseConfig DatabaseConfig { get; set; } = null!;
    
    public DateTime ExecutionTime { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = string.Empty; // Pending/Success/Failed
    public string? StorageFilePath { get; set; }
    public long? FileSizeInBytes { get; set; }
    public string? ErrorMessage { get; set; }
}
