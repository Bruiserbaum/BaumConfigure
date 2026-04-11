# BaumConfigure

Windows GUI for building pre-configured Ubuntu OS images for ARM and x64 servers. Set hostname, user credentials, timezone, and software — then generate a ready-to-flash `.img` file with cloud-init baked in.

---

## Features

- **Dark theme** matching the rest of the BaumLab suite
- **Image builder** — injects cloud-init config directly into a base `.img` via `virt-customize` in WSL
- **Rockchip image browser** — browse and download Ubuntu Rockchip releases from GitHub directly in-app, with automatic `.img.xz` decompression via WSL
- **User setup** — hostname, username, password with live strength indicator and automatic sha-512 hashing
- **Software options** — Docker (official install script), Kubernetes (kubeadm), Portainer CE
- **Extra packages and first-boot commands** for custom configuration
- **Auto-update** — checks GitHub on launch and offers one-click install with automatic restart
- **Settings persistence** — all paths and config fields saved across sessions

---

## How It Works

```
Base Ubuntu .img
       │
       ▼
BaumConfigure (Windows GUI)
       │  Generates cloud-init user-data / meta-data
       │  Runs virt-customize in WSL to inject config
       ▼
Configured .img  ──►  Flash to node via BMC or balenaEtcher
```

1. Download a base Ubuntu ARM64 or x64 image (see links below)
2. Open BaumConfigure and fill in your settings
3. Click **Build Image** — the app calls WSL to produce a configured `.img`
4. Flash the output image to your node

---

## Requirements

### Windows
- Windows 10 (build 19041+) or Windows 11
- WSL with a Linux distro (Ubuntu recommended)

### Inside WSL
```bash
sudo apt install libguestfs-tools whois
```

| Tool | Purpose |
|------|---------|
| `virt-customize` (libguestfs-tools) | Injects cloud-init config into the image |
| `mkpasswd` (whois) | Hashes the password to sha-512 at build time |

---

## Installation

Download and run the latest installer — no admin rights required:

**[BaumConfigure-Setup-1.5.2.exe](https://github.com/Bruiserbaum/BaumConfigure/releases/latest)**

---

## Base Images

Download to any folder and point BaumConfigure at the file:

| Target | Image | Source |
|--------|-------|--------|
| Rockchip SBCs (RK3588, RK3566…) | Ubuntu for Rockchip | Use **Get Rockchip Image** button in BaumConfigure, or [ubuntu-rockchip releases](https://github.com/Joshua-Riek/ubuntu-rockchip/releases) |
| Turing RK1 (ARM64) | Ubuntu 24.04 for RK1 | [Turing Pi RK1 releases](https://github.com/turing-machines/armbian-build/releases) |
| Raspberry Pi CM4 (ARM64) | Ubuntu 24.04 Server ARM64 | [Ubuntu Raspberry Pi](https://ubuntu.com/download/raspberry-pi) |
| Generic x64 server | Ubuntu 24.04 Server x64 | [Ubuntu Server](https://ubuntu.com/download/server) |

---

## Configuration Fields

| Field | Description |
|-------|-------------|
| **Base Image** | Path to the source `.img` file |
| **Output Folder** | Where the configured image is written |
| **WSL Distro** | WSL distro name (default: `Ubuntu`) |
| **Hostname** | Node hostname set on first boot |
| **Timezone** | e.g. `America/New_York`, `Europe/London` |
| **Username** | Primary user account name |
| **Password** | Plain-text input — hashed to sha-512 by WSL at build time, never stored as plaintext |
| **Docker** | Installs Docker via the official install script, adds user to docker group |
| **Kubernetes** | Installs kubeadm, kubelet, kubectl (v1.32) |
| **Portainer CE** | Deploys Portainer CE container (requires Docker) |
| **Extra Packages** | Additional apt packages, space or comma separated |
| **First-Boot Commands** | Custom runcmd entries, one per line |

---

## Updates

BaumConfigure checks for updates on launch. When a new version is available, an **Update available** badge appears in the title bar. Clicking it downloads the installer and relaunches the app automatically.

---

## Building from Source

```bash
git clone https://github.com/Bruiserbaum/BaumConfigure.git
cd BaumConfigure/BaumConfigureGUI
dotnet build
dotnet run
```

To build a release installer:
```bash
cd installer
build-installer.bat
# Requires Inno Setup 6: https://jrsoftware.org/isinfo.php
```

To regenerate the icon:
```powershell
.\create-icon.ps1
```

---

## Project Structure

```
BaumConfigure/
├── BaumConfigureGUI/
│   ├── AppTheme.cs               # Shared colour palette and fonts
│   ├── MainForm.cs               # Main window
│   ├── RockchipBrowserForm.cs    # Modal dialog for Rockchip image browser
│   ├── Models/
│   │   ├── NodeConfig.cs         # Image configuration model
│   │   └── AppSettings.cs        # Persisted settings model
│   ├── Services/
│   │   ├── CloudInitService.cs   # Generates user-data / meta-data
│   │   ├── ImageBuilderService.cs# virt-customize build pipeline
│   │   ├── RockchipImageService.cs# GitHub Rockchip release browser + downloader
│   │   ├── UpdateService.cs      # GitHub update check + installer
│   │   └── WslService.cs         # Runs commands in WSL
│   └── Resources/
│       └── app.ico               # Application icon (all sizes)
├── installer/
│   ├── setup.iss                 # Inno Setup script
│   └── build-installer.bat       # Build script
├── create-icon.ps1               # Icon generation script
└── nodes/                        # cloud-init templates (CLI workflow)
```

---

## Part of BaumLab

| Project | Description |
|---------|-------------|
| **[BaumConfigure](https://github.com/Bruiserbaum/BaumConfigure)** | *(this repo)* OS image builder for ARM and x64 servers |
| **[BaumDocker](https://github.com/Bruiserbaum/BaumDocker)** | Docker Compose stacks and Swarm setup |
| **[BaumLaunch](https://github.com/Bruiserbaum/BaumLaunch)** | WinGet GUI with system tray updater |

---

## License

Apache License 2.0. See LICENSE for details.
