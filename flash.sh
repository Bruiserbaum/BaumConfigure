#!/usr/bin/env bash
# flash.sh — Flash a configured OS image to a Turing Pi 2 node
#
# Requires:
#   - tpi CLI (https://docs.turingpi.com/docs/tpi-tool)
#   - cloud-image-utils (sudo apt install cloud-image-utils)
#   - (optional) libguestfs-tools for --inject mode
#
# Usage:
#   ./flash.sh <node> <image-type> [options]
#
# Arguments:
#   node         Node number: 1, 3, 4
#   image-type   Image type: rk1 | cm4
#
# Options:
#   --inject     Inject cloud-init directly into image (instead of seed ISO)
#   --dry-run    Print commands without executing
#   --no-power   Skip power off/on after flash
#
# Examples:
#   ./flash.sh 1 rk1
#   ./flash.sh 3 cm4 --inject
#   ./flash.sh 4 cm4 --dry-run

set -euo pipefail

SCRIPT_DIR="$(dirname "$0")"
IMAGES_DIR="${SCRIPT_DIR}/images"
NODES_DIR="${SCRIPT_DIR}/nodes"
SEEDS_DIR="${SCRIPT_DIR}/seeds"

# ── Argument parsing ──────────────────────────────────────────────────────────
NODE="${1:-}"
IMAGE_TYPE="${2:-}"
INJECT=false
DRY_RUN=false
NO_POWER=false

for arg in "${@:3}"; do
    case "$arg" in
        --inject)   INJECT=true ;;
        --dry-run)  DRY_RUN=true ;;
        --no-power) NO_POWER=true ;;
        *) echo "Unknown option: $arg"; exit 1 ;;
    esac
done

if [ -z "$NODE" ] || [ -z "$IMAGE_TYPE" ]; then
    echo "Usage: $0 <node> <rk1|cm4> [--inject] [--dry-run] [--no-power]"
    echo ""
    echo "  node:        1 (TuringPiRK1), 3 (TuringPICompute3), 4 (TuringPICompute4)"
    echo "  image-type:  rk1 | cm4"
    exit 1
fi

# ── Image selection ───────────────────────────────────────────────────────────
case "$IMAGE_TYPE" in
    rk1) BASE_IMG="${IMAGES_DIR}/ubuntu-rk1-base.img" ;;
    cm4) BASE_IMG="${IMAGES_DIR}/ubuntu-cm4-base.img" ;;
    *)
        echo "ERROR: Unknown image type '${IMAGE_TYPE}'. Use: rk1 | cm4"
        exit 1
        ;;
esac

NODE_DIR="${NODES_DIR}/node${NODE}"
SEED_ISO="${SEEDS_DIR}/node${NODE}-seed.iso"

# ── Validation ────────────────────────────────────────────────────────────────
if [ ! -f "$BASE_IMG" ]; then
    echo "ERROR: Base image not found: ${BASE_IMG}"
    echo "Download it to images/ — see README.md for links."
    exit 1
fi

if [ ! -d "$NODE_DIR" ]; then
    echo "ERROR: No node config found at ${NODE_DIR}"
    exit 1
fi

run() {
    if [ "$DRY_RUN" = true ]; then
        echo "[DRY RUN] $*"
    else
        echo "+ $*"
        "$@"
    fi
}

echo "=== BaumConfigure Flash ==="
echo "  Node:       ${NODE} (node${NODE})"
echo "  Image type: ${IMAGE_TYPE}"
echo "  Base image: ${BASE_IMG}"
echo "  Mode:       $([ "$INJECT" = true ] && echo "inject" || echo "seed ISO")"
[ "$DRY_RUN" = true ] && echo "  DRY RUN — no commands will execute"
echo ""

# ── Build working image copy ──────────────────────────────────────────────────
WORK_IMG="/tmp/node${NODE}-$(date +%s).img"
echo "Copying base image to ${WORK_IMG}..."
run cp "$BASE_IMG" "$WORK_IMG"

# ── Inject or seed ────────────────────────────────────────────────────────────
if [ "$INJECT" = true ]; then
    # Requires: sudo apt install libguestfs-tools
    if ! command -v virt-copy-in &>/dev/null; then
        echo "ERROR: libguestfs-tools not installed. Run: sudo apt install libguestfs-tools"
        exit 1
    fi
    echo "Injecting cloud-init config into image..."
    run virt-copy-in -a "$WORK_IMG" "${NODE_DIR}/user-data" /etc/cloud/cloud.cfg.d/
    run virt-copy-in -a "$WORK_IMG" "${NODE_DIR}/meta-data"  /etc/cloud/cloud.cfg.d/
else
    # Build seed ISO if it doesn't exist
    if [ ! -f "$SEED_ISO" ]; then
        echo "Seed ISO not found — building now..."
        run bash "${SCRIPT_DIR}/build-seed.sh" "node${NODE}"
    else
        echo "Using existing seed ISO: ${SEED_ISO}"
    fi
    # Note: seed ISO must be attached via BMC web UI or a second tpi flash call
    echo ""
    echo "NOTE: Seed ISO at ${SEED_ISO}"
    echo "      Attach it via the BMC web UI (Storage -> Upload ISO -> Mount to node ${NODE})"
    echo "      before powering the node on, OR use --inject to bake it into the image."
    echo ""
fi

# ── Flash via tpi ─────────────────────────────────────────────────────────────
if ! command -v tpi &>/dev/null; then
    echo "ERROR: tpi CLI not found. Install from: https://docs.turingpi.com/docs/tpi-tool"
    exit 1
fi

if [ "$NO_POWER" = false ]; then
    echo "Powering off node ${NODE}..."
    run tpi power off -n "$NODE"
    sleep 2
fi

echo "Flashing node ${NODE}..."
run tpi flash -n "$NODE" -i "$WORK_IMG"

if [ "$NO_POWER" = false ]; then
    echo "Powering on node ${NODE}..."
    run tpi power on -n "$NODE"
fi

echo ""
echo "=== Flash complete ==="
echo "Node ${NODE} will configure itself on first boot (cloud-init)."
echo "Monitor progress: tpi uart -n ${NODE}  (Ctrl+C to exit)"
