using System;
using System.IO;

namespace OilErp.Bootstrap;

public static class FirstRunTracker
{
    private static readonly string MarkerDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OilErp");
    private static readonly string MarkerFile = Path.Combine(MarkerDir, "first-run.machine");

    public static bool IsFirstRun(out string machineCode)
    {
        machineCode = ComputeMachineCode();
        if (!File.Exists(MarkerFile)) return true;
        var content = File.ReadAllText(MarkerFile).Trim();
        return !string.Equals(content, machineCode, StringComparison.OrdinalIgnoreCase);
    }

    public static void MarkCompleted(string machineCode)
    {
        try
        {
            Directory.CreateDirectory(MarkerDir);
            File.WriteAllText(MarkerFile, machineCode);
        }
        catch
        {
            // swallow
        }
    }

    private static string ComputeMachineCode()
    {
        var host = Environment.MachineName;
        var user = Environment.UserName;
        var seed = $"{host}:{user}".ToLowerInvariant();
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(seed));
    }
}
