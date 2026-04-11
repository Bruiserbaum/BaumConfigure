using System.Text;
using BaumConfigureGUI.Models;

namespace BaumConfigureGUI.Services;

/// <summary>
/// Builds a deployable .img from a base image by injecting cloud-init config.
///
/// losetup requires the target file to live on a native Linux filesystem.
/// DrvFs mounts (/mnt/c/…) do not support loop devices, so the entire build
/// works in /tmp (WSL-native ext4) and the finished image is moved to the
/// Windows output path at the end.
///
/// All disk operations run as root via wsl -u root.
/// </summary>
public class ImageBuilderService(string wslDistro)
{
    private readonly WslService _wsl     = new(wslDistro);
    private readonly WslService _wslRoot = new(wslDistro);

    /// <summary>
    /// Hash a plain-text password using sha-512 via mkpasswd in WSL.
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
    ///   1. Decompress (.xz) or copy base image into /tmp on the WSL-native fs
    ///   2. Attach /tmp image via losetup -fP
    ///   3. Find the ext4 root partition, mount it, inject cloud-init + netplan
    ///   4. Unmount, detach loop, move finished image to Windows output path
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

        var userData = CloudInitService.GenerateUserData(config);
        var metaData = CloudInitService.GenerateMetaData(config);
        var netplan  = CloudInitService.GenerateNetplanConfig(config);

        // Write config files to Windows temp so WSL can read them via /mnt/
        var winTmp = Path.Combine(Path.GetTempPath(), $"baumc-{config.Hostname}");
        Directory.CreateDirectory(winTmp);
        File.WriteAllText(Path.Combine(winTmp, "user-data"), userData);
        File.WriteAllText(Path.Combine(winTmp, "meta-data"), metaData);
        if (netplan != null)
            File.WriteAllText(Path.Combine(winTmp, "90-baum-network.yaml"), netplan);
        var wslTmp = WslService.ToWslPath(winTmp);

        bool isXz = baseImagePath.EndsWith(".xz", StringComparison.OrdinalIgnoreCase);

        var sb = new StringBuilder();
        sb.AppendLine("set -euo pipefail");
        sb.AppendLine();
        sb.AppendLine("# Use a temp path on the WSL-native filesystem.");
        sb.AppendLine("# losetup cannot use DrvFs (/mnt/c/…) files.");
        sb.AppendLine("TMP_IMG=\"/tmp/baumc-$$.img\"");
        sb.AppendLine();

        // ── Step 1: Decompress or copy to /tmp ───────────────────────────────
        sb.AppendLine("echo '── Step 1/4: Preparing image on WSL filesystem...'");
        if (isXz)
        {
            sb.AppendLine($"echo '  Decompressing .xz (this may take a while)...'");
            sb.AppendLine($"xz -d -c '{wslBase}' > \"$TMP_IMG\"");
        }
        else
        {
            sb.AppendLine($"cp '{wslBase}' \"$TMP_IMG\"");
        }
        sb.AppendLine("echo '  Image staged at $TMP_IMG'");
        sb.AppendLine();

        // ── Step 2: Attach via losetup ────────────────────────────────────────
        sb.AppendLine("echo '── Step 2/4: Attaching image...'");
        sb.AppendLine("LOOP=$(losetup -fP --show \"$TMP_IMG\")");
        sb.AppendLine("[ -n \"$LOOP\" ] || { echo 'ERROR: losetup returned empty device'; rm -f \"$TMP_IMG\"; exit 1; }");
        sb.AppendLine("echo \"  Loop device: $LOOP\"");
        sb.AppendLine("sleep 0.5  # let kernel scan partition table");
        sb.AppendLine();

        // ── Step 3: Find root partition and inject ────────────────────────────
        sb.AppendLine("echo '── Step 3/4: Injecting cloud-init config...'");
        sb.AppendLine("ROOT_PART=''");
        sb.AppendLine("for PART in ${LOOP}p2 ${LOOP}p1 ${LOOP}p3 ${LOOP}p4; do");
        sb.AppendLine("  [ -b \"$PART\" ] || continue");
        sb.AppendLine("  FSTYPE=$(blkid -o value -s TYPE \"$PART\" 2>/dev/null || echo '')");
        sb.AppendLine("  if [ \"$FSTYPE\" = 'ext4' ]; then ROOT_PART=\"$PART\"; break; fi");
        sb.AppendLine("done");
        sb.AppendLine();
        sb.AppendLine("if [ -z \"$ROOT_PART\" ]; then");
        sb.AppendLine("  echo 'ERROR: No ext4 root partition found in image.'");
        sb.AppendLine("  losetup -d \"$LOOP\"; rm -f \"$TMP_IMG\"; exit 1");
        sb.AppendLine("fi");
        sb.AppendLine("echo \"  Root partition: $ROOT_PART\"");
        sb.AppendLine();
        sb.AppendLine("mkdir -p /tmp/baumc-mnt");
        sb.AppendLine("mount \"$ROOT_PART\" /tmp/baumc-mnt");
        sb.AppendLine();
        sb.AppendLine("mkdir -p /tmp/baumc-mnt/etc/cloud/cloud.cfg.d");
        sb.AppendLine($"cp '{wslTmp}/user-data' /tmp/baumc-mnt/etc/cloud/cloud.cfg.d/user-data");
        sb.AppendLine($"cp '{wslTmp}/meta-data' /tmp/baumc-mnt/etc/cloud/cloud.cfg.d/meta-data");
        sb.AppendLine("chmod 644 /tmp/baumc-mnt/etc/cloud/cloud.cfg.d/user-data /tmp/baumc-mnt/etc/cloud/cloud.cfg.d/meta-data");

        if (netplan != null)
        {
            sb.AppendLine("mkdir -p /tmp/baumc-mnt/etc/netplan");
            sb.AppendLine($"cp '{wslTmp}/90-baum-network.yaml' /tmp/baumc-mnt/etc/netplan/90-baum-network.yaml");
            sb.AppendLine("chmod 600 /tmp/baumc-mnt/etc/netplan/90-baum-network.yaml");
        }

        sb.AppendLine();

        // ── Step 4: Unmount, detach, move to output ───────────────────────────
        sb.AppendLine("echo '── Step 4/4: Finalising...'");
        sb.AppendLine("sync");
        sb.AppendLine("umount /tmp/baumc-mnt");
        sb.AppendLine("losetup -d \"$LOOP\"");
        sb.AppendLine($"echo '  Moving image to output path...'");
        sb.AppendLine($"mv \"$TMP_IMG\" '{wslOutput}'");
        sb.AppendLine($"rm -rf '{wslTmp}'");
        sb.AppendLine();
        sb.AppendLine($"echo '✔ Image ready: {wslOutput}'");

        await _wslRoot.RunAsync(sb.ToString(), onLog, ct, user: "root");
    }
}
