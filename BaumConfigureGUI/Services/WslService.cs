using System.Diagnostics;

namespace BaumConfigureGUI.Services;

public class WslService(string distro = "Ubuntu")
{
    public async Task RunAsync(string command, Action<string> onOutput,
        CancellationToken ct = default, string? user = null)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "wsl.exe",
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute  = false,
            CreateNoWindow   = true,
        };

        if (!string.IsNullOrWhiteSpace(distro))
        {
            psi.ArgumentList.Add("-d");
            psi.ArgumentList.Add(distro);
        }
        if (!string.IsNullOrWhiteSpace(user))
        {
            psi.ArgumentList.Add("-u");
            psi.ArgumentList.Add(user);
        }
        psi.ArgumentList.Add("--");
        psi.ArgumentList.Add("bash");
        psi.ArgumentList.Add("-c");
        psi.ArgumentList.Add(command.Replace("\r\n", "\n").Replace("\r", "\n"));

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start wsl.exe");

        var stdoutTask = Task.Run(async () =>
        {
            while (await process.StandardOutput.ReadLineAsync(ct) is { } line)
                onOutput(line);
        }, ct);

        var stderrTask = Task.Run(async () =>
        {
            while (await process.StandardError.ReadLineAsync(ct) is { } line)
                onOutput("ERR: " + line);
        }, ct);

        await Task.WhenAll(stdoutTask, stderrTask);
        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"WSL command failed (exit {process.ExitCode}).");
    }

    /// <summary>
    /// Writes <paramref name="script"/> to a Windows temp file with Unix line endings,
    /// then executes it via <c>bash /mnt/c/…/script.sh</c>.
    /// This avoids CRLF contamination that occurs when passing multi-line scripts
    /// through <c>bash -c</c> via wsl.exe argument forwarding.
    /// </summary>
    public async Task RunScriptAsync(string script, Action<string> onOutput,
        CancellationToken ct = default, string? user = null)
    {
        var tmpPath = Path.Combine(Path.GetTempPath(), $"baumc-{Guid.NewGuid():N}.sh");
        // Write with Unix LF endings and no BOM so bash can execute it cleanly
        await File.WriteAllTextAsync(tmpPath,
            script.Replace("\r\n", "\n").Replace("\r", "\n"),
            new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false), ct);
        try
        {
            var wslPath = ToWslPath(tmpPath);
            await RunAsync($"bash '{wslPath}'", onOutput, ct, user);
        }
        finally
        {
            File.Delete(tmpPath);
        }
    }

    /// <summary>Converts a Windows path like C:\foo\bar to /mnt/c/foo/bar for WSL.</summary>
    public static string ToWslPath(string windowsPath)
    {
        if (windowsPath.Length >= 2 && windowsPath[1] == ':')
        {
            var drive = char.ToLower(windowsPath[0]);
            var rest  = windowsPath[2..].Replace('\\', '/');
            return $"/mnt/{drive}{rest}";
        }
        return windowsPath.Replace('\\', '/');
    }
}
