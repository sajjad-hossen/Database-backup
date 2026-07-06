using System.Threading.Tasks;

namespace DatabaseBackupAPI.Services;

public interface IStorageService
{
    Task UploadFileAsync(string serviceUrl, string accessKey, string secretKey, string bucketName, string s3Key, string localFilePath);
    Task DeleteOldBackupsAsync(string bucketName, int keepDays, string accessKey, string secretKey, string serviceUrl);
}
