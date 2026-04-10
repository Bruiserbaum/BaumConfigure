# BaumConfigure

Cloud-init configuration and flashing scripts for the Turing Pi 2 cluster. Produces pre-configured Ubuntu images for each node — hostname, user, SSH keys, packages, and startup commands all set before first boot.

---

## How It Works

1. Edit the `nodes/<nodeN>/user-data` for each node (fill in SSH key, timezone, IPs)
2. Run `build-seed.sh` to generate a cloud-init seed ISO per node
3. Run `flash.sh` to flash the base image + attach the seed ISO via the Turing Pi 2 BMC
4. Power on — cloud-init configures the node automatically on first boot

---

## Cluster Layout

| Node | Hostname | Hardware | Role |
|------|----------|----------|------|
| 1 | TuringPiRK1 | Turing RK1, 256GB NVMe | Swarm Manager |
| 2 | — | — | Empty |
| 3 | TuringPICompute3 | CM4, 2TB SATA | Swarm Worker |
| 4 | TuringPICompute4 | CM4, eMMC | Swarm Worker |

---

## Prerequisites

### On your workstation (Linux/WSL)

```bash
# cloud-init seed ISO builder
sudo apt install cloud-image-utils

# (optional) inject cloud-init directly into image
sudo apt install libguestfs-tools

# tpi CLI for flashing via BMC
# https://docs.turingpi.com/docs/tpi-tool
```

### Base images

Download to `images/` — not committed (too large for git):

| Node | Image | Source |
|------|-------|--------|
| RK1 (Node 1) | `ubuntu-rk1-base.img` | [Turing Pi RK1 releases](https://github.com/turing-machines/armbian-build/releases) |
| CM4 (Nodes 3 & 4) | `ubuntu-cm4-base.img` | [Ubuntu Raspberry Pi](https://ubuntu.com/download/raspberry-pi) |

---

## Setup

### 1. Fill in your node configs

Edit each `nodes/<nodeN>/user-data` and replace all `REPLACE_WITH_*` values:

| Placeholder | Value |
|-------------|-------|
| `REPLACE_WITH_SSH_PUBLIC_KEY` | Your public key (`cat ~/.ssh/id_ed25519.pub`) |
| `REPLACE_WITH_TIMEZONE` | e.g. `America/New_York` |
| `REPLACE_WITH_RK1_IP` | Static IP of TuringPiRK1 |
| `REPLACE_WITH_CM3_IP` | Static IP of TuringPICompute3 |
| `REPLACE_WITH_CM4_IP` | Static IP of TuringPICompute4 |

> **Password hashing** (if you want password login):
> ```bash
> mkpasswd -m sha-512   # Linux
> ```

### 2. Build seed ISOs

```bash
chmod +x build-seed.sh
./build-seed.sh          # all nodes
./build-seed.sh node1    # single node
```

Seed ISOs are written to `seeds/`.

### 3. Flash a node

```bash
chmod +x flash.sh

# Flash Node 1 (RK1) — uses seed ISO
./flash.sh 1 rk1

# Flash Node 3 (CM4) — bake cloud-init directly into image
./flash.sh 3 cm4 --inject

# Dry run to preview commands
./flash.sh 4 cm4 --dry-run
```

The `--inject` mode uses `virt-copy-in` to write cloud-init config directly into the image file. The default seed ISO mode requires attaching the ISO via the BMC web UI before powering on.

### 4. Monitor first boot

```bash
# Watch cloud-init progress over serial console
tpi uart -n 1
```

---

## Adding a New Node

1. Create `nodes/<nodeN>/meta-data` and `nodes/<nodeN>/user-data` (copy from `templates/`)
2. Fill in the hostname and your credentials
3. Run `./build-seed.sh node<N>`
4. Run `./flash.sh <N> <rk1|cm4>`

---

## Project Structure

```
BaumConfigure/
├── flash.sh              # Flash base image + attach seed to a node
├── build-seed.sh         # Build cloud-init seed ISOs from node configs
├── nodes/
│   ├── node1/            # TuringPiRK1 (RK1, NVMe, Swarm Manager)
│   │   ├── meta-data
│   │   └── user-data
│   ├── node3/            # TuringPICompute3 (CM4, SATA, Worker)
│   │   ├── meta-data
│   │   └── user-data
│   └── node4/            # TuringPICompute4 (CM4, eMMC, Worker)
│       ├── meta-data
│       └── user-data
├── templates/
│   ├── user-data.template
│   └── meta-data.template
├── images/               # Base .img files (not committed — too large)
└── seeds/                # Generated seed ISOs (not committed)
```

---

## Part of BaumLab

| Project | Description |
|---------|-------------|
| **[BaumConfigure](https://github.com/Bruiserbaum/BaumConfigure)** | *(this repo)* OS image configuration for Turing Pi 2 nodes |
| **[BaumDocker](https://github.com/Bruiserbaum/BaumDocker)** | Docker Compose stacks and Swarm setup for the cluster |

---

## License

Apache License 2.0. See LICENSE for details.
