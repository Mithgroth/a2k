using System.Diagnostics;

namespace a2k.Cli.Services;

public class ShellService
{
    public static void RunCommand(string arguments, string? fileName = "", bool throwOnError = true)
    {
        using var process = new Process();
        process.StartInfo.FileName = fileName;
        process.StartInfo.Arguments = arguments;
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
            throw new Exception($"Docker command failed: {stderr}");
        }
    }
}
