# Batch 09：正式通关与失败事件接入

## 任务目标

把正式关卡中的出口、伤害、掉落或死亡事件接到现有通关和失败流程，不重写玩家或关卡系统。

## 涉及文件

- 正式关卡 `.unity` 文件
- 正式出口或伤害区域 Prefab（如存在）
- `Assets/Scripts/Levels/LevelFlowTrigger.cs`（仅在正式规则暴露通用缺口时）
- `Assets/Scripts/Levels/LevelFlowEvents.cs`（仅在正式规则暴露通用缺口时）
- 必要的最小适配组件及 `.meta`
- `Docs/GDD.md`（玩法规则发生变化时）
- `Docs/TDD.md`
- `PROGRESS.md`

## 具体实现要求

1. 执行前确认出口、伤害、掉落和死亡的正式规则与对象归属。
2. 简单碰撞条件优先使用 `LevelFlowTrigger`，复杂机关条件使用 `LevelFlowEvents` 或最小适配组件。
3. 只调用现有 `CompleteLevel()` 和 `FailLevel()`，不得新建第二套状态机。
4. 非 Player 不得触发；玩家子碰撞体必须能识别所属 Rigidbody 根节点。
5. 通关和失败均只触发一次，并受当前流程状态保护。
6. 场景与 Prefab 引用通过 Unity Inspector 配置，不手写序列化 YAML。
7. 不修改 Layer、Tag、物理矩阵、敌人设计或角色控制器。

## 依赖关系

- 前置依赖：Batch 08、正式场景、终点与死亡规则。
- 后续依赖：Batch 13。

## 验收标准

- 非 Player 进入触发区无反应。
- 玩家根碰撞体和子碰撞体均可正确识别。
- 出口只触发一次通关，伤害／死亡只触发一次失败。
- 失败后通关不能覆盖失败；通关后失败不能覆盖通关。
- 通关和失败均冻结游戏并显示正确结果界面。
- Retry 恢复正式场景初始状态。
- 场景 diff 只包含预期对象与引用。

## 测试命令

Unity：

```text
逐个打开正式关卡并检查 Exit／Damage 对象 Inspector
使用 Player 与非 Player Collider 分别触发
重复进入触发区验证只执行一次
执行 Retry、Next Level、Return To Main Menu
Tools > Shattered Caverns > Validate Game Flow
```

终端：

```bash
git status --short --branch
git diff --stat -- Assets/Scenes Assets/Prefab Assets/Scripts/Levels
tail -80 Logs/GameFlowValidation.txt
```

## 完成条件

- 每个正式关卡的通关和失败路径均通过。
- 未重写队友玩法逻辑或全局流程。
- `PROGRESS.md` 逐关记录测试证据和提交哈希。
- 场景接入形成独立提交。

