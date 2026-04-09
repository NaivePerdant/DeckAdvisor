# DeckAdvisor

《杀戮尖塔 2》Mod，在卡牌奖励和牌组构建界面为卡牌评分。

## 前置要求

- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- [Godot 4.5.1 Mono](https://godotengine.org/download) — 用于导出 `.pck` 文件
- 通过 Steam 安装的《杀戮尖塔 2》

## 配置

复制模板文件并填入 Godot 路径：

```bash
cp Directory.Build.props.example Directory.Build.props
```

```xml
<GodotPath>/Applications/Godot_mono.app/Contents/MacOS/Godot</GodotPath>
```

游戏路径会从默认 Steam 库自动检测。如果安装在自定义位置，还需设置：

```xml
<Sts2Path>/path/to/steamapps/common/Slay the Spire 2</Sts2Path>
```

## 构建与部署

**macOS / Linux：**

```bash
./build.sh
```

如果游戏路径未能自动检测：

```bash
./build.sh --sts2-path "/path/to/Slay the Spire 2"
```

**Windows：**

```bat
build.bat
```

脚本会自动编译 `.dll`、用 Godot 导出 `.pck`，并将所有文件复制到游戏的 `mods/DeckAdvisor/` 目录。

## 手动构建

```bash
dotnet publish -c Release
```

然后将 `DeckAdvisor.dll`、`DeckAdvisor.json`、`DeckAdvisor.pck` 复制到对应目录：

| 平台 | Mods 目录 |
|------|-----------|
| Windows / Linux | `<STS2>/mods/DeckAdvisor/` |
| macOS | `<STS2>/SlayTheSpire2.app/Contents/MacOS/mods/DeckAdvisor/` |

在游戏 Mod 列表中启用 DeckAdvisor 后重启游戏即可。
