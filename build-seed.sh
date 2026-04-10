#!/usr/bin/env bash
# build-seed.sh — Build cloud-init seed ISOs for each node
#
# Requires: cloud-image-utils (Ubuntu/Debian: sudo apt install cloud-image-utils)
#
# Usage:
#   ./build-seed.sh          # Build seeds for all nodes
#   ./build-seed.sh node1    # Build seed for a single node

set -euo pipefail

NODES_DIR="$(dirname "$0")/nodes"
OUTPUT_DIR="$(dirname "$0")/seeds"

mkdir -p "$OUTPUT_DIR"

build_node() {
    local node="$1"
    local node_dir="${NODES_DIR}/${node}"

    if [ ! -d "$node_dir" ]; then
        echo "ERROR: No config found for ${node} (expected ${node_dir})"
        return 1
    fi

    if [ ! -f "${node_dir}/user-data" ] || [ ! -f "${node_dir}/meta-data" ]; then
        echo "ERROR: ${node} is missing user-data or meta-data"
        return 1
    fi

    # Warn if REPLACE_WITH_ placeholders are still present
    if grep -q "REPLACE_WITH_" "${node_dir}/user-data" 2>/dev/null; then
        echo "WARNING: ${node}/user-data still contains REPLACE_WITH_ placeholders!"
    fi

    echo "Building seed ISO for ${node}..."
    cloud-localds \
        "${OUTPUT_DIR}/${node}-seed.iso" \
        "${node_dir}/user-data" \
        "${node_dir}/meta-data"

    echo "  -> ${OUTPUT_DIR}/${node}-seed.iso"
}

if [ $# -gt 0 ]; then
    build_node "$1"
else
    for node_dir in "${NODES_DIR}"/*/; do
        build_node "$(basename "$node_dir")"
    done
fi

echo ""
echo "Done. Seed ISOs written to: ${OUTPUT_DIR}/"
echo "Flash with: ./flash.sh <node_number> <rk1|cm4> [--seed]"
