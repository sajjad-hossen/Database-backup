using System;
using System.Diagnostics;
using Microsoft.Extensions.Configuration;

namespace DatabaseBackupAPI.Services;

public class BackupService
{
    private readonly IConfiguration _configuration;

    public BackupService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public bool CreateLocalBackup(string connectionString, string outputFilePath)
    {
        try
        {
            string pgDumpPath = _configuration["PgDumpPath"] ?? "pg_dump";

            var processStartInfo = new ProcessStartInfo
            {
                FileName = pgDumpPath,
                // Add -t flags to export only the tables the user cares about
                Arguments = $"\"{connectionString}\" -t \"\\\"BazarCosts\\\"\" -t \"\\\"Deposits\\\"\" -t \"\\\"Meals\\\"\" -t \"\\\"Users\\\"\" -t \"\\\"Messes\\\"\" -f \"{outputFilePath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processStartInfo);
            if (process == null)
            {
                Console.WriteLine("Failed to start pg_dump process.");
                return false;
            }

            // Read standard output and error to capture any messages
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();

            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                Console.WriteLine($"Backup failed with exit code {process.ExitCode}.");
                if (!string.IsNullOrEmpty(error))
                {
                    Console.WriteLine($"Error details: {error}");
                }
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An exception occurred during backup: {ex.Message}");
            return false;
        }
    }
}
