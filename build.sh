#!/bin/bash
# build.sh — compile and package DeckAdvisor mod
# Usage: ./build.sh [--sts2-path /path/to/sts2]
set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
MOD_NAME="DeckAdvisor"

# Parse optional --sts2-path argument
STS2_PATH=""
while [[ $# -gt 0 ]]; do
    case $1 in
        --sts2-path) STS2_PATH="$2"; shift 2 ;;
        *) echo "Unknown argument: $1"; exit 1 ;;
    esac
done

# Auto-detect macOS Steam path if not provided
if [[ -z "$STS2_PATH" ]]; then
    DEFAULT="$HOME/Library/Application Support/Steam/steamapps/common/Slay the Spire 2"
    if [[ -d "$DEFAULT" ]]; then
        STS2_PATH="$DEFAULT"
    else
        echo "ERROR: Could not find Slay the Spire 2. Pass --sts2-path <path>"
        exit 1
    fi
fi

MODS_DIR="$STS2_PATH/SlayTheSpire2.app/Contents/MacOS/mods"
GODOT_PATH=$(find "$HOME" /usr/local /opt -name "Godot_mono*" -o -name "Godot*Mono*" 2>/dev/null | grep -i "4.5.1" | head -1)

echo "==> STS2 path: $STS2_PATH"
echo "==> Mods dir:  $MODS_DIR"

# 1. Build .dll
echo "==> Building $MOD_NAME.dll..."
cd "$SCRIPT_DIR"
dotnet publish -c Release -p:Sts2Path="$STS2_PATH" -o "$SCRIPT_DIR/dist/$MOD_NAME"

# 2. Export .pck with Godot (requires Godot 4.5.1 Mono)
if [[ -n "$GODOT_PATH" ]]; then
    echo "==> Exporting .pck with Godot: $GODOT_PATH"
    "$GODOT_PATH" --headless --export-pack "BasicExport" "$SCRIPT_DIR/dist/$MOD_NAME/$MOD_NAME.pck"
else
    echo "WARNING: Godot 4.5.1 Mono not found — skipping .pck export."
    echo "         Install Godot 4.5.1 Mono and re-run, or set GODOT_PATH manually."
fi

# 3. Copy manifest
cp "$SCRIPT_DIR/$MOD_NAME.json" "$SCRIPT_DIR/dist/$MOD_NAME/"

# 4. Deploy to mods folder
if [[ -d "$MODS_DIR" ]]; then
    echo "==> Deploying to $MODS_DIR/$MOD_NAME/"
    mkdir -p "$MODS_DIR/$MOD_NAME"
    cp -r "$SCRIPT_DIR/dist/$MOD_NAME/." "$MODS_DIR/$MOD_NAME/"
    echo "==> Done! Enable DeckAdvisor in the game's mod list."
else
    echo "==> Mods directory not found. Package is at: $SCRIPT_DIR/dist/$MOD_NAME/"
fi
