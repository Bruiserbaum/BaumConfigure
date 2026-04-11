using System.Text;
using BaumConfigureGUI.Models;

namespace BaumConfigureGUI.Services;

/// <summary>
/// Builds a deployable .img from a base image by injecting cloud-init config.
/// All heavy lifting runs inside WSL via virt-customize.
/// </summary>
public class ImageBuilderService(string wslDistro)
{
    private readonly WslService _wsl = new(wslDistro);

    /// <summary>
    /// Hash a plain-text password using sha-512 via mkpasswd in WSL.
    /// Returns the hash string, or throws on failure.
    /// </summary>
    public async Task<string> HashPasswordAsync(string password)
    {
        string? hash = null;
        var escaped = password.Replace("'", "'\\''");
        await _wsl.RunAsync(
            $"printf '%s' '{escaped}' | mkpasswd -m sha-512 -s",
            line =>
            {
                if (line.StartsWith("$6$") || line.StartsWith("$y$"))
                    hash = line.Trim();
            });
        return hash ?? throw new InvalidOperationException(
            "mkpasswd returned no output. Is whois installed in WSL?\n  sudo apt install whois");
    }

    /// <summary>
    /// Builds a configured .img file from the base image.
    /// Steps:
    ///   1. Copy base image to output path
    ///   2. Write user-data / meta-data to a temp dir in WSL
    ///   3. Use virt-customize to inject them into the image copy
    /// </summary>
    public async Task BuildImageAsync(
        NodeConfig      config,
        string          baseImagePath,
        string          outputImagePath,
        Action<string>  onLog,
        CancellationToken ct = default)
    {
        var wslBase   = WslService.ToWslPath(baseImagePath);
        var wslOutput = WslService.ToWslPath(outputImagePath);
        var tmpDir    = $"/tmp/baumc-{config.Hostname}-{DateTime.Now:yyyyMMddHHmmss}";

        var userData = CloudInitService.GenerateUserData(config);
        var metaData = CloudInitService.GenerateMetaData(config);
        var netplan  = CloudInitService.GenerateNetplanConfig(config);

        // Write config files to Windows temp so we can copy them in via WSL
        var winTmp  = Path.Combine(Path.GetTempPath(), $"baumc-{config.Hostname}");
        Directory.CreateDirectory(winTmp);
        File.WriteAllText(Path.Combine(winTmp, "user-data"), userData);
        File.WriteAllText(Path.Combine(winTmp, "meta-data"), metaData);
        if (netplan != null)
            File.WriteAllText(Path.Combine(winTmp, "90-baum-network.yaml"), netplan);
        var wslTmp = WslService.ToWslPath(winTmp);

        var sb = new StringBuilder();
        sb.AppendLine("set -euo pipefail");
        sb.AppendLine();
        sb.AppendLine("echo '── Step 1/4: Checking for virt-customize...'");
        sb.AppendLine("command -v virt-customize >/dev/null || { echo 'ERROR: virt-customize not found.'; echo 'Install with: sudo apt install libguestfs-tools'; exit 1; }");
        sb.AppendLine();
        sb.AppendLine("echo '── Step 2/4: Copying base image...'");
        sb.AppendLine($"cp '{wslBase}' '{wslOutput}'");
        sb.AppendLine($"echo '  Copied to {wslOutput}'");
        sb.AppendLine();
        sb.AppendLine("echo '── Step 3/4: Injecting cloud-init config...'");
        sb.AppendLine($"LIBGUESTFS_BACKEND=direct virt-customize \\");
        sb.AppendLine($"  -a '{wslOutput}' \\");
        sb.AppendLine($"  --copy-in '{wslTmp}/user-data:/etc/cloud/cloud.cfg.d/' \\");
        sb.AppendLine($"  --copy-in '{wslTmp}/meta-data:/etc/cloud/cloud.cfg.d/' \\");
        if (netplan != null)
        {
            sb.AppendLine($"  --copy-in '{wslTmp}/90-baum-network.yaml:/etc/netplan/' \\");
            sb.AppendLine("  --run-command 'chmod 600 /etc/netplan/90-baum-network.yaml' \\");
        }
        sb.AppendLine("  --run-command 'chmod 644 /etc/cloud/cloud.cfg.d/user-data /etc/cloud/cloud.cfg.d/meta-data'");
        sb.AppendLine();
        sb.AppendLine("echo '── Step 4/4: Cleaning up...'");
        sb.AppendLine($"rm -rf '{wslTmp}'");
        sb.AppendLine();
        sb.AppendLine($"echo ''");
        sb.AppendLine($"echo '✔ Image ready: {wslOutput}'");

        await _wsl.RunAsync(sb.ToString(), onLog, ct);
    }
}
