# DeckAdvisor 开发笔记

## 项目概述
STS2 (Slay the Spire 2) 的 Harmony mod，在卡牌奖励界面显示每张牌的评分标签。

## 关键架构

### 游戏节点层级（奖励界面）
```
NCardRewardSelectionScreen (Control)
  └── UI (Control)
       └── CardRow (Control)  ← _cardRow 字段
            └── NGridCardHolder (scale=0.8)  ← SmallScale = Vector2.One * 0.8f
                 └── NCard (Control, scale=1,1)
```

### NCard 关键信息
- `NCard.defaultSize = new Vector2(300f, 422f)` — 卡片固定尺寸常量
- `NCard.Size` 在 `UpdateVisuals` 时为 `(0,0)`（layout 未计算）
- `NCard.Scale` 在奖励界面为 `(1,1)`，缩放由父节点 `NGridCardHolder` 控制
- `NGridCardHolder.SmallScale = Vector2.One * 0.8f`

### 评分标签位置
- 标签加在 `NGridCardHolder`（NCard 的父节点）上，不是 NCard 上
- 当前 Y 偏移：`250f`（在 holder 坐标系里，holder scale=0.8 后显示在卡片底部附近）
- X 居中：`(NCard.defaultSize.X - lblSize.X) / 2f`

## 关键 API

### 获取当前 RunState / Player
```csharp
var state = RunManager.Instance.DebugOnlyGetState();
var player = state.Players.FirstOrDefault();
```
- `RunManager` 只有一个静态属性 `Instance`
- `DebugOnlyGetState()` 是公开方法，直接调用

### 获取奖励卡列表
```csharp
// 从 NCardRewardSelectionScreen 反射读取 _options 字段
var field = screen.GetType().GetField("_options",
    BindingFlags.NonPublic | BindingFlags.Instance);
var options = field.GetValue(screen) as IReadOnlyList<CardCreationResult>;
var cards = options.Select(o => o.Card).ToList();
```
- `_options` 类型是 `List<CardCreationResult>`（运行时），但声明为 `IReadOnlyList<CardCreationResult>`
- `CardCreationResult.Card` 返回 `CardModel`（可能是 relic 修改后的版本）

### ModelId 相等性
- `ModelId` 是 C# record，实现了 `Equals`/`GetHashCode`
- `GetHashCode` 包含 `EqualityContract`（运行时类型），子类实例与父类实例 hash 不同
- 实际使用中两个 `ModelId` 只要 Category+Entry 相同且类型相同就能正确匹配

## 已知问题 / 注意事项

### Harmony patch async 方法
- `CardSelectCmd.FromChooseACardScreen` 是 async 方法（编译器生成 `d__8` 状态机）
- Harmony `Prefix` patch async 方法会静默失败（参数签名不匹配）
- **解决方案**：改为在 `NCard.UpdateVisuals` 的 Postfix 里懒加载评分

### UpdateVisuals 调用时机
- `UpdateVisuals` 在 `RefreshOptions` 循环里逐张调用，此时其他卡还未加入场景树
- 不能依赖兄弟节点来收集所有奖励卡，要用 `_options` 反射

### CardScorer.Current 清空时机
- `EvaluateFromReflection` 调用 `Evaluate`，`Evaluate` 会 `Current.Clear()`
- 用 `ContainsKey` 判断是否已评分，避免重复清空

## 游戏日志路径
```
~/Library/Application Support/SlayTheSpire2/logs/godot.log
```

## Mod 部署路径（macOS）
```
~/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app/Contents/MacOS/mods/DeckAdvisor/
```
`dotnet build` 会自动复制 dll 到此路径（见 `Sts2PathDiscovery.props`）。

## 反编译资源
完整反编译代码在 `/Users/master/Downloads/code/`，按命名空间分目录。
关键文件：
- `MegaCrit.Sts2.Core.Nodes.Cards/NCard.cs`
- `MegaCrit.Sts2.Core.Nodes.Cards.Holders/NGridCardHolder.cs`
- `MegaCrit.Sts2.Core.Nodes.Cards.Holders/NCardHolder.cs`
- `MegaCrit.Sts2.Core.Nodes.Screens.CardSelection/NCardRewardSelectionScreen.cs`
- `MegaCrit.Sts2.Core.Runs/RunManager.cs`
