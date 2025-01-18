using System.Diagnostics;

namespace a2k.Shared;

public static class Shell
{
    public static void Run(string command, bool throwOnError = true)
    {
        if (string.IsNullOrEmpty(command))
        {
            throw new ArgumentNullException(nameof(command));
        }

        using var process = new Process();
        var parts = command.Split(' ', 2);
        process.StartInfo.FileName = parts[0];
        process.StartInfo.Arguments = parts.Length > 1 ? parts[1] : string.Empty;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.RedirectStandardInput = true;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.CreateNoWindow = true;

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
