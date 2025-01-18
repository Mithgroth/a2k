using System.Diagnostics;

namespace a2k.Shared;

public static class Shell
{
    public static void Run(string command, string? workingDirectory = null, bool throwOnError = true)
    {
        if (string.IsNullOrEmpty(command))
        {
            throw new ArgumentNullException(nameof(command));
        }

        var parts = command.Split(' ', 2);
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = parts[0],
                Arguments = parts.Length > 1 ? parts[1] : string.Empty,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = workingDirectory ?? Directory.GetCurrentDirectory()
            }
        };

        process.Start();
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        Console.WriteLine(stdout);

        if (process.ExitCode != 0 && throwOnError)
        {
            throw new Exception($"Command failed: {stderr}");
        }
    }
}
