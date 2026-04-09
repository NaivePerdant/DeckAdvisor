# DeckAdvisor

A mod for Slay the Spire 2 that scores cards during reward and deck-building screens.

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- [Godot 4.5.1 Mono](https://godotengine.org/download) — required for `.pck` export
- Slay the Spire 2 installed via Steam

## Setup

Edit `Directory.Build.props` to set your Godot path:

```xml
<GodotPath>/Applications/Godot_mono.app/Contents/MacOS/Godot</GodotPath>
```

STS2 is auto-detected from the default Steam library. If it's in a custom location, also set:

```xml
<Sts2Path>/path/to/steamapps/common/Slay the Spire 2</Sts2Path>
```

## Build & Deploy

**macOS / Linux:**

```bash
./build.sh
```

If STS2 is not auto-detected:

```bash
./build.sh --sts2-path "/path/to/Slay the Spire 2"
```

**Windows:**

```bat
build.bat
```

The script compiles the `.dll`, exports the `.pck` via Godot, and copies everything to the game's `mods/DeckAdvisor/` folder.

## Manual Build

```bash
dotnet publish -c Release
```

Then copy `DeckAdvisor.dll`, `DeckAdvisor.json`, and `DeckAdvisor.pck` to:

| Platform | Mods folder |
|----------|-------------|
| Windows / Linux | `<STS2>/mods/DeckAdvisor/` |
| macOS | `<STS2>/SlayTheSpire2.app/Contents/MacOS/mods/DeckAdvisor/` |

Enable the mod in the game's mod list and restart.
