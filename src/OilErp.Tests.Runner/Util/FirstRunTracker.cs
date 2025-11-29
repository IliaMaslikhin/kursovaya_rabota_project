using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace OilErp.Bootstrap;

/// <summary>
/// Tracks whether the current machine/user is running the harness for the first time.
/// </summary>
internal static class FirstRunTracker
{
    private static readonly string MarkerDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OilErp");
    private static readonly string MarkerPath = Path.Combine(MarkerDirectory, "first-run.machine");

    /// <summary>
    /// Returns true if this machine/user is running the harness for the first time.
    /// </summary>
    public static bool IsFirstRun(out string machineCode)
    {
        machineCode = BuildMachineCode();

        try
        {
            if (!File.Exists(MarkerPath))
            {
                return true;
            }

            var text = File.ReadAllText(MarkerPath).Trim();
            return !string.Equals(text, machineCode, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            // On any IO/permission error assume first run to stay conservative.
            return true;
        }
    }

    /// <summary>
    /// Marks the machine as provisioned.
    /// </summary>
    public static void MarkCompleted(string machineCode)
    {
        try
        {
            Directory.CreateDirectory(MarkerDirectory);
            File.WriteAllText(MarkerPath, machineCode);
        }
        catch
        {
            // Swallow errors to avoid blocking the main flow.
        }
    }

    private static string BuildMachineCode()
    {
        var processPath = Environment.ProcessPath ?? "unknown";
        var fingerprint = $"{Environment.MachineName}|{Environment.UserName}|{Environment.OSVersion}|{processPath}";
        var bytes = Encoding.UTF8.GetBytes(fingerprint);
        var hash = SHA256.HashData(bytes);
        // Shorten for readability while remaining deterministic.
        return Convert.ToHexString(hash.AsSpan(0, 8));
    }
}
