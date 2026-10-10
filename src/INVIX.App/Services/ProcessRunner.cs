using System.Diagnostics;

namespace Invix.Services;

internal static class ProcessRunner
{
    public readonly record struct Result(int ExitCode, string StdOut, string StdErr, bool TimedOut)
    {
        public bool Succeeded => !TimedOut && ExitCode == 0;
    }

    public static Result Run(string fileName, string arguments, int timeoutMs = 15000)
    {
        var psi = new ProcessStartInfo(fileName, arguments)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        try
        {
            using var process = Process.Start(psi);
            if (process is null)
            {
                return new Result(-1, string.Empty, $"Could not start '{fileName}'.", false);
            }

            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit(timeoutMs))
            {
                try { process.Kill(entireProcessTree: true); } catch {  }
                return new Result(-1, string.Empty, $"'{fileName}' timed out after {timeoutMs} ms.", true);
            }

            return new Result(process.ExitCode, stdout.GetAwaiter().GetResult(), stderr.GetAwaiter().GetResult(), false);
        }
        catch (Exception ex)
        {
            return new Result(-1, string.Empty, ex.Message, false);
        }
    }
}