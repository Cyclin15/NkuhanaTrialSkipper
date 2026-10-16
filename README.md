# NkuhanaTrialSkipper

跳过 N'kuhana 试炼（the Winnowing，Hallowed Concepts DLC）的 BepInEx 插件。

## 行为

- 每次进入 N'kuhana 试炼时，立即按**成功**完成结算，走游戏原生结算路径：
  N'kuhana 宝箱依次打开三只手（金手=每人随机一页 N'tormat、银手=补发 N'tormat、
  铜手=每人留一件虚灵物品），物品返还、重生的锣、出口传送门全部原版行为。
- 铜手（Greater Trial Blessing）自选列表**最大化**（默认开启，可关闭）：
  原版规则是"从本次试炼实际拾取的虚灵物品中选一件永久保留"，即时结算时
  什么都没捡、列表会是空的；开启后改为注入该试炼**可能刷出的全部**虚灵物品
  种类（对照 wiki 的试炼物品池，物品内部名已逐一核实），仍是选一件、永久保留。
- 每次跳过结算后，Reckoning 剩余时间**直接设置到下一档**（每局总预算 210 秒）：
  210 → 150 → 90 → 30（第 n 次跳过 = 210 − 60×n）。
- 触发门槛：当前剩余 ≥ 60 秒。剩余不足 60 秒时本次进入不触发跳过，试炼正常进行。
  满预算一局最多触发 **3 次**，收在 30 秒档，不会归零。
- 仅在主机/单人端生效（结算逻辑为服务器权威）。

## 配置

`BepInEx/config/com.zcode.NkuhanaTrialSkipper.cfg`，或游戏内 Risk of Options 模组设置：

| 配置项 | 默认值 | 说明 |
|---|---|---|
| `Enabled` | `true` | 总开关 |
| `TimeCostSeconds` | `60` | 档位差（秒）。第 n 次跳过后剩余 = 210 − n×档位差 |
| `MaximizeBronzeChoices` | `true` | 跳过结算时把铜手自选列表最大化（注入该试炼的全部虚灵物品种类） |

## 构建

```
dotnet build -c Release
```

编译后自动把 dll/pdb 复制到 r2modman Default profile 的
`BepInEx/plugins/NkuhanaTrialSkipper/`（路径配置在 csproj 的 `ProfilePluginsDir`）。

## 原理（RoR2 1.5.0）

- `BaseTrialQuest.OnEnter` 后缀：登记新试炼任务（此帧任务尚未被 `QuestRunner` 激活）。
- `QuestRunner.Update` 后缀：任务激活后的第一帧，守门检查通过后先向宝箱铜手组件
  （`CreateItemPotentialFromPickedUpItems`，试炼期间始终存在于场景中）的私有
  `_pickedUpItems` 集合注入该试炼的全部虚灵物品种类（`RewardPools`），再反射调用
  `BaseQuest.EndQuest(true)`，游戏随后自己走 `OnExit` 完成全部原生结算。
- `BaseTrialQuest.OnExit` 后缀：结算完成后 `DLC4TrialSelector.SetRemainingTrialTimeServer`
  把剩余时间直接设置到下一档（210 − 60×n，保底 0）。

`recon/` 目录是反编译参考代码，不参与编译。
