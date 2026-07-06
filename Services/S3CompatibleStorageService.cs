using System;
using System.IO;
using System.Threading.Tasks;
using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;

namespace DatabaseBackupAPI.Services;

public class S3CompatibleStorageService : IStorageService
{
    private static AmazonS3Client CreateClient(string serviceUrl, string accessKey, string secretKey)
    {
        var config = new AmazonS3Config
        {
            ServiceURL         = serviceUrl,
            ForcePathStyle     = true,
            SignatureVersion   = "4",
            SignatureMethod    = Amazon.Runtime.SigningAlgorithm.HmacSHA256,
            // Cloudflare R2 requires the region to be "auto"; without this the SDK
            // defaults to us-east-1 and the computed signature does not match.
            AuthenticationRegion = "auto"
        };
        return new AmazonS3Client(accessKey, secretKey, config);
    }

    public async Task UploadFileAsync(string serviceUrl, string accessKey, string secretKey, string bucketName, string s3Key, string localFilePath)
    {
        using var client = CreateClient(serviceUrl, accessKey, secretKey);

        // Use PutObjectRequest directly — most compatible with Cloudflare R2
        var putRequest = new PutObjectRequest
        {
            BucketName = bucketName,
            Key = s3Key,
            FilePath = localFilePath,
            DisablePayloadSigning = true
        };

        await client.PutObjectAsync(putRequest);

        // Delete the local file after a successful upload
        if (File.Exists(localFilePath))
        {
            File.Delete(localFilePath);
        }
    }

    public async Task DeleteOldBackupsAsync(string bucketName, int keepDays, string accessKey, string secretKey, string serviceUrl)
    {
        using var client = CreateClient(serviceUrl, accessKey, secretKey);

        var request = new ListObjectsV2Request
        {
            BucketName = bucketName
        };

        var response = await client.ListObjectsV2Async(request);
        var cutoffDate = DateTime.UtcNow.AddDays(-keepDays);

        foreach (var s3Object in response.S3Objects)
        {
            // Check if the file is older than the specified retention days
            if (s3Object.LastModified < cutoffDate)
            {
                var deleteRequest = new DeleteObjectRequest
                {
                    BucketName = bucketName,
                    Key = s3Object.Key
                };
                
                await client.DeleteObjectAsync(deleteRequest);
            }
        }
    }
}
